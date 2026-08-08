using exchange_simulator.Services;

namespace exchange_simulator.Server;


internal static class PersonalAccountEndpoints
{
    public static RouteGroupBuilder MapPersonalAccountEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        var accountApi = endpoints.MapGroup("/api/account");

        accountApi.MapGet("", static (LocalMarket market, TestParticipant participant) =>
            GetAccount(market, participant));

        accountApi.MapGet("/active-orders", static (LocalMarket market, TestParticipant participant) =>
            GetActiveOrders(market, participant));

        return accountApi;
    }
    
    
    private static IResult GetAccount(LocalMarket market, TestParticipant participant)
    {
        if (!market.TryGetAccount(participant.AccountId, out var account))
        {
            throw new InvalidOperationException(
                "The test participant account is not registered in the local market.");
        }

        return TypedResults.Ok(
            AccountResponseMapper.ToAccountResponse(account!));
    }

    private static IResult GetActiveOrders(LocalMarket market, TestParticipant participant)
    {
        var orders = market.GetActiveOrders(participant.AccountId)
            .Select(AccountResponseMapper.ToOrderResponse).ToArray();

        return TypedResults.Ok(orders);
    }
}