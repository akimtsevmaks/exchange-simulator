using exchange_simulator.Contracts;
using exchange_simulator.Enums;
using exchange_simulator.Models.TradingCore;
using exchange_simulator.Services;

namespace exchange_simulator.Server;


internal static class TradingCommandEndpoints
{
    public static RouteGroupBuilder MapTradingCommandEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var ordersApi = endpoints.MapGroup("/api/orders");

        ordersApi.MapPost("", static (PlaceOrderRequest request, LocalMarket market, TestParticipant participant) =>
            PlaceOrder(request, market, participant));

        ordersApi.MapDelete("/{orderId}", static (Guid orderId, LocalMarket market, TestParticipant participant) =>
            CancelOrder(orderId, market, participant));

        return ordersApi;
    }
    
    
    private static IResult PlaceOrder(PlaceOrderRequest request, LocalMarket market, TestParticipant participant)
    {
        if (request.Side is null)
            return ToRejectionResult(OrderRejectionReason.InvalidOrderSide);

        if (request.Type is null)
            return ToRejectionResult(OrderRejectionReason.InvalidOrderType);

        var command = OrderCommandMapper.ToPlaceOrderCommand(request, participant.AccountId);

        return ToHttpResult(market.PlaceOrder(command));
    }

    private static IResult CancelOrder(Guid orderId, LocalMarket market, TestParticipant participant)
    {
        if (!market.TryGetOrder(participant.AccountId, orderId, out _))
            return ToRejectionResult(OrderRejectionReason.OrderNotFound);

        return ToHttpResult(market.CancelOrder(orderId));
    }
    
    private static IResult ToHttpResult(OrderCommandResult result)
    {
        if (result.IsSuccess)
            return TypedResults.Ok(
                OrderCommandMapper.ToOrderCommandResponse(result));

        if (result.RejectionReason is not { } reason || result.Order is not null || result.Trades.Count != 0)
            throw new InvalidOperationException("The market returned an invalid rejected command result");

        return ToRejectionResult(reason);
    }

    private static IResult ToRejectionResult(OrderRejectionReason reason)
    {
        var error = new ApiErrorResponse(
            Code: reason.ToString(),
            Message: GetRejectionMessage(reason));

        return reason switch
        {
            OrderRejectionReason.InvalidOrderType or
            OrderRejectionReason.InvalidOrderSide or
            OrderRejectionReason.InvalidSize or
            OrderRejectionReason.OrderValueTooLarge or
            OrderRejectionReason.QuantityNotMultipleOfLotSize or
            OrderRejectionReason.InvalidPrice or
            OrderRejectionReason.PriceRequiredForLimitOrder or
            OrderRejectionReason.PriceNotAllowedForMarketOrder =>
                TypedResults.BadRequest(error),

            OrderRejectionReason.OrderNotFound =>
                TypedResults.NotFound(error),

            OrderRejectionReason.InsufficientAvailableCash or
            OrderRejectionReason.InsufficientAvailablePosition or
            OrderRejectionReason.OrderNotActive =>
                TypedResults.Conflict(error),

            OrderRejectionReason.InvalidOwnerId or
            OrderRejectionReason.AccountNotFound =>
                throw new InvalidOperationException(
                    $"The server-created command was rejected with '{reason}'"),

            _ => throw new InvalidOperationException(
                $"Unsupported order rejection reason '{reason}'")
        };
    }

    private static string GetRejectionMessage(OrderRejectionReason reason) =>
        reason switch
        {
            OrderRejectionReason.InvalidOrderType =>
                "Order type is invalid",
            OrderRejectionReason.InvalidOrderSide =>
                "Order side is invalid",
            OrderRejectionReason.InvalidSize =>
                "Order size must be greater than zero",
            OrderRejectionReason.OrderValueTooLarge =>
                "Order value is too large",
            OrderRejectionReason.QuantityNotMultipleOfLotSize =>
                "Order size must be a multiple of the instrument lot size",
            OrderRejectionReason.InvalidPrice =>
                "Order price must be greater than zero",
            OrderRejectionReason.PriceRequiredForLimitOrder =>
                "A limit order requires a price",
            OrderRejectionReason.PriceNotAllowedForMarketOrder =>
                "A market order must not specify a price",
            OrderRejectionReason.InsufficientAvailableCash =>
                "The account does not have enough available cash",
            OrderRejectionReason.InsufficientAvailablePosition =>
                "The account does not have enough available position",
            OrderRejectionReason.OrderNotFound =>
                "Order was not found",
            OrderRejectionReason.OrderNotActive =>
                "Order is not active",
            OrderRejectionReason.InvalidOwnerId or
            OrderRejectionReason.AccountNotFound =>
                throw new InvalidOperationException(
                    $"The server-created command was rejected with '{reason}'"),
            _ => throw new InvalidOperationException(
                $"Unsupported order rejection reason '{reason}'")
        };
}