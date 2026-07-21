using exchange_simulator.Enums;
using exchange_simulator.Models;

namespace exchange_simulator.Tests.TradingCore;

public class TradingEngineMatchingTests : TradingEngineTestBase
{
    [Theory]
    [InlineData(OrderSide.Buy)]
    [InlineData(OrderSide.Sell)]
    public void PlaceLimitOrder_ShouldMatch_WhenPriceEqualsBestOppositePrice(OrderSide incomingSide)
    {
        // Arrange
        var engine = new TradingEngine(GetTestInstrument());
        
        var restingSide = GetOppositeSide(incomingSide);
        
        // Act
        var restingResult = engine.PlaceOrder(GetLimitCommand(restingSide));
        var incomingResult = engine.PlaceOrder(GetLimitCommand(incomingSide));
        
        // Assert
        var restingOrder = GetStoredOrder(engine, GetResultOrder(restingResult).Id);
        var incomingOrder = GetResultOrder(incomingResult);
        var trade = Assert.Single(incomingResult.Trades);

        Assert.Equal((100m, 10L), (trade.Price, trade.Size));
        Assert.Equal(OrderStatus.Filled, restingOrder.OrderStatus);
        Assert.Equal(OrderStatus.Filled, incomingOrder.OrderStatus);
        
        var orderBook = engine.GetOrderBookSnapshot();
        Assert.Empty(orderBook.Bids);
        Assert.Empty(orderBook.Asks);
    }
    
    [Theory]
    [InlineData(OrderSide.Buy)]
    [InlineData(OrderSide.Sell)]
    public void PlaceLimitOrder_ShouldRestIncomingRemainder_WhenIncomingOrderIsLarger(OrderSide incomingSide)
    {
        // Arrange
        var engine = new TradingEngine(GetTestInstrument());

        var restingSide = GetOppositeSide(incomingSide);
        const decimal restingPrice = 100m;
        var incomingPrice = incomingSide == OrderSide.Buy ? 110m : 90m;

        // Act
        engine.PlaceOrder(GetLimitCommand(restingSide, 20, restingPrice));
        var incomingResult = engine.PlaceOrder(GetLimitCommand(incomingSide, 30, incomingPrice));

        // Assert
        var incomingOrder = GetResultOrder(incomingResult);
        var trade = Assert.Single(incomingResult.Trades);

        Assert.Equal((restingPrice, 20L), (trade.Price, trade.Size));
        Assert.Equal(
            (OrderStatus.Active, 20L, 10L),
            (incomingOrder.OrderStatus, incomingOrder.FilledSize, incomingOrder.RemainingSize));

        var snapshot = engine.GetOrderBookSnapshot();
        var remainingLevel = Assert.Single(incomingSide == OrderSide.Buy ? snapshot.Bids : snapshot.Asks);

        Assert.Equal(new OrderBookLevel(incomingPrice, 10), remainingLevel);
    }
    
    [Theory]
    [InlineData(OrderSide.Buy)]
    [InlineData(OrderSide.Sell)]
    public void PlaceLimitOrder_ShouldLeaveRestingRemainder_WhenIncomingOrderIsSmaller(OrderSide incomingSide)
    {
        // Arrange
        var engine = new TradingEngine(GetTestInstrument());

        var restingSide = GetOppositeSide(incomingSide);
        const decimal restingPrice = 100m;
        var incomingPrice = incomingSide == OrderSide.Buy ? 110m : 90m;

        // Act
        var restingResult = engine.PlaceOrder(GetLimitCommand(restingSide, 30, restingPrice));
        var incomingResult = engine.PlaceOrder(GetLimitCommand(incomingSide, 20, incomingPrice));

        // Assert
        var restingOrder = GetStoredOrder(engine, GetResultOrder(restingResult).Id);
        var trade = Assert.Single(incomingResult.Trades);

        Assert.Equal((restingPrice, 20L), (trade.Price, trade.Size));
        Assert.Equal(
            (OrderStatus.Active, 20L, 10L),
            (restingOrder.OrderStatus, restingOrder.FilledSize, restingOrder.RemainingSize));

        var snapshot = engine.GetOrderBookSnapshot();
        var remainingLevel = Assert.Single(restingSide == OrderSide.Buy ? snapshot.Bids : snapshot.Asks);

        Assert.Equal(new OrderBookLevel(restingPrice, 10), remainingLevel);
    }
    
    [Theory]
    [InlineData(OrderSide.Buy)]
    [InlineData(OrderSide.Sell)]
    public void PlaceLimitOrder_ShouldPartiallyFillSecondLevel_AndLeaveFartherLevelUntouched(OrderSide incomingSide)
    {
        // Arrange
        var engine = new TradingEngine(GetTestInstrument());

        var restingSide = GetOppositeSide(incomingSide);
        var bestPrice = incomingSide == OrderSide.Buy ? 90m : 110m;
        const decimal secondPrice = 100m;
        var fartherPrice = incomingSide == OrderSide.Buy ? 110m : 90m;

        // Act
        engine.PlaceOrder(GetLimitCommand(restingSide, 30, fartherPrice));
        engine.PlaceOrder(GetLimitCommand(restingSide, 20, secondPrice));
        engine.PlaceOrder(GetLimitCommand(restingSide, 10, bestPrice));
        var incomingResult = engine.PlaceOrder(GetLimitCommand(incomingSide, 20, secondPrice));

        // Assert
        Assert.Collection(
            incomingResult.Trades,
            trade =>
                Assert.Equal((bestPrice, 10L), (trade.Price, trade.Size)),
            trade =>
                Assert.Equal((secondPrice, 10L), (trade.Price, trade.Size)));

        var snapshot = engine.GetOrderBookSnapshot();
        var remainingLevels = restingSide == OrderSide.Buy ? snapshot.Bids : snapshot.Asks;

        Assert.Equal(
            [new OrderBookLevel(secondPrice, 10), new OrderBookLevel(fartherPrice, 30)],
            remainingLevels);
    }
}