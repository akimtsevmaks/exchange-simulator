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
    
    public PositionSnapshot GetSnapshot() =>
        new(InstrumentId, Quantity, ReservedQuantity, AvailableQuantity);
}