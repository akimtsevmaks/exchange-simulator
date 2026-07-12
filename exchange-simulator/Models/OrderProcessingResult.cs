namespace exchange_simulator.Models;

public sealed record OrderProcessingResult(
    IReadOnlyList<Trade> Trades,
    decimal RemainingSize,
    bool IsResting
    
    );