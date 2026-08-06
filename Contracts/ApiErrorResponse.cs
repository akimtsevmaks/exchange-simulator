namespace exchange_simulator.Contracts;

public sealed record ApiErrorResponse(
    string Code,
    string Message
    
    );