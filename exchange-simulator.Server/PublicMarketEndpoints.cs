using exchange_simulator.Contracts;
using exchange_simulator.Services;

namespace exchange_simulator.Server;

internal static class PublicMarketEndpoints
{
    private const int DefaultTradeLimit = 30;
    private const int MaximumTradeLimit = 300;

    public static RouteGroupBuilder MapPublicMarketEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        var marketApi = endpoints.MapGroup("api/market");

        marketApi.MapGet("/instrument", static (LocalMarket market) =>
            TypedResults.Ok(
                MarketResponseMapper.ToInstrumentResponse(market.Instrument)));
        
        marketApi.MapGet("/state", static (LocalMarket market) =>
            TypedResults.Ok(
                MarketResponseMapper.ToMarketStateResponse(market)));
        
        marketApi.MapGet("/order-book", static (LocalMarket market) =>
            TypedResults.Ok(
                MarketResponseMapper.ToOrderBookResponse(market.GetOrderBookSnapshot())));
        
        marketApi.MapGet("/trades", static (LocalMarket market, int? limit) =>
            GetRecentTrades(market, limit));
        
        return marketApi;
    }
    
    private static IResult GetRecentTrades(LocalMarket market, int? requestedLimit)
    {
        var limit = requestedLimit ?? DefaultTradeLimit;

        if (limit < 1 || limit > MaximumTradeLimit)
        {
            return TypedResults.BadRequest(
                new ApiErrorResponse(
                    "InvalidLimit",
                    $"Query parameter 'limit' must be from 1 to {MaximumTradeLimit}."));
        }

        var trades = market.GetTrades().TakeLast(limit)
            .Select(MarketResponseMapper.ToTradeResponse).ToArray();

        return TypedResults.Ok(trades);
    }
}