using exchange_simulator.Enums;
using exchange_simulator.Models;
using exchange_simulator.Models.TradingCore;

namespace exchange_simulator.Tests.TradingCore;

public class TradingEnginePricePriorityTests : TradingEngineTestBase
{
    [Theory]
    [InlineData(OrderSide.Buy)]
    [InlineData(OrderSide.Sell)]
    public void PlaceMarketOrder_ShouldMatchBestOppositePriceFirst(OrderSide incomingSide)
    {
        // Arrange
        var engine = new TradingEngine(GetTestInstrument());

        var restingSide = GetOppositeSide(incomingSide);
        const decimal worsePrice = 100m;
        var bestPrice = incomingSide == OrderSide.Buy ? 90m : 110m;

        // Act
        engine.PlaceOrder(GetLimitCommand(restingSide, price: worsePrice));
        engine.PlaceOrder(GetLimitCommand(restingSide, price: bestPrice));
        var marketResult = engine.PlaceOrder(GetMarketCommand(incomingSide));

        // Assert
        var trade = Assert.Single(marketResult.Trades);
        Assert.Equal(bestPrice, trade.Price);
    }
    
    [Theory]
    [InlineData(OrderSide.Buy)]
    [InlineData(OrderSide.Sell)]
    public void PlaceAggressiveLimitOrder_ShouldConsumeCrossingLevelsInPriceOrder_AndStopAtLimit(OrderSide incomingSide)
    {
        // Arrange
        var engine = new TradingEngine(GetTestInstrument());

        var restingSide = GetOppositeSide(incomingSide);
        const decimal limitPrice = 110m;
        var bestPrice = incomingSide == OrderSide.Buy ? 100m : 120m;
        const decimal secondPrice = 110m;
        var nonCrossingPrice = incomingSide == OrderSide.Buy ? 120m : 100m;

        // Act
        engine.PlaceOrder(GetLimitCommand(restingSide, 30, nonCrossingPrice));
        engine.PlaceOrder(GetLimitCommand(restingSide, 20, secondPrice));
        engine.PlaceOrder(GetLimitCommand(restingSide, 10, bestPrice));
        var incomingResult = engine.PlaceOrder(GetLimitCommand(incomingSide, 70, limitPrice));

        // Assert
        Assert.Collection(
            incomingResult.Trades,
            trade =>
                Assert.Equal((bestPrice, 10L), (trade.Price, trade.Size)),
            trade =>
                Assert.Equal((secondPrice, 20L), (trade.Price, trade.Size)));

        var snapshot = engine.GetOrderBookSnapshot();
        var incomingLevel = Assert.Single(incomingSide == OrderSide.Buy ? snapshot.Bids : snapshot.Asks);
        var untouchedLevel = Assert.Single(restingSide == OrderSide.Buy ? snapshot.Bids : snapshot.Asks);

        Assert.Equal(new OrderBookLevel(limitPrice, 40), incomingLevel);
        Assert.Equal(new OrderBookLevel(nonCrossingPrice, 30), untouchedLevel);
    }
}