namespace exchange_simulator.Contracts;

public sealed record OrderBookLevelResponse(
    decimal Price,
    string Size
    
    );