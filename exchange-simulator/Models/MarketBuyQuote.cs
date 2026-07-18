namespace exchange_simulator.Models;

public sealed record MarketBuyQuote(
    long RequestedSize,
    long ExecutableSize,
    long UnfilledSize,
    decimal Cost
    
    );