using exchange_simulator.Enums;
using exchange_simulator.Models.TradingCore;

namespace exchange_simulator.Models.AccountTrading;

public class TradingAccount
{
    private readonly List<AccountOperation> _operations = [];
    private readonly decimal _initialInstrumentPrice;
    
    public Guid Id { get; }
    
    public decimal CashBalance { get; private set; }
    public decimal ReservedCash { get; private set; }
    public decimal AvailableCash =>  CashBalance - ReservedCash;
    
    public Position Position { get; }

    public TradingAccount(Guid id, Instrument instrument)
    {
        if (id == Guid.Empty)
            throw new ArgumentException("invalid account ID", nameof(id));
        ArgumentNullException.ThrowIfNull(instrument);
        
        Id = id;
        _initialInstrumentPrice = instrument.InitialPrice;
        Position = new Position(instrument.Id);
    }

    public AccountOperation GrantInitialCash(decimal amount)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(amount);
        
        var newBalance = checked(CashBalance + amount);
        var operation = new AccountOperation(
            Guid.NewGuid(),
            Id,
            AccountOperationType.InitialCashGranted,
            amount,
            null,
            0,
            null,
            null,
            DateTimeOffset.UtcNow);
        
        _operations.Add(operation);
        CashBalance = newBalance;
        
        return operation;
    }

    public AccountOperation GrantInitialInstruments(long quantity)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(quantity);

        var operation = new AccountOperation(
            Guid.NewGuid(),
            Id,
            AccountOperationType.InitialInstrumentsGranted,
            0,
            Position.InstrumentId,
            quantity,
            null,
            null,
            DateTimeOffset.UtcNow);
        
        Position.GrantInitialQuantity(quantity, _initialInstrumentPrice);
        _operations.Add(operation);
        
        return operation;
    }

    public bool TryReserveCash(decimal amount)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(amount);

        if (amount > AvailableCash)
            return false;
        
        ReservedCash += amount;
        return true;
    }

    public void ReleaseCash(decimal amount)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(amount);
        
        if (amount > ReservedCash)
            throw new InvalidOperationException("Can't release more than reserved quantity");
        
        ReservedCash -= amount;
    }
    
    public bool TryReserveInstruments(long quantity) =>
        Position.TryReserve(quantity);
    
    public void ReleaseInstruments(long quantity) =>
         Position.Release(quantity);

    internal AccountOperation SettleBuy(Trade trade, Guid orderId, decimal reservedCash)
    {
        ArgumentNullException.ThrowIfNull(trade);
        
        if (orderId == Guid.Empty)
            throw new ArgumentException("invalid order id", nameof(orderId));
        if (trade.InstrumentId != Position.InstrumentId)
            throw new InvalidOperationException("trade instrument does not match position instrument");

        var cost = checked(trade.Price * trade.Size);

        if (reservedCash < cost)
            throw new InvalidOperationException("Reserved cash cannot be less than trade cost");
        if (reservedCash > ReservedCash)
            throw new InvalidOperationException("Trade uses more than reserved cash");
        if (cost > CashBalance)
            throw new InvalidOperationException("Trade uses more than account balance");

        Position.Buy(trade.Size, cost);
        ReservedCash -= reservedCash;
        CashBalance -= cost;

        var operation = new AccountOperation(
            Guid.NewGuid(),
            Id,
            AccountOperationType.TradeBuy,
            -cost,
            Position.InstrumentId,
            trade.Size,
            orderId,
            trade.Id,
            trade.ExecutedAt);
        
        _operations.Add(operation);
        return operation;
    }

    internal AccountOperation SettleSell(Trade trade, Guid orderId)
    {
        ArgumentNullException.ThrowIfNull(trade);

        if (orderId == Guid.Empty)
            throw new ArgumentException("invalid order id", nameof(orderId));
        if (trade.InstrumentId != Position.InstrumentId)
            throw new InvalidOperationException("trade instrument does not match position instrument");

        var cost = checked(trade.Price * trade.Size);
        var newBalance = checked(CashBalance + cost);

        Position.Sell(trade.Size);
        CashBalance = newBalance;
        
        var operation = new AccountOperation(
            Guid.NewGuid(),
            Id,
            AccountOperationType.TradeSell,
            cost,
            Position.InstrumentId,
            -trade.Size,
            orderId,
            trade.Id,
            trade.ExecutedAt);
        
        _operations.Add(operation);
        return operation;
    }

    internal void SettleSelfTrade(Trade trade, Guid buyOrderId, Guid sellOrderId, decimal reservedCash)
    {
        ArgumentNullException.ThrowIfNull(trade);
        
        if (trade.InstrumentId != Position.InstrumentId)
            throw new InvalidOperationException("trade instrument does not match position instrument");
        
        var cost = checked(trade.Price * trade.Size);

        if (reservedCash < cost || reservedCash > ReservedCash)
            throw new InvalidOperationException("self-trade cash reservation is inconsistent");
        
        Position.Release(trade.Size);
        ReservedCash -= reservedCash;
        
        _operations.Add(new AccountOperation(
            Guid.NewGuid(),
            Id,
            AccountOperationType.TradeBuy,
            -cost,
            Position.InstrumentId,
            trade.Size,
            buyOrderId,
            trade.Id,
            trade.ExecutedAt));

        _operations.Add(new AccountOperation(
            Guid.NewGuid(),
            Id,
            AccountOperationType.TradeSell,
            cost,
            Position.InstrumentId,
            -trade.Size,
            sellOrderId,
            trade.Id,
            trade.ExecutedAt));
    }
    
    public IReadOnlyList<AccountOperation> GetOperations() =>
        _operations.ToArray();
    
    public TradingAccountSnapshot GetSnapshot() =>
        new(Id, CashBalance,  ReservedCash, AvailableCash, Position.GetSnapshot());
}