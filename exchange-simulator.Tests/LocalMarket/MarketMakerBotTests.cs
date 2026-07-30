using exchange_simulator.Bots;
using exchange_simulator.Enums;

namespace exchange_simulator.Tests.LocalMarket;

public class MarketMakerBotTests : LocalMarketTestBase
{
    [Fact]
    public void ExecuteStep_ShouldCreateBidAndAskAroundReferencePrice()
    {
        // Arrange
        var market = GetMarket();
        var bot = new MarketMakerBot(market, quoteOffset: 5m, orderSize: 10);

        // Act
        bot.ExecuteStep();

        // Assert
        var orders = market.GetActiveOrders(market.MarketMakerAccountId);
        var bid = Assert.Single(orders,
            order => order.OrderSide == OrderSide.Buy);
        var ask = Assert.Single(orders,
            order => order.OrderSide == OrderSide.Sell);

        Assert.Equal(OrderType.Limit, bid.OrderType);
        Assert.Equal(95m, bid.Price);
        Assert.Equal(10, bid.RemainingSize);
        
        Assert.Equal(OrderType.Limit, ask.OrderType);
        Assert.Equal(105m, ask.Price);
        Assert.Equal(10, ask.RemainingSize);
    }

    [Fact]
    public void ExecuteStep_ShouldNotDuplicateCurrentQuotes()
    {
        // Arrange
        var market = GetMarket();
        var bot = new MarketMakerBot(market, quoteOffset: 5m, orderSize: 10);
        
        bot.ExecuteStep();
        
        var orderIdsBefore = market
            .GetActiveOrders(market.MarketMakerAccountId)
            .Select(order => order.Id).Order().ToArray();

        // Act
        bot.ExecuteStep();

        // Assert
        var orderIdsAfter = market
            .GetActiveOrders(market.MarketMakerAccountId)
            .Select(order => order.Id).Order().ToArray();

        Assert.Equal(orderIdsBefore, orderIdsAfter);
    }

    [Fact]
    public void ExecuteStep_ShouldCancelOldQuotesBeforeQuotingNewReferencePrice()
    {
        // Arrange
        var market = GetMarket();
        var bot = new MarketMakerBot(market, quoteOffset: 5m, orderSize: 10);
        bot.ExecuteStep();

        var oldBid = Assert.Single(
            market.GetActiveOrders(market.MarketMakerAccountId),
            order => order.OrderSide == OrderSide.Buy);

        var manualBuy = PlaceMarket(market, market.ManualAccountId, OrderSide.Buy, size: 10);

        Assert.True(manualBuy.IsSuccess);
        Assert.Equal(105m, market.GetReferencePrice());

        // Act
        bot.ExecuteStep();

        // Assert
        var orders = market.GetActiveOrders(market.MarketMakerAccountId);
        var bid = Assert.Single(orders,
            order => order.OrderSide == OrderSide.Buy);
        var ask = Assert.Single(orders,
            order => order.OrderSide == OrderSide.Sell);

        Assert.Equal(100m, bid.Price);
        Assert.Equal(110m, ask.Price);
        Assert.Equal(
            OrderStatus.Cancelled,
            GetStoredOrder(market, market.MarketMakerAccountId, oldBid.Id).OrderStatus);
    }

    [Fact]
    public void ExecuteStep_ShouldSkipBothSides_WhenResourcesAreInsufficient()
    {
        // Arrange
        var market = GetMarket(initialCash: 500m, initialInstruments: 5);
        var bot = new MarketMakerBot(market, quoteOffset: 1m, orderSize: 10);
        
        var accountBefore = GetAccount(market, market.MarketMakerAccountId);

        // Act
        bot.ExecuteStep();

        // Assert
        Assert.Empty(market.GetActiveOrders(market.MarketMakerAccountId));
        Assert.Equal(
            accountBefore,
            GetAccount(market, market.MarketMakerAccountId));
    }

    [Fact]
    public void Stop_ShouldCancelQuotesAndPreventNewOnes()
    {
        // Arrange
        var market = GetMarket();
        var bot = new MarketMakerBot(market, quoteOffset: 5m, orderSize: 10);
        bot.ExecuteStep();

        // Act
        bot.Stop();
        bot.ExecuteStep();

        // Assert
        Assert.Empty(market.GetActiveOrders(market.MarketMakerAccountId));
    }
}
