using exchange_simulator.Contracts;
using exchange_simulator.Models.AccountTrading;
using exchange_simulator.Models.TradingCore;
using ContractOrderSide = exchange_simulator.Contracts.OrderSide;
using ContractOrderStatus = exchange_simulator.Contracts.OrderStatus;
using ContractOrderType = exchange_simulator.Contracts.OrderType;
using DomainOrderSide = exchange_simulator.Enums.OrderSide;
using DomainOrderStatus = exchange_simulator.Enums.OrderStatus;
using DomainOrderType = exchange_simulator.Enums.OrderType;

namespace exchange_simulator.Server;


internal sealed class AccountResponseMapper
{
    public static AccountResponse ToAccountResponse(TradingAccountSnapshot account) =>
        new(
            account.Id,
            account.CashBalance,
            account.ReservedCash,
            account.AvailableCash,
            ToPositionResponse(account.Position));
    
    public static OrderResponse ToOrderResponse(OrderSnapshot order) =>
        new(
            order.Id,
            order.InstrumentId,
            ToOrderType(order.OrderType),
            ToOrderSide(order.OrderSide),
            ToOrderStatus(order.OrderStatus),
            order.Price,
            order.Size,
            order.RemainingSize,
            order.FilledSize,
            order.CreatedAt);
    
    
    private static PositionResponse ToPositionResponse(PositionSnapshot position) =>
        new(
            position.InstrumentId,
            position.Quantity,
            position.ReservedQuantity,
            position.AvailableQuantity,
            position.AveragePrice);
    
    private static ContractOrderType ToOrderType(DomainOrderType type) =>
        type switch
        {
            DomainOrderType.Market => ContractOrderType.Market,
            DomainOrderType.Limit => ContractOrderType.Limit,
            _ => throw new InvalidOperationException($"Unsupported order type '{type}'.")
        };
    
    private static ContractOrderSide ToOrderSide(DomainOrderSide side) =>
        side switch
        {
            DomainOrderSide.Buy => ContractOrderSide.Buy,
            DomainOrderSide.Sell => ContractOrderSide.Sell,
            _ => throw new InvalidOperationException($"Unsupported order side '{side}'.")
        };
    
    private static ContractOrderStatus ToOrderStatus(DomainOrderStatus status) =>
        status switch
        {
            DomainOrderStatus.Active => ContractOrderStatus.Active,
            DomainOrderStatus.Filled => ContractOrderStatus.Filled,
            DomainOrderStatus.Cancelled => ContractOrderStatus.Cancelled,
            DomainOrderStatus.Created => throw new InvalidOperationException(
                "An order in the Created status cannot be returned by the server."),
            _ => throw new InvalidOperationException(
                $"Unsupported order status '{status}'.")
        };
}