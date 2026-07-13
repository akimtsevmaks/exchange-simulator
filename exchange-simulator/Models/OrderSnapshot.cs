using exchange_simulator.Enums;

namespace exchange_simulator.Models;

public record OrderSnapshot(
    Guid Id,
    Guid OwnerId,
    Guid InstrumentId,
    
    OrderType OrderType,
    OrderSide OrderSide,
    OrderStatus OrderStatus,
    
    decimal? Price,
    decimal Size,
    decimal RemainingSize,
    decimal FilledSize,
    
    DateTimeOffset CreatedAt
    
    );