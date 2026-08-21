using System.Runtime.InteropServices;
using exchange_simulator.Enums;
using exchange_simulator.Models;
using exchange_simulator.Models.AccountTrading;
using exchange_simulator.Models.TradingCore;

namespace exchange_simulator.Services;

public sealed class AccountTradingService
{
    private readonly TradingEngine _tradingEngine;
    private readonly Dictionary<Guid, TradingAccount> _accounts = [];
    private readonly Dictionary<Guid, List<OrderHistoryEntry>> _orderHistory = [];
    private readonly List<OrderHistoryEntry> _orderHistoryEntries = [];
    
    public Instrument Instrument => _tradingEngine.Instrument;

    public AccountTradingService(Instrument instrument) 
        : this(new TradingEngine(instrument)) { }

    private AccountTradingService(TradingEngine tradingEngine)
    {
        ArgumentNullException.ThrowIfNull(tradingEngine);
        _tradingEngine = tradingEngine;
    }

    public static AccountTradingService Restore(Instrument instrument, AccountTradingRestoreState state)
    {
        ArgumentNullException.ThrowIfNull(instrument);
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(state.Accounts);
        ArgumentNullException.ThrowIfNull(state.Orders);
        ArgumentNullException.ThrowIfNull(state.Trades);
        ArgumentNullException.ThrowIfNull(state.OrderHistory);
        
        var accounts = state.Accounts.ToArray();
        var orders = state.Orders.ToArray();
        var trades = state.Trades.ToArray();
        var orderHistory = state.OrderHistory.ToArray();

        var service = new AccountTradingService(TradingEngine.Restore(instrument, orders, trades));
        
        
        foreach (var accountState in accounts)
        {
            if (accountState is null)
                throw new ArgumentException("accounts cannot contain null", nameof(state));

            var account = TradingAccount.Restore(accountState, instrument);

            if (!service._accounts.TryAdd(account.Id, account))
                throw new ArgumentException($"duplicate account {account.Id}", nameof(state));
        }
        
        
        foreach (var order in orders)
        {
            if (!service._accounts.ContainsKey(order.OwnerId))
                throw new ArgumentException($"order {order.Id} references a missing account", nameof(state));
        }
        
        
        foreach (var entry in orderHistory)
        {
            if (entry is null)
                throw new ArgumentException("order history cannot contain null", nameof(state));
            if (!service._tradingEngine.TryGetOrder(entry.OrderId, out var order))
                throw new ArgumentException($"history references missing order {entry.OrderId}", nameof(state));

            ValidateRestoredHistoryEntry(entry, order!);

            if (!service._orderHistory.TryGetValue(entry.OrderId, out var history))
            {
                history = [];
                service._orderHistory.Add(entry.OrderId, history);
            }

            history.Add(entry);
            service._orderHistoryEntries.Add(entry);
        }
        
        
        foreach (var order in orders)
        {
            if (!service._orderHistory.TryGetValue(order.Id, out var history))
                throw new ArgumentException($"order {order.Id} has no history", nameof(state));

            ValidateRestoredOrderHistory(order, history);
        }

        
        return service;
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
    
    public AccountOperation GrantInitialInstruments(Guid accountId, long quantity) =>
        GetAccount(accountId).GrantInitialInstruments(quantity);
    
    public OrderBookSnapshot GetOrderBookSnapshot() =>
        _tradingEngine.GetOrderBookSnapshot();
    
    public decimal GetReferencePrice() =>
        _tradingEngine.GetReferencePrice();
    
    public IReadOnlyList<Trade> GetTrades() =>
        _tradingEngine.GetTrades();

    public bool TryGetOrder(Guid accountId, Guid orderId, out OrderSnapshot? snapshot)
    {
        GetAccount(accountId);

        if (!_tradingEngine.TryGetOrder(orderId, out var order) ||
            order!.OwnerId != accountId)
        {
            snapshot = null;
            return false;
        }

        snapshot = order;
        return true;
    }

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
            
            var sizeAtLevel = level.Size >= remainingSize ? remainingSize : (long)level.Size;

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
            try
            {
                reservedCash = command.Type == OrderType.Limit
                    ? checked(command.Price!.Value * command.Size)
                    : GetMarketBuyQuote(command.Size).Cost;
            }
            catch (OverflowException)
            {
                return RejectOrder(OrderRejectionReason.OrderValueTooLarge);
            }
            
            if (reservedCash > 0 && !account.TryReserveCash(reservedCash))
                return RejectOrder(OrderRejectionReason.InsufficientAvailableCash);
        }
        else
        {
            reservedInstruments = command.Size;

            if (!account.TryReserveInstruments(reservedInstruments))
                return RejectOrder(OrderRejectionReason.InsufficientAvailablePosition);
        }

