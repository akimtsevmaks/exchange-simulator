namespace exchange_simulator.Models.TradingCore;

internal sealed record PlannedTrade(
    Guid BuyerAccountId,
    Guid SellerAccountId,
    decimal Price,
    long Size
    
    );