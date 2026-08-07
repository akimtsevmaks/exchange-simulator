namespace exchange_simulator.Contracts;

public sealed record OrderCommandResponse(
    OrderResponse Order,
    IReadOnlyList<TradeResponse> Trades
    
    );