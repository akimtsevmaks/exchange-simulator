namespace exchange_simulator.Models.TradingCore;

public sealed record OrderBookLevel(
    decimal Price,
    long Size
    
    );