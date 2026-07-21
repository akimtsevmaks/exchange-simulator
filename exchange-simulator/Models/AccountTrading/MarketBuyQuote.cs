namespace exchange_simulator.Models.AccountTrading;

public sealed record MarketBuyQuote(
    long RequestedSize,
    long ExecutableSize,
    long UnfilledSize,
    decimal Cost
    
    );