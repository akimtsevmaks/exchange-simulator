namespace exchange_simulator.Bots;

public sealed record NoiseBotOptions(
    int RandomSeed,
    decimal PriceOffset,
    int MaxOrderLots,
    int MaxActiveOrders
    
    );