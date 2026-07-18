namespace exchange_simulator.Models;

public sealed class Position
{
    public Guid InstrumentId { get; }
    
    public long Quantity { get; private set; }
    public long ReservedQuantity { get; private set; }
    public long AvailableQuantity => Quantity - ReservedQuantity;

    internal Position(Guid instrumentId)
    {
        if (instrumentId == Guid.Empty)
            throw new ArgumentException("invalid instrument ID", nameof(instrumentId));
        
        InstrumentId = instrumentId;
    }

    internal void GrantInitialQuantity(long quantity)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(quantity);
        Quantity = checked(Quantity + quantity);
    }

    internal bool TryReserve(long quantity)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(quantity);

        if (quantity > AvailableQuantity)
            return false;
        
        ReservedQuantity += quantity;
        return true;
    }

    internal void Release(long quantity)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(quantity);
        
        if (quantity > ReservedQuantity)
            throw new InvalidOperationException("Can't release more than reserved quantity");
        
        ReservedQuantity -= quantity;
    }
    
    public PositionSnapshot GetSnapshot() =>
        new(InstrumentId, Quantity, ReservedQuantity, AvailableQuantity);
}