using exchange_simulator.Enums;

namespace exchange_simulator.Models;

public class TradingAccount
{
    private readonly List<AccountOperation> _operations = [];
    
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
            AccountOperationType.InitialInstrumentGranted,
            0,
            Position.InstrumentId,
            quantity,
            DateTimeOffset.UtcNow);
        
        Position.GrantInitialQuantity(quantity);
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
    
    public IReadOnlyList<AccountOperation> GetOperations() =>
        _operations.ToArray();
    
    public TradingAccountSnapshot GetSnapshot() =>
        new(Id, CashBalance,  ReservedCash, AvailableCash, Position.GetSnapshot());
}