        var settlementValidated = false;
        OrderCommandResult result;

        try
        {
            result = _tradingEngine.PlaceOrder(command,
                plannedTrades =>
                {
                    ValidateSettlement(plannedTrades);
                    settlementValidated = true;
                });
        }
        catch
        {
            if (!settlementValidated)
                ReleaseReservation(account, reservedCash, reservedInstruments);
            throw;
        }

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

    internal IReadOnlyList<OrderSnapshot> ApplyRestartPolicy()
    {
        var activeOrders = _tradingEngine.GetActiveOrders()
            .OrderBy(order => order.CreatedAt)
            .ThenBy(order => order.Id).ToArray();
        
        ValidateRestartReservations(activeOrders);

        var cancelledOrders = new List<OrderSnapshot>(activeOrders.Length);

        foreach (var activeOrder in activeOrders)
        {
            var result = CancelOrder(activeOrder.Id);

            if (!result.IsSuccess || result.Trades.Count != 0 ||
                result.Order is not { OrderStatus: OrderStatus.Cancelled } cancelledOrder)
                throw new InvalidOperationException($"Restart cancellation failed for active order {activeOrder.Id}");
            
            cancelledOrders.Add(cancelledOrder);
        }
        
        return cancelledOrders.ToArray();
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

        return _orderHistoryEntries
            .Where(entry => GetOrder(entry.OrderId).OwnerId == accountId).ToArray();
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
    
    private void ValidateSettlement(IReadOnlyList<PlannedTrade> plannedTrades)
    {
        var projections = new Dictionary<Guid, SettlementProjection>();

        foreach (var plannedTrade in plannedTrades)
        {
            if (plannedTrade.BuyerAccountId == plannedTrade.SellerAccountId)
                continue;

            var cost = checked(plannedTrade.Price * plannedTrade.Size);
            GetProjection(plannedTrade.BuyerAccountId).ApplyBuy(plannedTrade.Size, cost);
            GetProjection(plannedTrade.SellerAccountId).ApplySell(cost);
        }

        SettlementProjection GetProjection(Guid accountId)
        {
            if (projections.TryGetValue(accountId, out var projection))
                return projection;

            projection = new SettlementProjection(GetAccount(accountId));
            projections.Add(accountId, projection);
            return projection;
        }
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

    private void ValidateRestartReservations(IReadOnlyList<OrderSnapshot> activeOrders)
    {
        var reservedCashByAccount = new Dictionary<Guid, decimal>();
        var reservedInstrumentsByAccount = new Dictionary<Guid, long>();

        foreach (var order in activeOrders)
        {
            GetAccount(order.OwnerId);

            if (order.OrderSide == OrderSide.Buy)
            {
                var price = order.Price ??
                            throw new InvalidOperationException(
                                $"Active buy order {order.Id} must have a limit price");
                
                var reservation = checked(price * order.RemainingSize);
                var currentReservation = reservedCashByAccount.GetValueOrDefault(order.OwnerId);
                
                reservedCashByAccount[order.OwnerId] = checked(currentReservation + reservation);
            }
            else
            {
                var currentReservation = reservedInstrumentsByAccount.GetValueOrDefault(order.OwnerId);
                
                reservedInstrumentsByAccount[order.OwnerId] = checked(currentReservation + order.RemainingSize);
            }
        }

        foreach (var account in _accounts.Values)
        {
            var expectedCash = reservedCashByAccount.GetValueOrDefault(account.Id);
            var expectedInstruments = reservedInstrumentsByAccount.GetValueOrDefault(account.Id);
            
            if (account.ReservedCash != expectedCash)
                throw new InvalidOperationException(
                    $"Account {account.Id} cash reserve does not match its active orders");

            if (account.Position.ReservedQuantity != expectedInstruments)
                throw new InvalidOperationException(
                    $"Account {account.Id} instrument reserve does not match its active orders");
        }
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
        
        var acceptedEntry = new OrderHistoryEntry(
            order.Id,
            OrderHistoryEventType.Accepted,
            0,
            order.Size,
            null,
            order.CreatedAt);
        
        _orderHistory.Add(order.Id, [acceptedEntry]);
        _orderHistoryEntries.Add(acceptedEntry);

        foreach (var trade in result.Trades)
        {
            RecordTradeHistory(trade.BuyOrderId, trade);
            RecordTradeHistory(trade.SellOrderId, trade);
        }

        if (order.OrderStatus == OrderStatus.Active)
        {
            AddHistoryEntry(
                order.Id,
                OrderHistoryEventType.Activated,
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
        var entry = new OrderHistoryEntry(
            orderId,
            eventType,
            filledSize,
            remainingSize,
            tradeId,
            occurredAt);
        
        GetHistory(orderId).Add(entry);
        _orderHistoryEntries.Add(entry);
    }
    
    private static void ValidateRestoredHistoryEntry(OrderHistoryEntry entry, OrderSnapshot order)
    {
        if (!Enum.IsDefined(entry.EventType))
            throw new ArgumentException("order history event type is invalid", nameof(entry));
        if (entry.FilledSize < 0 || entry.FilledSize > order.Size)
            throw new ArgumentException("order history filled size is invalid", nameof(entry));
        if (entry.RemainingSize < 0 || entry.RemainingSize > order.Size)
            throw new ArgumentException("order history remaining size is invalid", nameof(entry));
        if (entry.FilledSize != order.Size - entry.RemainingSize)
            throw new ArgumentException("order history sizes do not match the order size", nameof(entry));
        if (entry.TradeId == Guid.Empty)
            throw new ArgumentException("order history contains an empty trade ID", nameof(entry));

        var isTradeEvent = entry.EventType is OrderHistoryEventType.PartiallyFilled or OrderHistoryEventType.Filled;

        if (isTradeEvent != entry.TradeId.HasValue)
            throw new ArgumentException("order history trade reference does not match its event type", nameof(entry));
    }
    
    private static void ValidateRestoredOrderHistory(OrderSnapshot order, IReadOnlyList<OrderHistoryEntry> history)
    {
        var first = history[0];

        if (first.EventType != OrderHistoryEventType.Accepted ||
            first.FilledSize != 0 ||
            first.RemainingSize != order.Size ||
            first.TradeId.HasValue)
            throw new ArgumentException($"order {order.Id} history must start with acceptance", nameof(history));

        var last = history[^1];

        if (last.FilledSize != order.FilledSize ||
            last.RemainingSize != order.RemainingSize)
            throw new ArgumentException($"order {order.Id} history does not match its current size", nameof(history));

        var finalEventMatchesStatus = order.OrderStatus switch
        {
            OrderStatus.Active => last.EventType is OrderHistoryEventType.Activated or OrderHistoryEventType.PartiallyFilled,
            OrderStatus.Filled => last.EventType == OrderHistoryEventType.Filled,
            OrderStatus.Cancelled => last.EventType == OrderHistoryEventType.Cancelled,
            _ => false
        };

        if (!finalEventMatchesStatus)
            throw new ArgumentException($"order {order.Id} history does not match its status", nameof(history));
    }

    private List<OrderHistoryEntry> GetHistory(Guid orderId)
    {
        if (!_orderHistory.TryGetValue(orderId, out var history))
            throw new InvalidOperationException($"Order history for {orderId} does not exist");
        
        return history;
    }

    private sealed class SettlementProjection
    {
        private decimal _cashBalance;
        private long _positionQuantity;
        private decimal _positionAveragePrice;

        public SettlementProjection(TradingAccount account)
        {
            _cashBalance = account.CashBalance;
            _positionQuantity = account.Position.Quantity;
            _positionAveragePrice = account.Position.AveragePrice;
        }

        public void ApplyBuy(long quantity, decimal cost)
        {
            var newQuantity = checked(_positionQuantity + quantity);
            var currentCost = checked(_positionAveragePrice * _positionQuantity);
            var newCost = checked(currentCost + cost);

            _positionQuantity = newQuantity;
            _positionAveragePrice = newCost / newQuantity;
        }

        public void ApplySell(decimal proceeds) =>
            _cashBalance = checked(_cashBalance + proceeds);
    }
}