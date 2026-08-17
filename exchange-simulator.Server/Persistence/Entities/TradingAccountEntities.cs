namespace exchange_simulator.Server.Persistence.Entities;


internal sealed class TradingAccountEntity
{
    public Guid Id { get; set; }
    public Guid WorldId { get; set; }
    public decimal CashBalance { get; set; }
    public decimal ReservedCash { get; set; }
}


internal sealed class PositionEntity
{
    public Guid AccountId { get; set; }
    public Guid InstrumentId { get; set; }
    public long Quantity { get; set; }
    public long ReservedQuantity { get; set; }
    public decimal AveragePrice { get; set; }
}