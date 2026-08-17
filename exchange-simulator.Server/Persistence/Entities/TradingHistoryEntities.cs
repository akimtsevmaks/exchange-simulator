using exchange_simulator.Enums;

namespace exchange_simulator.Server.Persistence.Entities;


internal sealed class OrderEntity
{
    public Guid Id { get; set; }
    public Guid OwnerAccountId { get; set; }
    public Guid InstrumentId { get; set; }
    public OrderType Type { get; set; }
    public OrderSide Side { get; set; }
    public OrderStatus Status { get; set; }
    public decimal? Price { get; set; }
    public long Size { get; set; }
    public long RemainingSize { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}


internal sealed class TradeEntity
{
    public Guid Id { get; set; }
    public Guid InstrumentId { get; set; }
    public Guid BuyOrderId { get; set; }
    public Guid SellOrderId { get; set; }
    public long SequenceNumber { get; set; }
    public decimal Price { get; set; }
    public long Size { get; set; }
    public DateTimeOffset ExecutedAt { get; set; }
}


internal sealed class OrderHistoryEntryEntity
{
    public Guid OrderId { get; set; }
    public long SequenceNumber { get; set; }
    public OrderHistoryEventType EventType { get; set; }
    public long FilledSize { get; set; }
    public long RemainingSize { get; set; }
    public Guid? TradeId { get; set; }
    public DateTimeOffset OccurredAt { get; set; }
}


internal sealed class AccountOperationEntity
{
    public Guid Id { get; set; }
    public Guid AccountId { get; set; }
    public long SequenceNumber { get; set; }
    public AccountOperationType Type { get; set; }
    public decimal CashChange { get; set; }
    public Guid? InstrumentId { get; set; }
    public long InstrumentQuantityChange { get; set; }
    public Guid? OrderId { get; set; }
    public Guid? TradeId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}