using exchange_simulator.Enums;

namespace exchange_simulator.Models.AccountTrading;

public record OrderHistoryEntry(
    Guid OrderId,
    OrderHistoryEventType EventType,
    long FilledSize,
    long RemainingSize,
    Guid? TradeId,
    DateTimeOffset OccurredAt
    
    );