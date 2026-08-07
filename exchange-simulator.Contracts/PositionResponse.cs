namespace exchange_simulator.Contracts;

public sealed record PositionResponse(
    Guid InstrumentId,
    long Quantity,
    long ReservedQuantity,
    long AvailableQuantity,
    decimal AveragePrice
    
    );