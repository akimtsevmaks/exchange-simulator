using exchange_simulator.Enums;
using exchange_simulator.Models;

namespace exchange_simulator.Tests.TradingCore;

public class TradingEngineMarketOrderTests : TradingEngineTestBase
{
    [Theory]
    [InlineData(OrderSide.Buy)]
    [InlineData(OrderSide.Sell)]
    public void PlaceMarketOrder_ShouldCancelWithoutTrades_WhenOrderBookIsEmpty(OrderSide side)
    {
        // Arrange
        var engine = new TradingEngine(GetTestInstrument());

        const long size = 30;
        
        // Act
        var result = engine.PlaceOrder(GetMarketCommand(side, size));
        
        // Assert
        var order = GetResultOrder(result);
        
        Assert.True(result.IsSuccess);
        Assert.Empty(result.Trades);
        Assert.Equal(
            (OrderStatus.Cancelled, 0L, size),
            (order.OrderStatus, order.FilledSize, order.RemainingSize));

        var snapshot = engine.GetOrderBookSnapshot();
        Assert.Empty(snapshot.Bids);
        Assert.Empty(snapshot.Asks);
    }

    [Theory]
    [InlineData(OrderSide.Buy)]
    [InlineData(OrderSide.Sell)]
    public void PlaceMarketOrder_ShouldCancelUnfilledRemainder_WhenLiquidityIsInsufficient(OrderSide incomingSide)
    {
        // Arrange
        var engine = new TradingEngine(GetTestInstrument());
        
        var restingSide = GetOppositeSide(incomingSide);
        var bestPrice = incomingSide == OrderSide.Buy ? 90m : 110m;
        var secondPrice = incomingSide == OrderSide.Buy ? 95m : 105m;
        
        const long bestSize = 10;
        const long secondSize = 20;
        const long marketSize = 50;
        const long availableLiquidity = bestSize + secondSize;
        const long expectedRemainder = marketSize - availableLiquidity;
        
        // Act
        engine.PlaceOrder(GetLimitCommand(restingSide, bestSize, bestPrice));
        engine.PlaceOrder(GetLimitCommand(restingSide, secondSize, secondPrice));
        var marketResult = engine.PlaceOrder(GetMarketCommand(incomingSide, marketSize));
        
        // Assert
        var marketOrder = GetResultOrder(marketResult);
        
        Assert.Equal(availableLiquidity, marketResult.Trades.Sum(trade => trade.Size));
        Assert.Equal(
            (OrderStatus.Cancelled, availableLiquidity, expectedRemainder),
            (marketOrder.OrderStatus, marketOrder.FilledSize, marketOrder.RemainingSize));
    }
}