using exchange_simulator.Enums;

namespace exchange_simulator.Models;

public record PlaceOrderCommand(
    Guid OwnerId,
    OrderSide Side,
    OrderType Type,
    long Size,
    decimal? Price
    
    );