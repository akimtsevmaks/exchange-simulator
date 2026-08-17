namespace exchange_simulator.Server.Persistence.Entities;


internal sealed class TradingWorldEntity
{
    public Guid Id { get; set; }
    public decimal InitialCashPerAccount { get; set; }
    public long InitialInstrumentsPerAccount { get; set; }
}


internal sealed class InstrumentEntity
{
    public Guid Id { get; set; }
    public Guid WorldId { get; set; }
    public string Ticker { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public long LotSize { get; set; }
    public decimal InitialPrice { get; set; }
}


internal enum BotAccountKind
{
    MarketMaker,
    NoiseBot
}


internal sealed class BotAccountEntity
{
    public Guid WorldId { get; set; }
    public BotAccountKind Kind { get; set; }
    public Guid AccountId { get; set; }
}