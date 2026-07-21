namespace exchange_simulator.Models.AccountTrading;

public sealed record PositionSnapshot(
    Guid InstrumentId,
    
    long Quantity,
    long ReservedQuantity,
    long AvailableQuantity,
    decimal AveragePrice
    
    );