namespace exchange_simulator.Contracts;

public sealed record OrderResponse(
    Guid Id,
    Guid InstrumentId,
    
    OrderType Type,
    OrderSide Side,
    OrderStatus Status,
    
    decimal? Price,
    long Size,
    long RemainingSize,
    long FilledSize,
    
    DateTimeOffset CreatedAt
    
    );