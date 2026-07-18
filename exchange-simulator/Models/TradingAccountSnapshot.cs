namespace exchange_simulator.Models;

public record TradingAccountSnapshot(
    Guid Id,
    
    decimal CashBalance,
    decimal ReservedCash,
    decimal AvailableCash,
    
    PositionSnapshot Position
    
    );