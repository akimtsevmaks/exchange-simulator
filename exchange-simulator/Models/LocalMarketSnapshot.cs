using exchange_simulator.Models.AccountTrading;
using exchange_simulator.Models.TradingCore;

namespace exchange_simulator.Models;

public sealed record LocalMarketSnapshot(
    OrderBookSnapshot OrderBook,
    decimal ReferencePrice,
    IReadOnlyList<Trade> Trades,
    TradingAccountSnapshot ManualAccount,
    IReadOnlyList<OrderSnapshot> ManualActiveOrders
    
    );