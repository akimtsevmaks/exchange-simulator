namespace exchange_simulator.Enums;

public enum OrderRejectionReason
{
    InvalidOwnerId,
    InvalidOrderType,
    InvalidOrderSide,
    InvalidSize,
    OrderValueTooLarge,
    QuantityNotMultipleOfLotSize,
    InvalidPrice,
    PriceRequiredForLimitOrder,
    PriceNotAllowedForMarketOrder,
    AccountNotFound,
    InsufficientAvailableCash,
    InsufficientAvailablePosition,
    OrderNotFound,
    OrderNotActive
}