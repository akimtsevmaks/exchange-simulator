namespace exchange_simulator.Contracts;

public sealed record MarketStateResponse(
    MarketStatus Status,
    decimal ReferencePrice
    
    );