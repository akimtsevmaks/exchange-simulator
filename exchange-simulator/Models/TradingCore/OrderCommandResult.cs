using exchange_simulator.Enums;

namespace exchange_simulator.Models.TradingCore;

public sealed record OrderCommandResult(
    bool IsSuccess,
    OrderRejectionReason? RejectionReason,
    OrderSnapshot? Order,
    IReadOnlyList<Trade> Trades
    
    );