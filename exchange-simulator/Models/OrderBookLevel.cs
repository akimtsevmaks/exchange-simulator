namespace exchange_simulator.Models;

public sealed record OrderBookLevel(
    decimal Price,
    long Size
    
    );