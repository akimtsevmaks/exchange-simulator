namespace exchange_simulator.Models;

public sealed record OrderProcessingResult(
    IReadOnlyList<Trade> Trades,
    long RemainingSize,
    bool IsResting
    
    );