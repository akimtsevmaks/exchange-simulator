using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using exchange_simulator.Client;
using exchange_simulator.Contracts;
using exchange_simulator.Tests.Server;

namespace exchange_simulator.Tests.Client;


public sealed class MarketConsoleClientNetworkTests
{
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();
    
    [Fact]
    public async Task ConsoleClient_ShouldCompleteHappyPathThroughTestServer()
    {
        // Arrange
        using var factory = new ExchangeServerFactory();
        using var httpClient = factory.CreateClient();
        
        using var input = new StringReader("buy limit 3 90\n" +
                                           "status\n" +
                                           "exit\n");
        using var output = new StringWriter();
        
        var client = new MarketConsoleClient(
            new MarketApiClient(httpClient), input, output);

        // Act
        await client.RunAsync();

        // Assert
        var outputText = output.ToString();
        Assert.Contains("Order accepted:", outputText);
        Assert.Contains("Market", outputText);
        Assert.Contains("separate server reads", outputText);
        Assert.Contains("Account", outputText);
        Assert.Contains("price=90 size=3", outputText);
    }
    
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task UnknownMutationResult_ShouldNotRetryAndShouldReconcile(bool simulateTimeout)
    {
        // Arrange
        var handler = new RecordingHttpMessageHandler((request, cancellationToken) =>
        {
            if (request.Method == HttpMethod.Post)
            {
                if (simulateTimeout)
                    throw new OperationCanceledException(cancellationToken);

                throw new HttpRequestException("Connection interrupted.");
            }

            return Task.FromResult(CreateRecoveryResponse(request));
        });
        using var httpClient = CreateHttpClient(handler);
        
        using var input = new StringReader("buy limit 1 90\n" +
                                           "exit\n");
        using var output = new StringWriter();
        
        var client = new MarketConsoleClient(
            new MarketApiClient(httpClient), input, output);

        // Act
        await client.RunAsync();

        // Assert
        Assert.Equal(
            [
                "POST /api/orders",
                "GET /api/account",
                "GET /api/account/active-orders",
                "GET /api/market/trades?limit=10"
            ],
            handler.Requests);
        Assert.Contains("Command result is unknown", output.ToString());
        Assert.Contains("was not sent again", output.ToString());
    }
    
    [Fact]
    public async Task DefiniteApiRejection_ShouldNotBecomeUnknownOrRetry()
    {
        // Arrange
        var handler = new RecordingHttpMessageHandler((_, _) =>
            Task.FromResult(JsonResponse(
                HttpStatusCode.Conflict,
                new ApiErrorResponse(
                    "InsufficientAvailableCash",
                    "The account does not have enough available cash."))));
        using var httpClient = CreateHttpClient(handler);
        
        using var input = new StringReader("buy limit 10 90\n" +
                                           "exit\n");
        using var output = new StringWriter();
        
        var client = new MarketConsoleClient(
            new MarketApiClient(httpClient), input, output);

        // Act
        await client.RunAsync();

        // Assert
        Assert.Equal([ "POST /api/orders" ], handler.Requests);
        Assert.Contains(
            "HTTP 409 Conflict: InsufficientAvailableCash",
            output.ToString());
        Assert.DoesNotContain("result is unknown", output.ToString());
    }
    
    [Fact]
    public async Task ReadFailure_ShouldNotRetryAutomatically()
    {
        // Arrange
        var handler = new RecordingHttpMessageHandler((_, _) =>
            throw new HttpRequestException("Server unavailable."));
        using var httpClient = CreateHttpClient(handler);
        
        using var input = new StringReader("status\n" +
                                           "exit\n");
        using var output = new StringWriter();
        
        var client = new MarketConsoleClient(
            new MarketApiClient(httpClient), input, output);

        // Act
        await client.RunAsync();

        // Assert
        Assert.Equal([ "GET /api/market/instrument" ], handler.Requests);
        Assert.Contains("Network error:", output.ToString());
        Assert.Contains("not retried automatically", output.ToString());
    }
    
    
    private static HttpClient CreateHttpClient(HttpMessageHandler handler) =>
        new(handler)
        {
            BaseAddress = new Uri("http://test-server/"),
            Timeout = TimeSpan.FromMilliseconds(100)
        };
    
    private static HttpResponseMessage CreateRecoveryResponse(HttpRequestMessage request) =>
        request.RequestUri!.AbsolutePath switch
        {
            "/api/account" => JsonResponse(
                HttpStatusCode.OK,
                new AccountResponse(
                    Guid.NewGuid(),
                    CashBalance: 100000m,
                    ReservedCash: 0m,
                    AvailableCash: 100000m,
                    new PositionResponse(
                        Guid.NewGuid(),
                        Quantity: 1000,
                        ReservedQuantity: 0,
                        AvailableQuantity: 1000,
                        AveragePrice: 100m))),
            "/api/account/active-orders" => JsonResponse(
                HttpStatusCode.OK,
                Array.Empty<OrderResponse>()),
            "/api/market/trades" => JsonResponse(
                HttpStatusCode.OK,
                Array.Empty<TradeResponse>()),
            _ => throw new InvalidOperationException(
                $"Unexpected recovery route '{request.RequestUri}'.")
        };
    
    private static HttpResponseMessage JsonResponse<T>(HttpStatusCode statusCode, T value) =>
        new(statusCode)
        {
            Content = JsonContent.Create(value, options: JsonOptions)
        };
    
    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);

        options.Converters.Add(
            new JsonStringEnumConverter(
                JsonNamingPolicy.CamelCase,
                allowIntegerValues: false));

        return options;
    }
    
    private sealed class RecordingHttpMessageHandler(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> sendAsync) : HttpMessageHandler
    {
        public List<string> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(
                $"{request.Method.Method} " +
                $"{request.RequestUri!.PathAndQuery}");

            return sendAsync(request, cancellationToken);
        }
    }
}