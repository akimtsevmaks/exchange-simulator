using exchange_simulator.Models.TradingCore;

namespace exchange_simulator.Models.AccountTrading;


public sealed record TradingAccountRestoreState(
    TradingAccountSnapshot Snapshot,
    IReadOnlyList<AccountOperation> Operations
    
    );
    
    
public sealed record AccountTradingRestoreState(
    IReadOnlyList<TradingAccountRestoreState> Accounts,
    IReadOnlyList<OrderSnapshot> Orders,
    IReadOnlyList<Trade> Trades,
    IReadOnlyList<OrderHistoryEntry> OrderHistory
    
    );