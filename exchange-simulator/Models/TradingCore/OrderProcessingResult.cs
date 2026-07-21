namespace exchange_simulator.Models.TradingCore;

public sealed record OrderProcessingResult(
    IReadOnlyList<Trade> Trades,
    long RemainingSize,
    bool IsResting
    
    );