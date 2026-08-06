namespace exchange_simulator.Contracts;

public sealed record OrderBookResponse(
    Guid InstrumentId,
    IReadOnlyList<OrderBookLevelResponse> Bids,
    IReadOnlyList<OrderBookLevelResponse> Asks
    
    );