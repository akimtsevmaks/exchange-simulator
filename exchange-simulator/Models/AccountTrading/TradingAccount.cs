using exchange_simulator.Enums;
using exchange_simulator.Models.TradingCore;

namespace exchange_simulator.Models.AccountTrading;

public class TradingAccount
{
    private readonly List<AccountOperation> _operations;
    private readonly decimal _initialInstrumentPrice;
    
    public Guid Id { get; }
    
    public decimal CashBalance { get; private set; }
    public decimal ReservedCash { get; private set; }
    public decimal AvailableCash =>  CashBalance - ReservedCash;
    
    public Position Position { get; }
    
    public TradingAccount(Guid id, Instrument instrument) 
        : this(id, instrument, CreateEmptyPosition(instrument), 0, 0, []) { }

    private TradingAccount(
        Guid id,
        Instrument instrument,
        Position position,
        decimal cashBalance,
        decimal reservedCash,
        IReadOnlyList<AccountOperation> operations)
    {
        if (id == Guid.Empty)
            throw new ArgumentException("invalid account ID", nameof(id));
        
        ArgumentNullException.ThrowIfNull(instrument);
        ArgumentNullException.ThrowIfNull(position);
        ArgumentNullException.ThrowIfNull(operations);
        
        ArgumentOutOfRangeException.ThrowIfNegative(cashBalance);
        ArgumentOutOfRangeException.ThrowIfNegative(reservedCash);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(reservedCash, cashBalance);
        
        if (position.InstrumentId != instrument.Id)
            throw new ArgumentException("instrument position does not match position instrument", nameof(position));
        
        
        Id = id;
        _initialInstrumentPrice = instrument.InitialPrice;
        CashBalance = cashBalance;
        ReservedCash = reservedCash;
        Position = position;
        _operations = [..operations];
    }

    internal static TradingAccount Restore(TradingAccountRestoreState state, Instrument instrument)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(instrument);
        ArgumentNullException.ThrowIfNull(state.Snapshot);
        ArgumentNullException.ThrowIfNull(state.Snapshot.Position);
        ArgumentNullException.ThrowIfNull(state.Operations);
        
        var snapshot = state.Snapshot;
        
        ArgumentOutOfRangeException.ThrowIfNegative(snapshot.CashBalance);
        ArgumentOutOfRangeException.ThrowIfNegative(snapshot.ReservedCash);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(snapshot.ReservedCash, snapshot.CashBalance);
        
        if (snapshot.AvailableCash != snapshot.CashBalance - snapshot.ReservedCash)
            throw new ArgumentException("available cash does not match balance and reserve", nameof(state));
        if (snapshot.Position.InstrumentId != instrument.Id)
            throw new ArgumentException("restored instrument position does not match position instrument", nameof(state));
        
        var operations = state.Operations.ToArray();
        var operationIds = new HashSet<Guid>();

 
        foreach (var operation in operations)
        {
            if (operation is null)
                throw new ArgumentException("account operations cannot contain null", nameof(state));
            if (operation.Id == Guid.Empty)
                throw new ArgumentException("account operation ID cannot be empty", nameof(state));
            if (!operationIds.Add(operation.Id))
                throw new ArgumentException($"duplicate account operation {operation.Id}", nameof(state));
            if (operation.AccountId != snapshot.Id)
                throw new ArgumentException("account operation belongs to another account", nameof(state));
            if (!Enum.IsDefined(operation.Type))
                throw new ArgumentException("account operation type is invalid", nameof(state));
            if (operation.InstrumentId == Guid.Empty || operation.OrderId == Guid.Empty || operation.TradeId == Guid.Empty)
                throw new ArgumentException("account operation contains an empty reference", nameof(state));
            if (operation.InstrumentId.HasValue && operation.InstrumentId.Value != instrument.Id)
                throw new ArgumentException("account operation references another instrument", nameof(state));
        }
        
        return new TradingAccount(
            snapshot.Id,
            instrument,
            Position.Restore(snapshot.Position),
            snapshot.CashBalance,
            snapshot.ReservedCash,
            operations);
    }

    private static Position CreateEmptyPosition(Instrument instrument)
    {
        ArgumentNullException.ThrowIfNull(instrument);
        return new Position(instrument.Id);
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