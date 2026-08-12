using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using exchange_simulator.Contracts;
using exchange_simulator.Models.TradingCore;
using exchange_simulator.Services;
using Microsoft.Extensions.DependencyInjection;
using ContractOrderSide = exchange_simulator.Contracts.OrderSide;
using ContractOrderStatus = exchange_simulator.Contracts.OrderStatus;
using ContractOrderType = exchange_simulator.Contracts.OrderType;
using DomainOrderSide = exchange_simulator.Enums.OrderSide;
using DomainOrderStatus = exchange_simulator.Enums.OrderStatus;
using DomainOrderType = exchange_simulator.Enums.OrderType;
using LocalMarketStatus = exchange_simulator.Enums.LocalMarketStatus;
using TestMarket = exchange_simulator.Services.LocalMarket;

namespace exchange_simulator.Tests.Server;

public sealed class ServerApiIntegrationTests
{
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();
    
    [Fact]
    public async Task Server_ShouldStartMarketAndBots_WithoutHttpClient()
    {
        // Arrange
        using var factory = ExchangeServerFactory.CreateWithObservableBot(
            TimeSpan.FromMilliseconds(20));

        // Act
        var market = factory.Services.GetRequiredService<TestMarket>();
        await WaitUntilAsync(() => factory.ObservableBot!.StepCount > 0);

        // Assert
        Assert.Same(factory.Market, market);
        Assert.Equal(LocalMarketStatus.Running, market.Status);
        Assert.NotEmpty(market.GetOrderBookSnapshot().Bids);
    }
    
    [Fact]
    public async Task TwoHttpClients_ShouldObserveTheSameOrderBook()
    {
        // Arrange
        using var factory = new ExchangeServerFactory();
        using var firstClient = factory.CreateClient();
        using var secondClient = factory.CreateClient();

        await PlaceOrderAsync(
            firstClient,
            new PlaceOrderRequest(
                ContractOrderSide.Buy,
                ContractOrderType.Limit,
                Size: 10,
                Price: 90m));

        // Act
        var firstBook = await GetJsonAsync<OrderBookResponse>(
            firstClient,
            "api/market/order-book");
        var secondBook = await GetJsonAsync<OrderBookResponse>(
            secondClient,
            "api/market/order-book");
        var firstAccount = await GetJsonAsync<AccountResponse>(
            firstClient,
            "api/account");
        var secondAccount = await GetJsonAsync<AccountResponse>(
            secondClient,
            "api/account");

        // Assert
        Assert.Equal(firstBook.InstrumentId, secondBook.InstrumentId);
        Assert.Equal(firstBook.Bids.ToArray(), secondBook.Bids.ToArray());
        Assert.Equal(firstBook.Asks.ToArray(), secondBook.Asks.ToArray());
        Assert.Equal("10", Assert.Single(secondBook.Bids).Size);
        Assert.Equal(firstAccount, secondAccount);
    }
    
    [Fact]
    public async Task OrderPlacedByOneHttpClient_ShouldBeVisibleToAnother()
    {
        // Arrange
        using var factory = new ExchangeServerFactory();
        using var firstClient = factory.CreateClient();
        using var secondClient = factory.CreateClient();

        // Act
        var placement = await PlaceOrderAsync(
            firstClient,
            new PlaceOrderRequest(
                ContractOrderSide.Sell,
                ContractOrderType.Limit,
                Size: 7,
                Price: 110m));
        
        var activeOrders = await GetJsonAsync<OrderResponse[]>(
            secondClient,
            "api/account/active-orders");

        // Assert
        var visibleOrder = Assert.Single(activeOrders, order => order.Id == placement.Order.Id);
        Assert.Equal(ContractOrderStatus.Active, visibleOrder.Status);
        Assert.Equal(7, visibleOrder.RemainingSize);
    }
    
    [Fact]
    public async Task CrossingOrders_ShouldCreateOneTradeVisibleToBothClients()
    {
        // Arrange
        using var factory = new ExchangeServerFactory();
        using var firstClient = factory.CreateClient();
        using var secondClient = factory.CreateClient();

        var restingOrder = await PlaceOrderAsync(
            firstClient,
            new PlaceOrderRequest(
                ContractOrderSide.Sell,
                ContractOrderType.Limit,
                Size: 10,
                Price: 100m));

        // Act
        var aggressiveOrder = await PlaceOrderAsync(
            secondClient,
            new PlaceOrderRequest(
                ContractOrderSide.Buy,
                ContractOrderType.Market,
                Size: 4,
                Price: null));
        
        var firstTrades = await GetJsonAsync<TradeResponse[]>(
            firstClient,
            "api/market/trades?limit=10");
        var secondTrades = await GetJsonAsync<TradeResponse[]>(
            secondClient,
            "api/market/trades?limit=10");
        var activeOrders = await GetJsonAsync<OrderResponse[]>(
            secondClient,
            "api/account/active-orders");

        // Assert
        var commandTrade = Assert.Single(aggressiveOrder.Trades);
        Assert.Equal(100m, commandTrade.Price);
        Assert.Equal(4, commandTrade.Size);
        Assert.Equal(commandTrade.Id, Assert.Single(firstTrades).Id);
        Assert.Equal(
            firstTrades.Select(trade => trade.Id),
            secondTrades.Select(trade => trade.Id));

        var remainingOrder = Assert.Single(activeOrders, order => order.Id == restingOrder.Order.Id);
        Assert.Equal(6, remainingOrder.RemainingSize);
    }
    
