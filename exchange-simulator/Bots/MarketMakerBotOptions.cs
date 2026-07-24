namespace exchange_simulator.Bots;

public record MarketMakerBotOptions(
    decimal QuoteOffset,
    long OrderSize
    
    );