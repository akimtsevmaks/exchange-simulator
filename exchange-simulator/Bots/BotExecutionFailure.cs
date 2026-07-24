namespace exchange_simulator.Bots;

public sealed record BotExecutionFailure(
    Guid AccountId,
    string Operation,
    string ExceptionType,
    string Message,
    DateTimeOffset OccurredAt
    
    );