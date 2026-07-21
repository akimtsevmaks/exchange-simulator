using exchange_simulator.Enums;

namespace exchange_simulator.Models.TradingCore;

public record PlaceOrderCommand(
    Guid OwnerId,
    OrderSide Side,
    OrderType Type,
    long Size,
    decimal? Price = null
    
    );