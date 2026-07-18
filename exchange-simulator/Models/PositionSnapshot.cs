namespace exchange_simulator.Models;

public sealed record PositionSnapshot(
    Guid InstrumentId,
    
    long Quantity,
    long ReservedQuantity,
    long AvailableQuantity
    
    );