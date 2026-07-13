namespace exchange_simulator.Enums;

public enum OrderRejectionReason
{
    InvalidOwnerId,
    InvalidOrderType,
    InvalidOrderSide,
    InvalidSize,
    InvalidPrice,
    PriceRequiredForLimitOrder,
    PriceNotAllowedForMarketOrder,
    OrderNotFound,
    OrderNotActive
}