namespace exchange_simulator.Models;

public sealed record Trade(
    Guid Id,
    Guid InstrumentId,
    Guid BuyOrderId,
    Guid SellOrderId,
    
    decimal Price,
    long Size,
    DateTimeOffset ExecutedAt
    
    );