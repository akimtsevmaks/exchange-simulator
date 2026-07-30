using exchange_simulator.Bots;
using exchange_simulator.Clients;
using exchange_simulator.Enums;
using exchange_simulator.Models.TradingCore;

namespace exchange_simulator.Tests.LocalMarket;

public class LocalMarketConsoleClientTests : LocalMarketTestBase
{
    [Fact]
    public async Task RunAsync_ShouldPlaceManualLimitOrderAndPrintStatus()
    {
        // Arrange
        var market = GetMarket();
        
        using var input = new StringReader("buy limit 10 90\nstatus\nexit\n");
        using var output = new StringWriter();
        
        var client = new LocalMarketConsoleClient(market, input, output);

        // Act
        await client.RunAsync();

        // Assert
        var order = Assert.Single(market.GetActiveOrders(market.ManualAccountId));

        Assert.Equal(market.ManualAccountId, order.OwnerId);
        Assert.Equal(OrderSide.Buy, order.OrderSide);
        Assert.Equal(OrderType.Limit, order.OrderType);
        Assert.Equal(90m, order.Price);
        Assert.Equal(10, order.Size);

        var outputText = output.ToString();

        Assert.Contains(
            $"Order accepted: id={order.Id} status=Active " + "filled=0 remaining=10",
            outputText);
        Assert.Contains("Manual account", outputText);
        Assert.Contains(
            $"{order.Id} Buy Limit price=90 size=10 remaining=10",
            outputText);
        Assert.Contains("price=90 size=10", outputText);
    }

    [Fact]
    public async Task RunAsync_ShouldPlaceMarketOrderThroughManualAccount()
    {
        // Arrange
        var market = GetMarket();
        var marketMaker = new MarketMakerBot(market, quoteOffset: 1m, orderSize: 10);
        marketMaker.ExecuteStep();

        using var input = new StringReader("buy market 10\nexit\n");
        using var output = new StringWriter();
        
        var client = new LocalMarketConsoleClient(market, input, output);

        // Act
        await client.RunAsync();

        // Assert
        var trade = Assert.Single(market.GetTrades());

        Assert.Equal(101m, trade.Price);
        Assert.Equal(10, trade.Size);

        Assert.True(market.TryGetOrder(market.ManualAccountId, trade.BuyOrderId, out var orderSnapshot));

        var order = Assert.IsType<OrderSnapshot>(orderSnapshot);

        Assert.Equal(market.ManualAccountId, order.OwnerId);
        Assert.Equal(OrderSide.Buy, order.OrderSide);
        Assert.Equal(OrderType.Market, order.OrderType);
        Assert.Null(order.Price);
        Assert.Equal(10, order.Size);
        Assert.Equal(OrderStatus.Filled, order.OrderStatus);

        Assert.Contains(
            $"Order accepted: id={order.Id} status=Filled " + "filled=10 remaining=0",
            output.ToString());
    }

    [Fact]
    public async Task RunAsync_ShouldPrintDomainRejection()
    {
        // Arrange
        var market = GetMarket(initialCash: 100m);
        
        using var input = new StringReader("buy limit 10 90\nexit\n");
        using var output = new StringWriter();
        
        var client = new LocalMarketConsoleClient(market, input, output);

        // Act
        await client.RunAsync();

        // Assert
        Assert.Empty(market.GetActiveOrders(market.ManualAccountId));
        
        var outputText = output.ToString();

        Assert.Contains("Order rejected:", outputText);
        Assert.Contains(
            nameof(OrderRejectionReason.InsufficientAvailableCash),
            outputText);
    }

    [Fact]
    public async Task RunAsync_ShouldNotCancelAnotherParticipantsOrder()
    {
        // Arrange
        var market = GetMarket();
        var marketMaker = new MarketMakerBot(market, quoteOffset: 1m, orderSize: 10);
        marketMaker.ExecuteStep();
        var marketMakerOrder = market.GetActiveOrders(market.MarketMakerAccountId).First();

        using var input = new StringReader($"cancel {marketMakerOrder.Id}\nexit\n");
        using var output = new StringWriter();
        
        var client = new LocalMarketConsoleClient(market, input, output);

        // Act
        await client.RunAsync();

        // Assert
        Assert.Equal(
            OrderStatus.Active,
            GetStoredOrder(market, market.MarketMakerAccountId, marketMakerOrder.Id).OrderStatus);
        Assert.Contains(
            "does not belong to the manual participant",
            output.ToString());
    }

    [Theory]
    [InlineData("buy limit invalid 100", "Invalid size")]
    [InlineData("buy limit 999999999999999999999999999999 100", "Invalid size")]
    [InlineData("sell limit 10 invalid", "Invalid price")]
    [InlineData("sell limit 10 999999999999999999999999999999", "Invalid price")]
    [InlineData("buy limit 0 100", "Order rejected: InvalidSize")]
    [InlineData("buy limit -1 100", "Order rejected: InvalidSize")]
    [InlineData("buy market 0", "Order rejected: InvalidSize")]
    [InlineData("sell market -1", "Order rejected: InvalidSize")]
    [InlineData("buy limit 10 0", "Order rejected: InvalidPrice")]
    [InlineData("sell limit 10 -1", "Order rejected: InvalidPrice")]
    [InlineData("buy", "Usage: buy limit <size> <price>")]
    [InlineData("buy limit 10", "Usage: buy limit <size> <price>")]
    [InlineData("buy market", "Usage: buy market <size>")]
    [InlineData("buy market 10 100", "Usage: buy market <size>")]
    [InlineData("buy invalid 10 100", "Unknown order type 'invalid'")]
    [InlineData("cancel", "Usage: cancel <orderId>")]
    [InlineData("cancel invalid", "Invalid order id")]
    [InlineData("invalid", "Unknown command 'invalid'")]
    [InlineData("buy limit 9223372036854775807 79228162514264337593543950335",
        "Command rejected: numeric value is outside the supported range")]
    public async Task RunAsync_ShouldPrintInputError(string command, string expectedMessage)
    {
        // Arrange
        var market = GetMarket();
        
        using var input = new StringReader($"{command}\nexit\n");
        using var output = new StringWriter();
        
        var client = new LocalMarketConsoleClient(market, input, output);

        // Act
        await client.RunAsync();

        // Assert
        Assert.Contains(expectedMessage, output.ToString());
        Assert.Empty(market.GetActiveOrders(market.ManualAccountId));
    }
}