    [Fact]
    public async Task DisconnectingClients_ShouldNotStopBots()
    {
        // Arrange
        using var factory = ExchangeServerFactory.CreateWithObservableBot(TimeSpan.FromMilliseconds(30));

        using (var client = factory.CreateClient())
        {
            var state = await GetJsonAsync<MarketStateResponse>(
                client,
                "api/market/state");
            Assert.Equal(MarketStatus.Running, state.Status);
        }

        var stepCountAfterDisconnect = factory.ObservableBot!.StepCount;

        // Act
        await WaitUntilAsync(
            () => factory.ObservableBot.StepCount > stepCountAfterDisconnect);

        // Assert
        Assert.Equal(LocalMarketStatus.Running, factory.Market.Status);
    }
    
    [Fact]
    public async Task NewClient_ShouldObserveStateChangedWhileNoClientsWereConnected()
    {
        // Arrange
        using var factory = ExchangeServerFactory.CreateWithObservableBot(TimeSpan.FromMilliseconds(30));

        OrderBookResponse firstBook;
        int observedStepCount;

        using (var firstClient = factory.CreateClient())
        {
            firstBook = await GetJsonAsync<OrderBookResponse>(
                firstClient,
                "api/market/order-book");
            observedStepCount = factory.ObservableBot!.StepCount;
        }

        await WaitUntilAsync(
            () => factory.ObservableBot!.StepCount > observedStepCount);

        // Act
        using var secondClient = factory.CreateClient();
        var secondBook = await GetJsonAsync<OrderBookResponse>(
            secondClient,
            "api/market/order-book");

        // Assert
        Assert.True(secondBook.Bids.Count > firstBook.Bids.Count);
    }
    
    [Fact]
    public async Task InvalidCommand_ShouldReturnBadRequestWithStableDomainCode()
    {
        // Arrange
        using var factory = new ExchangeServerFactory();
        using var client = factory.CreateClient();
        var request = new PlaceOrderRequest(
            ContractOrderSide.Buy,
            ContractOrderType.Limit,
            Size: 0,
            Price: 100m);

        // Act
        using var response = await client.PostAsJsonAsync(
            "api/orders",
            request,
            JsonOptions);
        var error = await ReadJsonAsync<ApiErrorResponse>(response);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("InvalidSize", error.Code);
    }
    
    [Fact]
    public async Task MalformedJson_ShouldReturnUnifiedInvalidRequestError()
    {
        // Arrange
        using var factory = new ExchangeServerFactory();
        using var client = factory.CreateClient();
        using var content = new StringContent(
            "{", Encoding.UTF8,
            "application/json");

        // Act
        using var response = await client.PostAsync("api/orders", content);
        var error = await ReadJsonAsync<ApiErrorResponse>(response);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("InvalidRequest", error.Code);
        Assert.False(string.IsNullOrWhiteSpace(error.Message));
    }
    
    [Fact]
    public async Task RepeatedReads_ShouldNotChangeMarketOrAccountState()
    {
        // Arrange
        using var factory = new ExchangeServerFactory();
        using var client = factory.CreateClient();

        await PlaceOrderAsync(
            client,
            new PlaceOrderRequest(
                ContractOrderSide.Buy,
                ContractOrderType.Limit,
                Size: 3,
                Price: 90m));

        string[] endpoints =
        [
            "api/market/instrument",
            "api/market/state",
            "api/market/order-book",
            "api/market/trades?limit=10",
            "api/account",
            "api/account/active-orders"
        ];

        // Act & Assert
        foreach (var endpoint in endpoints)
        {
            var firstResponse = await client.GetStringAsync(endpoint);
            var secondResponse = await client.GetStringAsync(endpoint);

            Assert.Equal(firstResponse, secondResponse);
        }
    }

