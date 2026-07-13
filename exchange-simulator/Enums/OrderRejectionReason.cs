namespace exchange_simulator.Enums;

public enum OrderRejectionReason
{
    InvalidOwnerId,
    InvalidOrderType,
    InvalidOrderSide,
    InvalidSize,
    QuantityNotMultipleOfLotSize,
    InvalidPrice,
    PriceRequiredForLimitOrder,
    PriceNotAllowedForMarketOrder,
    OrderNotFound,
    OrderNotActive
}