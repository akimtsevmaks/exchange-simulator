using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using exchange_simulator.Contracts;

namespace exchange_simulator.Client;


internal sealed class MarketApiClient
{
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();
    
    private readonly HttpClient _httpClient;
    
    public MarketApiClient(HttpClient httpClient)
    {
        ArgumentNullException.ThrowIfNull(httpClient);

        if (httpClient.BaseAddress is null)
            throw new ArgumentException("HttpClient must have a base address", nameof(httpClient));

        _httpClient = httpClient;
    }
    
    public Task<InstrumentResponse> GetInstrumentAsync(CancellationToken cancellationToken) =>
        GetAsync<InstrumentResponse>("api/market/instrument", cancellationToken);

    public Task<MarketStateResponse> GetMarketStateAsync(CancellationToken cancellationToken) =>
        GetAsync<MarketStateResponse>("api/market/state", cancellationToken);

    public Task<OrderBookResponse> GetOrderBookAsync(CancellationToken cancellationToken) =>
        GetAsync<OrderBookResponse>("api/market/order-book", cancellationToken);
    
    public Task<TradeResponse[]> GetRecentTradesAsync(int limit, CancellationToken cancellationToken)
    {
        var value = limit.ToString(CultureInfo.InvariantCulture);
        return GetAsync<TradeResponse[]>($"api/market/trades?limit={value}", cancellationToken);
    }
    
    public Task<AccountResponse> GetAccountAsync(CancellationToken cancellationToken) =>
        GetAsync<AccountResponse>("api/account", cancellationToken);

    public Task<OrderResponse[]> GetActiveOrdersAsync(CancellationToken cancellationToken) =>
        GetAsync<OrderResponse[]>("api/account/active-orders", cancellationToken);
    
    public async Task<OrderCommandResponse> PlaceOrderAsync(PlaceOrderRequest order, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(order);

        using var request = new HttpRequestMessage(HttpMethod.Post, "api/orders");
        request.Content = JsonContent.Create(order, options: JsonOptions);

        return await SendAsync<OrderCommandResponse>(request, cancellationToken);
    }
    
    public async Task<OrderCommandResponse> CancelOrderAsync(Guid orderId, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Delete, $"api/orders/{orderId:D}");

        return await SendAsync<OrderCommandResponse>(request, cancellationToken);
    }
    
    private async Task<T> GetAsync<T>(string requestUri, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, requestUri);

        return await SendAsync<T>(request, cancellationToken);
    }
    
    private async Task<T> SendAsync<T>(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        using var response = await _httpClient.SendAsync(request, cancellationToken);

        if (!response.IsSuccessStatusCode)
            throw await CreateApiExceptionAsync(response, cancellationToken);

        try
        {
            var result = await response.Content.ReadFromJsonAsync<T>(JsonOptions, cancellationToken);

            return result ?? throw new InvalidDataException(
                "The server returned an empty successful response");
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("The successful server response contains invalid JSON", exception);
        }
        catch (NotSupportedException exception)
        {
            throw new InvalidDataException("The successful server response does not match the expected contract", exception);
        }
    }

    private static async Task<ApiRequestException> CreateApiExceptionAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        ApiErrorResponse? error = null;

        try
        {
            error = await response.Content.ReadFromJsonAsync<ApiErrorResponse>(JsonOptions, cancellationToken);
        }
        catch (JsonException) { }
        catch (NotSupportedException) { }

        var code = string.IsNullOrWhiteSpace(error?.Code)
            ? "InvalidErrorResponse" : error.Code;
        var message = string.IsNullOrWhiteSpace(error?.Message)
            ? "The server returned an error without a valid API error body" : error.Message;

        return new ApiRequestException(response.StatusCode, code, message);
    }
    
    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            RespectNullableAnnotations = true,
            RespectRequiredConstructorParameters = true
        };

        options.Converters.Add(new JsonStringEnumConverter(
                JsonNamingPolicy.CamelCase,
                allowIntegerValues: false));

        return options;
    }
}

internal sealed class ApiRequestException(HttpStatusCode statusCode, string code, string message): Exception(message)
{
    public HttpStatusCode StatusCode { get; } = statusCode;
    public string Code { get; } = code;
}