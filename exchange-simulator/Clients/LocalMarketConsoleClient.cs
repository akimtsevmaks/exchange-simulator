using System.Globalization;
using exchange_simulator.Enums;
using exchange_simulator.Models;
using exchange_simulator.Models.TradingCore;
using exchange_simulator.Services;

namespace exchange_simulator.Clients;

public sealed class LocalMarketConsoleClient
{
    private const int RecentTradeCount = 10;

    private readonly LocalMarket _market;
    private readonly TextReader _input;
    private readonly TextWriter _output;

    public LocalMarketConsoleClient(LocalMarket market)
        : this(market, Console.In, Console.Out)
    {
    }

    public LocalMarketConsoleClient(
        LocalMarket market,
        TextReader input,
        TextWriter output)
    {
        ArgumentNullException.ThrowIfNull(market);
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(output);

        _market = market;
        _input = input;
        _output = output;
    }

    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        _output.WriteLine("Local market console. Type 'help' to list commands");

        while (!cancellationToken.IsCancellationRequested)
        {
            _output.Write("> ");
            _output.Flush();

            string? input;

            try
            {
                input = await _input.ReadLineAsync(cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            if (input is null || !ExecuteCommand(input))
                return;
        }
    }

    private bool ExecuteCommand(string input)
    {
        var parts = input.Split(
            (char[]?)null,
            StringSplitOptions.RemoveEmptyEntries |
            StringSplitOptions.TrimEntries);

        if (parts.Length == 0)
            return true;

        try
        {
            return parts[0].ToLowerInvariant() switch
            {
                "status" => ExecuteStatus(parts),
                "buy" => ExecutePlaceOrder(parts, OrderSide.Buy),
                "sell" => ExecutePlaceOrder(parts, OrderSide.Sell),
                "cancel" => ExecuteCancel(parts),
                "help" => ExecuteHelp(parts),
                "exit" => ExecuteExit(parts),
                _ => UnknownCommand(parts[0])
            };
        }
        catch (OverflowException)
        {
            _output.WriteLine("Command rejected: numeric value is outside the supported range");
            return true;
        }
    }

    private bool ExecuteStatus(string[] parts)
    {
        if (!HasExpectedPartCount(parts, 1, "status"))
            return true;

        PrintStatus(_market.GetSnapshot());
        return true;
    }

    private bool ExecutePlaceOrder(string[] parts, OrderSide side)
    {
        if (parts.Length < 2)
        {
            PrintOrderUsage(side);
            return true;
        }

        return parts[1].ToLowerInvariant() switch
        {
            "limit" => ExecuteLimitOrder(parts, side),
            "market" => ExecuteMarketOrder(parts, side),
            _ => InvalidOrderType(side, parts[1])
        };
    }

    private bool ExecuteLimitOrder(string[] parts, OrderSide side)
    {
        if (!HasExpectedPartCount(
                parts,
                4,
                $"{SideCommand(side)} limit <size> <price>"))
        {
            return true;
        }

        if (!TryParseSize(parts[2], out var size) ||
            !TryParsePrice(parts[3], out var price))
        {
            return true;
        }

        PlaceOrder(side, OrderType.Limit, size, price);
        return true;
    }

    private bool ExecuteMarketOrder(string[] parts, OrderSide side)
    {
        if (!HasExpectedPartCount(
                parts,
                3,
                $"{SideCommand(side)} market <size>"))
        {
            return true;
        }

        if (!TryParseSize(parts[2], out var size))
            return true;

        PlaceOrder(side, OrderType.Market, size);
        return true;
    }

    private bool ExecuteCancel(string[] parts)
    {
        if (!HasExpectedPartCount(parts, 2, "cancel <orderId>"))
            return true;

        if (!Guid.TryParse(parts[1], out var orderId))
        {
            _output.WriteLine("Invalid order id.");
            return true;
        }

        if (!_market.TryGetOrder(_market.ManualAccountId, orderId, out _))
        {
            _output.WriteLine(
                "Cancel rejected: the order does not belong to the manual participant");
            return true;
        }

        PrintCancellationResult(_market.CancelOrder(orderId));
        return true;
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

    private void PlaceOrder(
        OrderSide side,
        OrderType type,
        long size,
        decimal? price = null)
    {
        var command = new PlaceOrderCommand(
            _market.ManualAccountId,
            side,
            type,
            size,
            price);

        PrintPlacementResult(_market.PlaceOrder(command));
    }

    private void PrintStatus(LocalMarketSnapshot snapshot)
    {
        _output.WriteLine();
        _output.WriteLine("Market");

        if (snapshot.Trades.Count == 0)
        {
            _output.WriteLine(
                $"Reference price: {FormatDecimal(snapshot.ReferencePrice)} " +
                "(initial price; no trades yet)");
        }
        else
        {
            _output.WriteLine(
                $"Last price: {FormatDecimal(snapshot.ReferencePrice)}");
        }

        _output.WriteLine("Order book");
        PrintBookSide("Asks", snapshot.OrderBook.Asks);
        PrintBookSide("Bids", snapshot.OrderBook.Bids);

        _output.WriteLine($"Last {RecentTradeCount} trades");

        var recentTrades = snapshot.Trades.TakeLast(RecentTradeCount).ToArray();

        if (recentTrades.Length == 0)
        {
            _output.WriteLine("  -");
        }
        else
        {
            foreach (var trade in recentTrades)
            {
                _output.WriteLine(
                    $"  {trade.ExecutedAt:O} price={FormatDecimal(trade.Price)} " +
                    $"size={trade.Size}");
            }
        }

        var account = snapshot.ManualAccount;
        var position = account.Position;

        _output.WriteLine("Manual account");
        _output.WriteLine(
            $"  cash: total={FormatDecimal(account.CashBalance)} " +
            $"reserved={FormatDecimal(account.ReservedCash)} " +
            $"available={FormatDecimal(account.AvailableCash)}");
        _output.WriteLine(
            $"  position: total={position.Quantity} " +
            $"reserved={position.ReservedQuantity} " +
            $"available={position.AvailableQuantity} " +
            $"averagePrice={FormatDecimal(position.AveragePrice)}");

        _output.WriteLine("Active orders");

        if (snapshot.ManualActiveOrders.Count == 0)
        {
            _output.WriteLine("  -");
        }
        else
        {
            foreach (var order in snapshot.ManualActiveOrders)
            {
                var price = order.Price.HasValue
                    ? FormatDecimal(order.Price.Value)
                    : "-";

                _output.WriteLine(
                    $"  {order.Id} {order.OrderSide} {order.OrderType} " +
                    $"price={price} size={order.Size} remaining={order.RemainingSize}");
            }
        }

        _output.WriteLine();
    }

    private void PrintBookSide(
        string name,
        IReadOnlyList<OrderBookLevel> levels)
    {
        _output.WriteLine($"  {name}");

        if (levels.Count == 0)
        {
            _output.WriteLine("    -");
            return;
        }

        foreach (var level in levels)
        {
            _output.WriteLine(
                $"    price={FormatDecimal(level.Price)} size={level.Size}");
        }
    }

    private void PrintPlacementResult(OrderCommandResult result)
    {
        if (!result.IsSuccess)
        {
            _output.WriteLine($"Order rejected: {result.RejectionReason}");
            return;
        }

        var order = result.Order ??
                    throw new InvalidOperationException(
                        "Successful placement must return an order");

        _output.WriteLine(
            $"Order accepted: id={order.Id} status={order.OrderStatus} " +
            $"filled={order.FilledSize} remaining={order.RemainingSize}");
    }

    private void PrintCancellationResult(OrderCommandResult result)
    {
        if (!result.IsSuccess)
        {
            _output.WriteLine($"Cancel rejected: {result.RejectionReason}");
            return;
        }

        var order = result.Order ??
                    throw new InvalidOperationException(
                        "Successful cancellation must return an order");

        _output.WriteLine($"Order cancelled: id={order.Id}");
    }

    private bool TryParseSize(string value, out long size)
    {
        if (long.TryParse(
                value,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out size))
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

        var normalizedValue = value.Contains(',') && !value.Contains('.')
            ? value.Replace(',', '.')
            : value;

        if (decimal.TryParse(
                normalizedValue,
                styles,
                CultureInfo.InvariantCulture,
                out price))
        {
            return true;
        }

        _output.WriteLine("Invalid price");
        return false;
    }

    private bool HasExpectedPartCount(
        string[] parts,
        int expectedCount,
        string usage)
    {
        if (parts.Length == expectedCount)
            return true;

        _output.WriteLine($"Usage: {usage}");
        return false;
    }

    private bool InvalidOrderType(OrderSide side, string value)
    {
        _output.WriteLine($"Unknown order type '{value}'");
        PrintOrderUsage(side);
        return true;
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
