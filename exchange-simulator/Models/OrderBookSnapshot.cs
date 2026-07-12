namespace exchange_simulator.Models;

public sealed record OrderBookSnapshot(
    Guid InstrumentId,
    IReadOnlyList<OrderBookLevel> Bids,
    IReadOnlyList<OrderBookLevel> Asks
    
    );