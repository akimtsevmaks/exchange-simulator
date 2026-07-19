using exchange_simulator.Enums;
using exchange_simulator.Models;

namespace exchange_simulator.Services;

public sealed class AccountTradingService
{
    private readonly TradingEngine _tradingEngine;
    private readonly Dictionary<Guid, TradingAccount> _accounts = [];
    
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
}