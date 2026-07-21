namespace exchange_simulator.Models.AccountTrading;

public record TradingAccountSnapshot(
    Guid Id,
    
    decimal CashBalance,
    decimal ReservedCash,
    decimal AvailableCash,
    
    PositionSnapshot Position
    
    );