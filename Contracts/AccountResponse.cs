namespace exchange_simulator.Contracts;

public sealed record AccountResponse(
    Guid Id,
    decimal CashBalance,
    decimal ReservedCash,
    decimal AvailableCash,
    PositionResponse Position
    
    );