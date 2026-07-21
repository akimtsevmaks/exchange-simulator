using exchange_simulator.Enums;
using exchange_simulator.Models;

namespace exchange_simulator.Services;

public sealed class AccountTradingService
{
    private readonly TradingEngine _tradingEngine;
    private readonly Dictionary<Guid, TradingAccount> _accounts = [];
    private readonly Dictionary<Guid, List<OrderHistoryEntry>> _orderHistory = [];
    
    public Instrument Instrument => _tradingEngine.Instrument;

    public AccountTradingService(TradingEngine tradingEngine)
    {
        ArgumentNullException.ThrowIfNull(tradingEngine);
        _tradingEngine = tradingEngine;
    }

    public TradingAccountSnapshot RegisterAccount(Guid accountId)
    {
        var account = new TradingAccount(accountId, Instrument);

        if (!_accounts.TryAdd(accountId, account))
            throw new InvalidOperationException($"Account {accountId} is already registered");

        return account.GetSnapshot();
    }
    
    public AccountOperation GrantInitialCash(Guid accountId, decimal amount) =>
        GetAccount(accountId).GrantInitialCash(amount);
    
    public AccountOperation GrantInitialInstrument(Guid accountId, long quantity) =>
        GetAccount(accountId).GrantInitialInstruments(quantity);

    public MarketBuyQuote GetMarketBuyQuote(long requestedSize)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(requestedSize);
        
        if (requestedSize % Instrument.LotSize != 0)
            throw new ArgumentException("Quantity must be a multiple of lot size");

        var remainingSize = requestedSize;
        var executableSize = 0L;
        var cost = 0m;

        foreach (var level in _tradingEngine.GetOrderBookSnapshot().Asks)
        {
            if (remainingSize == 0)
                break;
            
            var sizeAtLevel = Math.Min(remainingSize, level.Size);

            executableSize += sizeAtLevel;
            remainingSize -= sizeAtLevel;
            cost = checked(cost + level.Price * sizeAtLevel);
        }
        
