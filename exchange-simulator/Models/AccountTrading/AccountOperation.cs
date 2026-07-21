using exchange_simulator.Enums;

namespace exchange_simulator.Models.AccountTrading;

public record AccountOperation(
    Guid Id,
    Guid AccountId,
    AccountOperationType Type,
    decimal CashChange,
    Guid? InstrumentId,
    long InstrumentQuantityChange,
    Guid? OrderId,
    Guid? TradeId,
    DateTimeOffset CreatedAt
    
    );