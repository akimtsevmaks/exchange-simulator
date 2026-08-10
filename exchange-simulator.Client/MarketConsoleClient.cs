using System.Globalization;
using exchange_simulator.Contracts;

namespace exchange_simulator.Client;


internal sealed class MarketConsoleClient
{
    private const int RecentTradeCount = 10;

    private readonly MarketApiClient _apiClient;
    private readonly TextReader _input;
    private readonly TextWriter _output;

    public MarketConsoleClient(MarketApiClient apiClient) : this(apiClient, Console.In, Console.Out) { }
    public MarketConsoleClient(MarketApiClient apiClient, TextReader input, TextWriter output)
    {
        ArgumentNullException.ThrowIfNull(apiClient);
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(output);

        _apiClient = apiClient;
        _input = input;
        _output = output;
    }

    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        _output.WriteLine("Exchange HTTP client. Type 'help' to list commands");

        while (!cancellationToken.IsCancellationRequested)
        {
            _output.Write("> ");
            _output.Flush();

            var input = await ReadLineAsync(cancellationToken);

            if (input is null || !await ExecuteCommandAsync(input, cancellationToken))
                return;
        }
    }

    private async Task<string?> ReadLineAsync(CancellationToken cancellationToken)
    {
        var readTask = Task.Run(() =>
            _input.ReadLine(), CancellationToken.None);

        try
        {
            return await readTask.WaitAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return null;
        }
    }

    private async Task<bool> ExecuteCommandAsync(string input, CancellationToken cancellationToken)
    {
        var parts = input.Split((char[]?)null,
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (parts.Length == 0)
            return true;

        try
        {
            switch (parts[0].ToLowerInvariant())
            {
                case "status":
                    await ExecuteStatusAsync(parts, cancellationToken);
                    return true;
                case "buy":
                    await ExecutePlaceOrderAsync(parts, OrderSide.Buy, cancellationToken);
                    return true;
                case "sell":
                    await ExecutePlaceOrderAsync(parts, OrderSide.Sell, cancellationToken);
                    return true;
                case "cancel":
                    await ExecuteCancelAsync(parts, cancellationToken);
                    return true;
                case "help":
                    return ExecuteHelp(parts);
                case "exit":
                    return ExecuteExit(parts);
                default:
                    return UnknownCommand(parts[0]);
            }
        }
        catch (ApiRequestException exception)
        {
            PrintApiError(exception);
            return true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return false;
        }
        catch (OperationCanceledException)
        {
            _output.WriteLine("Request timed out. The request was not retried automatically.");
            return true;
        }
        catch (HttpRequestException exception)
        {
            _output.WriteLine($"Network error: {exception.Message}");
            _output.WriteLine("The request was not retried automatically.");
            return true;
        }
        catch (InvalidDataException exception)
        {
            _output.WriteLine($"Invalid server response: {exception.Message}");
            return true;
        }
    }

    private async Task ExecuteStatusAsync(string[] parts, CancellationToken cancellationToken)
    {
        if (!HasExpectedPartCount(parts, 1, "status"))
            return;

        var instrument = await _apiClient.GetInstrumentAsync(cancellationToken);
        var state = await _apiClient.GetMarketStateAsync(cancellationToken);
        var orderBook = await _apiClient.GetOrderBookAsync(cancellationToken);
        var trades = await _apiClient.GetRecentTradesAsync(
            RecentTradeCount,
            cancellationToken);
        var account = await _apiClient.GetAccountAsync(cancellationToken);
        var activeOrders = await _apiClient.GetActiveOrdersAsync(cancellationToken);

        PrintStatus(instrument, state, orderBook, trades, account, activeOrders);
    }

    private async Task ExecutePlaceOrderAsync(string[] parts, OrderSide side, CancellationToken cancellationToken)
    {
        if (parts.Length < 2)
        {
            PrintOrderUsage(side);
            return;
        }

        switch (parts[1].ToLowerInvariant())
        {
            case "limit":
                await ExecuteLimitOrderAsync(parts, side, cancellationToken);
                break;
            case "market":
                await ExecuteMarketOrderAsync(parts, side, cancellationToken);
                break;
            default:
                InvalidOrderType(side, parts[1]);
                break;
        }
    }

    private async Task ExecuteLimitOrderAsync(string[] parts, OrderSide side, CancellationToken cancellationToken)
    {
        if (!HasExpectedPartCount(parts, 4, $"{SideCommand(side)} limit <size> <price>") ||
            !TryParseSize(parts[2], out var size) ||
            !TryParsePrice(parts[3], out var price))
        {
            return;
        }

        var request = new PlaceOrderRequest(side, OrderType.Limit, size, price);

        await ExecuteMutationAsync(
            token => _apiClient.PlaceOrderAsync(request, token),
            PrintPlacementResult,
            cancellationToken);
    }

    private async Task ExecuteMarketOrderAsync(string[] parts, OrderSide side, CancellationToken cancellationToken)
    {
        if (!HasExpectedPartCount(parts, 3, $"{SideCommand(side)} market <size>") ||
            !TryParseSize(parts[2], out var size))
        {
            return;
        }

        var request = new PlaceOrderRequest(side, OrderType.Market, size, null);

        await ExecuteMutationAsync(
            token => _apiClient.PlaceOrderAsync(request, token),
            PrintPlacementResult,
            cancellationToken);
    }

    private async Task ExecuteCancelAsync(string[] parts, CancellationToken cancellationToken)
    {
        if (!HasExpectedPartCount(parts, 2, "cancel <orderId>"))
            return;

        if (!Guid.TryParse(parts[1], out var orderId))
        {
            _output.WriteLine("Invalid order id");
            return;
        }

        await ExecuteMutationAsync(
            token => _apiClient.CancelOrderAsync(orderId, token),
            PrintCancellationResult,
            cancellationToken);
    }

    private async Task ExecuteMutationAsync(
        Func<CancellationToken, Task<OrderCommandResponse>> command,
        Action<OrderCommandResponse> printSuccess,
        CancellationToken cancellationToken)
    {
        try
        {
            var response = await command(cancellationToken);
            printSuccess(response);
        }
        catch (ApiRequestException exception) when ((int)exception.StatusCode >= 500)
        {
            await HandleUnknownCommandResultAsync(
                $"server returned HTTP {(int)exception.StatusCode} {exception.Code}: {exception.Message}",
                cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            using var reconciliationTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));

            await HandleUnknownCommandResultAsync(
                "the client stopped before the response was received",
                reconciliationTimeout.Token);

            throw;
        }
        catch (OperationCanceledException)
        {
            await HandleUnknownCommandResultAsync(
                "the request timed out before a response was received",
                cancellationToken);
        }
        catch (HttpRequestException exception)
        {
            await HandleUnknownCommandResultAsync(
                $"the connection failed before a response was received ({exception.Message})",
                cancellationToken);
        }
        catch (InvalidDataException exception)
        {
            await HandleUnknownCommandResultAsync(
                $"the successful response could not be read ({exception.Message})",
                cancellationToken);
        }
    }

    private async Task HandleUnknownCommandResultAsync(string reason, CancellationToken cancellationToken)
    {
        PrintUnknownCommandResult(reason);
        _output.WriteLine("Refreshing account, active orders and recent trades without repeating the command.");

        await TryRefreshAsync(
            "account",
            async () => PrintAccount(
                await _apiClient.GetAccountAsync(cancellationToken)),
            cancellationToken);

        await TryRefreshAsync(
            "active orders",
            async () => PrintActiveOrders(
                await _apiClient.GetActiveOrdersAsync(cancellationToken)),
            cancellationToken);

        await TryRefreshAsync(
            "recent trades",
            async () => PrintRecentTrades(
                await _apiClient.GetRecentTradesAsync(RecentTradeCount, cancellationToken)),
            cancellationToken);
    }

    private async Task TryRefreshAsync(string stateName, Func<Task> readAndPrint, CancellationToken cancellationToken)
    {
        try
        {
            await readAndPrint();
        }
        catch (ApiRequestException exception)
        {
            _output.Write($"Could not refresh {stateName}: ");
            PrintApiError(exception);
        }
        catch (OperationCanceledException)
        {
            var reason = cancellationToken.IsCancellationRequested
                ? "refresh was cancelled or exceeded its time limit"
                : "request timed out";

            _output.WriteLine($"Could not refresh {stateName}: {reason}.");
        }
        catch (HttpRequestException exception)
        {
            _output.WriteLine($"Could not refresh {stateName}: network error ({exception.Message}).");
        }
        catch (InvalidDataException exception)
        {
            _output.WriteLine($"Could not refresh {stateName}: invalid server response ({exception.Message}).");
        }
    }

    private bool ExecuteHelp(string[] parts)
    {
        if (!HasExpectedPartCount(parts, 1, "help"))
            return true;

        PrintHelp();
        return true;
    }

    private bool ExecuteExit(string[] parts)
    {
        if (!HasExpectedPartCount(parts, 1, "exit"))
            return true;

        return false;
    }

    private void PrintStatus(
        InstrumentResponse instrument,
        MarketStateResponse state,
        OrderBookResponse orderBook,
        IReadOnlyList<TradeResponse> trades,
        AccountResponse account,
        IReadOnlyList<OrderResponse> activeOrders)
    {
        _output.WriteLine();
        _output.WriteLine("Market");
        _output.WriteLine("The sections below are separate server reads and may represent different moments.");
        _output.WriteLine(
            $"Instrument: {instrument.Ticker} ({instrument.Name}) " +
            $"lot={instrument.LotSize} " +
            $"initialPrice={FormatDecimal(instrument.InitialPrice)}");
        _output.WriteLine($"Status: {state.Status}");
        _output.WriteLine($"Reference price: {FormatDecimal(state.ReferencePrice)}");

        _output.WriteLine("Order book");
        PrintBookSide("Asks", orderBook.Asks);
        PrintBookSide("Bids", orderBook.Bids);
        PrintRecentTrades(trades);
        PrintAccount(account);
        PrintActiveOrders(activeOrders);
        _output.WriteLine();
    }

    private void PrintBookSide(string name, IReadOnlyList<OrderBookLevelResponse> levels)
    {
        _output.WriteLine($"  {name}");

        if (levels.Count == 0)
        {
            _output.WriteLine("    -");
            return;
        }

        foreach (var level in levels)
        {
            _output.WriteLine($"    price={FormatDecimal(level.Price)} size={level.Size}");
        }
    }

    private void PrintRecentTrades(IReadOnlyList<TradeResponse> trades)
    {
        _output.WriteLine($"Last {RecentTradeCount} trades");

        if (trades.Count == 0)
        {
            _output.WriteLine("  -");
            return;
        }

        foreach (var trade in trades)
        {
            _output.WriteLine($"  {trade.ExecutedAt:O} price={FormatDecimal(trade.Price)} size={trade.Size}");
        }
    }

    private void PrintAccount(AccountResponse account)
    {
        var position = account.Position;

        _output.WriteLine($"Account {account.Id}");
        _output.WriteLine(
            $"  cash: total={FormatDecimal(account.CashBalance)} " +
            $"reserved={FormatDecimal(account.ReservedCash)} " +
            $"available={FormatDecimal(account.AvailableCash)}");
        _output.WriteLine(
            $"  position: total={position.Quantity} " +
            $"reserved={position.ReservedQuantity} " +
            $"available={position.AvailableQuantity} " +
            $"averagePrice={FormatDecimal(position.AveragePrice)}");
    }

    private void PrintActiveOrders(IReadOnlyList<OrderResponse> activeOrders)
    {
        _output.WriteLine("Active orders");

        if (activeOrders.Count == 0)
        {
            _output.WriteLine("  -");
            return;
        }

        foreach (var order in activeOrders)
        {
            var price = order.Price.HasValue
                ? FormatDecimal(order.Price.Value) : "-";

            _output.WriteLine(
                $"  {order.Id} {order.Side} {order.Type} price={price} size={order.Size} remaining={order.RemainingSize}");
        }
    }

    private void PrintPlacementResult(OrderCommandResponse response)
    {
        var order = response.Order;

        _output.WriteLine(
            $"Order accepted: id={order.Id} status={order.Status} " +
            $"filled={order.FilledSize} remaining={order.RemainingSize}");
    }

    private void PrintCancellationResult(OrderCommandResponse response)
    {
        var order = response.Order;

        _output.WriteLine(
            $"Order cancelled: id={order.Id} status={order.Status} " +
            $"filled={order.FilledSize} remaining={order.RemainingSize}");
    }

    private void PrintApiError(ApiRequestException exception)
    {
        _output.WriteLine(
            $"HTTP {(int)exception.StatusCode} {exception.StatusCode}: " +
            $"{exception.Code}: {exception.Message}");
    }

    private void PrintUnknownCommandResult(string reason)
    {
        var normalizedReason = reason.Trim().TrimEnd('.');

        _output.WriteLine($"Command result is unknown: {normalizedReason}");
        _output.WriteLine("The trading command was not sent again");
    }

    private bool TryParseSize(string value, out long size)
    {
        if (long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out size))
        {
            return true;
        }

        _output.WriteLine("Invalid size");
        return false;
    }

    private bool TryParsePrice(string value, out decimal price)
    {
        const NumberStyles styles =
            NumberStyles.AllowLeadingSign |
            NumberStyles.AllowDecimalPoint;

        var normalizedValue = value.Contains(',') && !value.Contains('.') ? value.Replace(',', '.') : value;

        if (decimal.TryParse(normalizedValue, styles, CultureInfo.InvariantCulture, out price))
        {
            return true;
        }

        _output.WriteLine("Invalid price");
        return false;
    }

    private bool HasExpectedPartCount(string[] parts, int expectedCount, string usage)
    {
        if (parts.Length == expectedCount)
            return true;

        _output.WriteLine($"Usage: {usage}");
        return false;
    }

    private void InvalidOrderType(OrderSide side, string value)
    {
        _output.WriteLine($"Unknown order type '{value}'");
        PrintOrderUsage(side);
    }

    private bool UnknownCommand(string command)
    {
        _output.WriteLine($"Unknown command '{command}'. Type 'help' for usage");
        return true;
    }

    private void PrintOrderUsage(OrderSide side)
    {
        var command = SideCommand(side);

        _output.WriteLine($"Usage: {command} limit <size> <price>");
        _output.WriteLine($"       {command} market <size>");
    }

    private void PrintHelp()
    {
        _output.WriteLine("Commands:");
        _output.WriteLine("  status");
        _output.WriteLine("  buy limit <size> <price>");
        _output.WriteLine("  sell limit <size> <price>");
        _output.WriteLine("  buy market <size>");
        _output.WriteLine("  sell market <size>");
        _output.WriteLine("  cancel <orderId>");
        _output.WriteLine("  help");
        _output.WriteLine("  exit");
    }

    private static string SideCommand(OrderSide side) =>
        side == OrderSide.Buy ? "buy" : "sell";

    private static string FormatDecimal(decimal value) =>
        value.ToString(CultureInfo.InvariantCulture);
}
