using exchange_simulator.Enums;

namespace exchange_simulator.Models;

public record AccountOperation(
    Guid Id,
    Guid AccountId,
    AccountOperationType OperationType,
    decimal CashAmount,
    Guid? InstrumentId,
    long InstrumentQuantity,
    DateTimeOffset CreatedAt
    
    );