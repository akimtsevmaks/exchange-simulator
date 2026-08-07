namespace exchange_simulator.Contracts;

public sealed record InstrumentResponse(
    Guid Id,
    string Ticker,
    string Name,
    long LotSize,
    decimal InitialPrice
    
    );