namespace exchange_simulator.Models.TradingCore;

public sealed record OrderBookSnapshot(
    Guid InstrumentId,
    IReadOnlyList<OrderBookLevel> Bids,
    IReadOnlyList<OrderBookLevel> Asks
    
    );