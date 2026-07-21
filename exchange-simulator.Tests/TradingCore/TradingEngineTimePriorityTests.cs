using exchange_simulator.Enums;
using exchange_simulator.Models;
using exchange_simulator.Models.TradingCore;

namespace exchange_simulator.Tests.TradingCore;

public class TradingEngineTimePriorityTests : TradingEngineTestBase
{
    [Theory]
    [InlineData(OrderSide.Buy)]
    [InlineData(OrderSide.Sell)]
    public void PlaceMarketOrder_ShouldFillOldestOrderThenPartiallyFillNext_WhenOrdersHaveSamePrice(OrderSide incomingSide)
    {
        // Arrange
        var engine = new TradingEngine(GetTestInstrument());

        var restingSide = GetOppositeSide(incomingSide);
        const decimal price = 100m;

        // Act
        var firstOrder = GetResultOrder(engine.PlaceOrder(GetLimitCommand(restingSide, 20, price)));
        var secondOrder = GetResultOrder(engine.PlaceOrder(GetLimitCommand(restingSide, 20, price)));
        var marketResult = engine.PlaceOrder(GetMarketCommand(incomingSide, 30));

        // Assert
        Assert.Collection(
            marketResult.Trades,
            trade =>
                Assert.Equal((firstOrder.Id, 20L),
                    (GetRestingOrderId(trade, incomingSide), trade.Size)),
            trade =>
                Assert.Equal((secondOrder.Id, 10L),
                    (GetRestingOrderId(trade, incomingSide), trade.Size)));
    }
    
    [Theory]
    [InlineData(OrderSide.Buy)]
    [InlineData(OrderSide.Sell)]
    public void CancelAndReplaceOrder_ShouldLoseTimePriority_WhenOrdersHaveSamePrice(OrderSide incomingSide)
    {
        // Arrange
        var engine = new TradingEngine(GetTestInstrument());

        var restingSide = GetOppositeSide(incomingSide);
        var firstOwnerId = Guid.NewGuid();
        const decimal price = 100m;

        // Act
        var firstOrder = GetResultOrder(engine.PlaceOrder(GetLimitCommand(restingSide, 20, price, firstOwnerId)));
        var secondOrder = GetResultOrder(engine.PlaceOrder(GetLimitCommand(restingSide, 20, price)));

        engine.CancelOrder(firstOrder.Id);
        engine.PlaceOrder(GetLimitCommand(restingSide, 20, price, firstOwnerId));
        var marketResult = engine.PlaceOrder(GetMarketCommand(incomingSide, 20));

        // Assert
        var trade = Assert.Single(marketResult.Trades);

        Assert.Equal(secondOrder.Id, GetRestingOrderId(trade, incomingSide));
    }
    
    
    
    private static Guid GetRestingOrderId(Trade trade, OrderSide incomingSide) =>
        incomingSide == OrderSide.Buy ? trade.SellOrderId : trade.BuyOrderId;
}