    [Fact]
    public async Task ForeignAndUnknownOrders_ShouldHaveTheSameCancellationResult()
    {
        // Arrange
        using var factory = new ExchangeServerFactory();
        using var client = factory.CreateClient();

        var foreignPlacement = factory.Market.PlaceOrder(
            new PlaceOrderCommand(
                factory.Market.MarketMakerAccountId,
                DomainOrderSide.Sell,
                DomainOrderType.Limit,
                Size: 5,
                Price: 120m));
        var foreignOrder = Assert.IsType<OrderSnapshot>(foreignPlacement.Order);

        // Act
        using var foreignResponse = await client.DeleteAsync(
            $"api/orders/{foreignOrder.Id:D}");
        using var unknownResponse = await client.DeleteAsync(
            $"api/orders/{Guid.NewGuid():D}");
        var foreignError = await ReadJsonAsync<ApiErrorResponse>(foreignResponse);
        var unknownError = await ReadJsonAsync<ApiErrorResponse>(unknownResponse);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, foreignResponse.StatusCode);
        Assert.Equal(foreignResponse.StatusCode, unknownResponse.StatusCode);
        Assert.Equal(foreignError, unknownError);
        Assert.Equal("OrderNotFound", foreignError.Code);

        Assert.True(factory.Market.TryGetOrder(factory.Market.MarketMakerAccountId, foreignOrder.Id,
            out var storedOrder));
        Assert.Equal(DomainOrderStatus.Active, storedOrder!.OrderStatus);
    }
    
    [Fact]
    public async Task CancellingInactiveOrder_ShouldReturnConflict()
    {
        // Arrange
        using var factory = new ExchangeServerFactory();
        using var client = factory.CreateClient();
        var placement = await PlaceOrderAsync(
            client,
            new PlaceOrderRequest(
                ContractOrderSide.Buy,
                ContractOrderType.Limit,
                Size: 2,
                Price: 80m));

        using var successfulCancellation = await client.DeleteAsync(
            $"api/orders/{placement.Order.Id:D}");
        Assert.Equal(HttpStatusCode.OK, successfulCancellation.StatusCode);

        // Act
        using var repeatedCancellation = await client.DeleteAsync(
            $"api/orders/{placement.Order.Id:D}");
        var error = await ReadJsonAsync<ApiErrorResponse>(repeatedCancellation);

        // Assert
        Assert.Equal(HttpStatusCode.Conflict, repeatedCancellation.StatusCode);
        Assert.Equal("OrderNotActive", error.Code);
    }
    
    [Fact]
    public async Task RecentTrades_ShouldPreserveOrderAndApplyLimit()
    {
        // Arrange
        using var factory = new ExchangeServerFactory();
        using var client = factory.CreateClient();

        for (var index = 0; index < 5; index++)
        {
            await PlaceOrderAsync(
                client,
                new PlaceOrderRequest(
                    ContractOrderSide.Sell,
                    ContractOrderType.Limit,
                    Size: 1,
                    Price: 100m + index));
            await PlaceOrderAsync(
                client,
                new PlaceOrderRequest(
                    ContractOrderSide.Buy,
                    ContractOrderType.Market,
                    Size: 1,
                    Price: null));
        }

        var allTrades = await GetJsonAsync<TradeResponse[]>(
            client,
            "api/market/trades?limit=100");

        // Act
        var limitedTrades = await GetJsonAsync<TradeResponse[]>(
            client,
            "api/market/trades?limit=3");

        // Assert
        Assert.Equal(3, limitedTrades.Length);
        Assert.Equal(
            allTrades.TakeLast(3).Select(trade => trade.Id),
            limitedTrades.Select(trade => trade.Id));
        Assert.Equal(
            [ 102m, 103m, 104m ],
            limitedTrades.Select(trade => trade.Price).ToArray());
    }


    private static async Task<OrderCommandResponse> PlaceOrderAsync(HttpClient client, PlaceOrderRequest request)
    {
        using var response = await client.PostAsJsonAsync("api/orders", request, JsonOptions);

        response.EnsureSuccessStatusCode();
        return await ReadJsonAsync<OrderCommandResponse>(response);
    }
    
    private static async Task<T> GetJsonAsync<T>(HttpClient client, string requestUri) where T : class
    {
        using var response = await client.GetAsync(requestUri);

        response.EnsureSuccessStatusCode();
        return await ReadJsonAsync<T>(response);
    }
    
    private static async Task<T> ReadJsonAsync<T>(HttpResponseMessage response) where T : class
    {
        var value = await response.Content.ReadFromJsonAsync<T>(JsonOptions);

        return Assert.IsType<T>(value);
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        const int maximumAttempts = 100;

        for (var attempt = 0; attempt < maximumAttempts; attempt++)
        {
            if (condition())
                return;

            await Task.Delay(50);
        }

        Assert.True(condition(), "The expected asynchronous condition was not reached.");
    }
    
    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);

        options.Converters.Add(
            new JsonStringEnumConverter(
                JsonNamingPolicy.CamelCase,
                allowIntegerValues: false));

        return options;
    }
}