        return new MarketBuyQuote(requestedSize, executableSize, remainingSize, cost);
    }

    public OrderCommandResult PlaceOrder(PlaceOrderCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        
        if (!_tradingEngine.ValidateOrderRequest(command, out var validationReason))
            return RejectOrder(validationReason!.Value);
        if (!_accounts.TryGetValue(command.OwnerId, out var account))
            return RejectOrder(OrderRejectionReason.AccountNotFound);
        
        var reservedCash = 0m;
        var reservedInstruments = 0L;

        if (command.Side == OrderSide.Buy)
        {
            reservedCash = command.Type == OrderType.Limit
                ? checked(command.Price!.Value * command.Size)
                : GetMarketBuyQuote(command.Size).Cost;
            
            if (reservedCash > 0 && !account.TryReserveCash(reservedCash))
                return RejectOrder(OrderRejectionReason.InsufficientAvailableCash);
        }
        else
        {
            reservedInstruments = command.Size;

            if (!account.TryReserveInstruments(reservedInstruments))
                return RejectOrder(OrderRejectionReason.InsufficientAvailablePosition);
        }
        
        var result = _tradingEngine.PlaceOrder(command);

        if (!result.IsSuccess)
        {
            ReleaseReservation(account, reservedCash, reservedInstruments);
            return result;
        }

        foreach (var trade in result.Trades)
            SettleTrade(trade);

        ReleaseUnusedMarketReservation(account, command, result, reservedCash, reservedInstruments);
        RecordPlacementHistory(result);
        return result;
    }

    public OrderCommandResult CancelOrder(Guid orderId)
    {
        if (!_tradingEngine.TryGetOrder(orderId, out var order))
            return RejectOrder(OrderRejectionReason.OrderNotFound);
        
        if (!_accounts.TryGetValue(order!.OwnerId, out var account))
            return RejectOrder(OrderRejectionReason.AccountNotFound);
        
        var result = _tradingEngine.CancelOrder(orderId);

        if (!result.IsSuccess)
            return result;

        var cancelledOrder = result.Order ??
                             throw new InvalidOperationException("Successful cancellation must return an order");

        if (cancelledOrder.OrderSide == OrderSide.Buy)
        {
            var price = cancelledOrder.Price ??
                        throw new InvalidOperationException("Active buy order must have a limit price");
            var cashToRelease = checked(price * cancelledOrder.RemainingSize);
            account.ReleaseCash(cashToRelease);
        }
        else
        {
            account.ReleaseInstruments(cancelledOrder.RemainingSize);
        }

        RecordCancellationHistory(cancelledOrder);
        return result;
    }
    
    public bool TryGetAccount(Guid accountId, out TradingAccountSnapshot? snapshot)
    {
        if (!_accounts.TryGetValue(accountId, out var account))
        {
            snapshot = null;
            return false;
        }
        
        snapshot = account.GetSnapshot();
        return true;
    }

    public IReadOnlyList<AccountOperation> GetAccountOperations(Guid accountId) =>
        GetAccount(accountId).GetOperations();

    public IReadOnlyList<OrderSnapshot> GetActiveOrders(Guid accountId)
    {
        GetAccount(accountId);
        
        return _tradingEngine.GetActiveOrders()
            .Where(order => order.OwnerId == accountId).ToArray();
    }

    public IReadOnlyList<OrderHistoryEntry> GetAccountOrderHistory(Guid accountId)
    {
        GetAccount(accountId);

        return _orderHistory
            .Where(item => GetOrder(item.Key).OwnerId == accountId)
            .SelectMany(item => item.Value)
            .OrderBy(entry => entry.OccurredAt).ToArray();
    }

    public IReadOnlyList<Trade> GetAccountTrades(Guid accountId)
    {
        GetAccount(accountId);
        
        return _tradingEngine.GetTrades()
            .Where(trade =>
                GetOrder(trade.BuyOrderId).OwnerId == accountId ||
                GetOrder(trade.SellOrderId).OwnerId == accountId).ToArray();
    }

    public IReadOnlyList<OrderHistoryEntry> GetOrderHistory(Guid orderId)
    {
        if (!_orderHistory.TryGetValue(orderId, out var history))
            throw new KeyNotFoundException($"Order history for {orderId} eas not found");
        
        return history.ToArray();
    }

    private TradingAccount GetAccount(Guid accountId)
    {
        if (!_accounts.TryGetValue(accountId, out var account))
            throw new KeyNotFoundException($"Account {accountId} is not registered");
        
        return account;
    }

    private void SettleTrade(Trade trade)
    {
        var buyOrder = GetOrder(trade.BuyOrderId);
        var sellOrder = GetOrder(trade.SellOrderId);
        var buyer = GetAccount(buyOrder.OwnerId);
        var seller = GetAccount(sellOrder.OwnerId);
        var buyReservedCash = buyOrder.OrderType == OrderType.Limit
            ? checked(buyOrder.Price!.Value * trade.Size)
            : checked(trade.Price * trade.Size);
        
        if (buyer.Id == seller.Id)
        {
            buyer.SettleSelfTrade(trade, buyOrder.Id, sellOrder.Id, buyReservedCash);
            return;
        }
        
        buyer.SettleBuy(trade, buyOrder.Id, buyReservedCash);
        seller.SettleSell(trade, sellOrder.Id);
    }

    private OrderSnapshot GetOrder(Guid orderId)
    {
        if (!_tradingEngine.TryGetOrder(orderId, out var order))
            throw new InvalidOperationException($"trade references missing order {orderId}");

        return order!;
    }
    
    private static void ReleaseReservation( 
        TradingAccount account,
        decimal reservedCash,
        long reservedInstruments)
    {
        if (reservedCash > 0)
            account.ReleaseCash(reservedCash);
        if (reservedInstruments > 0)
            account.ReleaseInstruments(reservedInstruments);
    }
    
    private static void ReleaseUnusedMarketReservation(
        TradingAccount account,
        PlaceOrderCommand command,
        OrderCommandResult result,
        decimal reservedCash,
        long reservedInstruments)
    {
        if (command.Type != OrderType.Market)
            return;

        var usedCash = result.Trades.Sum(trade => checked(trade.Price * trade.Size));
        var usedInstruments = result.Trades.Sum(trade => trade.Size);
        var cashToRelease = reservedCash - usedCash;
        var instrumentsToRelease = reservedInstruments - usedInstruments;

        if (cashToRelease > 0)
            account.ReleaseCash(cashToRelease);
        if (instrumentsToRelease > 0)
            account.ReleaseInstruments(instrumentsToRelease);
    }

    private static OrderCommandResult RejectOrder(OrderRejectionReason reason) =>
        new(false, reason, null, []);

    private void RecordPlacementHistory(OrderCommandResult result)
    {
        var order = result.Order ??
                    throw new InvalidOperationException("Successful placement must return an order");
        
        _orderHistory.Add(order.Id,
            [
            new OrderHistoryEntry(
                order.Id,
                OrderHistoryEventType.Accepted,
                0,
                order.Size,
                null,
                order.CreatedAt)
            ]);

        foreach (var trade in result.Trades)
        {
            RecordTradeHistory(trade.BuyOrderId, trade);
            RecordTradeHistory(trade.SellOrderId, trade);
        }

        if (order.OrderStatus == OrderStatus.Active)
        {
            AddHistoryEntry(
                order.Id,
                OrderHistoryEventType.Accepted,
                order.FilledSize,
                order.RemainingSize,
                null,
                DateTimeOffset.UtcNow);
        }
        else if (order.OrderStatus == OrderStatus.Cancelled)
        {
            AddHistoryEntry(
                order.Id,
                OrderHistoryEventType.Cancelled,
                order.FilledSize,
                order.RemainingSize,
                null,
                DateTimeOffset.UtcNow);
        }
    }

    private void RecordTradeHistory(Guid orderId, Trade trade)
    {
        var order = GetOrder(orderId);
        var history = GetOrderHistory(orderId);
        var filledSize = checked(history[^1].FilledSize + trade.Size);

        if (filledSize > order.Size)
            throw new InvalidOperationException($"Trade overfills order {orderId} history");

        var remainingSize = order.Size - filledSize;
        var eventType = remainingSize == 0
            ? OrderHistoryEventType.Filled
            : OrderHistoryEventType.PartiallyFilled;

        AddHistoryEntry(
            orderId,
            eventType,
            filledSize,
            remainingSize,
            trade.Id,
            trade.ExecutedAt);
    }
    
    private void RecordCancellationHistory(OrderSnapshot order) =>
        AddHistoryEntry(
            order.Id,
            OrderHistoryEventType.Cancelled,
            order.FilledSize,
            order.RemainingSize,
            null,
            DateTimeOffset.UtcNow);

    private void AddHistoryEntry(
        Guid orderId,
        OrderHistoryEventType eventType,
        long filledSize,
        long remainingSize,
        Guid? tradeId,
        DateTimeOffset occurredAt)
    {
        GetHistory(orderId).Add(new OrderHistoryEntry(
            orderId,
            eventType,
            filledSize,
            remainingSize,
            tradeId,
            occurredAt));
    }

    private List<OrderHistoryEntry> GetHistory(Guid orderId)
    {
        if (!_orderHistory.TryGetValue(orderId, out var history))
            throw new InvalidOperationException($"Order history for {orderId} does not exist");
        
        return history;
    }

}