using System.Numerics;

namespace exchange_simulator.Models.TradingCore;

public sealed record OrderBookLevel(
    decimal Price,
    BigInteger Size
    
    );