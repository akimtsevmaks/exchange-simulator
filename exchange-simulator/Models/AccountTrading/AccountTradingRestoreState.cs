namespace exchange_simulator.Models.AccountTrading;


public sealed record TradingAccountRestoreState(
    TradingAccountSnapshot Snapshot,
    IReadOnlyList<AccountOperation> Operations
    
    );