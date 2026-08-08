using exchange_simulator.Contracts;
using exchange_simulator.Models.TradingCore;
using ContractOrderSide = exchange_simulator.Contracts.OrderSide;
using ContractOrderType = exchange_simulator.Contracts.OrderType;
using DomainOrderSide = exchange_simulator.Enums.OrderSide;
using DomainOrderType = exchange_simulator.Enums.OrderType;

namespace exchange_simulator.Server;

internal static class OrderCommandMapper
{
    public static PlaceOrderCommand ToPlaceOrderCommand(PlaceOrderRequest request, Guid ownerId)
    {
        ArgumentNullException.ThrowIfNull(request);

        var side = request.Side ?? throw new InvalidOperationException(
            "A place-order request must have a side before it is mapped.");
        var type = request.Type ?? throw new InvalidOperationException(
            "A place-order request must have a type before it is mapped.");

        return new PlaceOrderCommand(
            ownerId,
            ToDomainOrderSide(side),
            ToDomainOrderType(type),
            request.Size,
            request.Price);
    }
    
    public static OrderCommandResponse ToOrderCommandResponse(OrderCommandResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        if (!result.IsSuccess || result.RejectionReason is not null || result.Order is null)
            throw new InvalidOperationException("Only a valid successful command result can be mapped to a response.");

        return new OrderCommandResponse(
            AccountResponseMapper.ToOrderResponse(result.Order),
            result.Trades.Select(MarketResponseMapper.ToTradeResponse).ToArray());
    }
    
    
    private static DomainOrderSide ToDomainOrderSide(ContractOrderSide side) =>
        side switch
        {
            ContractOrderSide.Buy => DomainOrderSide.Buy,
            ContractOrderSide.Sell => DomainOrderSide.Sell,
            _ => throw new InvalidOperationException($"Unsupported contract order side '{side}'.")
        };

    private static DomainOrderType ToDomainOrderType(ContractOrderType type) =>
        type switch
        {
            ContractOrderType.Market => DomainOrderType.Market,
            ContractOrderType.Limit => DomainOrderType.Limit,
            _ => throw new InvalidOperationException($"Unsupported contract order type '{type}'.")
        };
}