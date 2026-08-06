namespace exchange_simulator.Contracts;

public sealed record PlaceOrderRequest(
    OrderSide? Side,
    OrderType? Type,
    long Size,
    decimal? Price
    
    );