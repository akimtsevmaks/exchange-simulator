namespace exchange_simulator.Contracts;

public sealed record TradeResponse(
    Guid Id,
    Guid InstrumentId,
    
    decimal Price,
    long Size,
    
    DateTimeOffset ExecutedAt
    
    );