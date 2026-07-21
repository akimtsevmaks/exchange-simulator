using exchange_simulator.Enums;
using exchange_simulator.Models;

namespace exchange_simulator.Tests.TradingCore;

public class TradingEngineEndToEndTests : TradingEngineTestBase
{
    [Fact]
    public void AggressiveOrderWorkflow_ShouldKeepOrdersTradesAndBookConsistent()
    {
        // Arrange
        var engine = new TradingEngine(GetTestInstrument(lotSize: 10));
        var nextLevelOrder = GetResultOrder(engine.PlaceOrder(
            GetLimitCommand(OrderSide.Sell, 30, 110m)));
        var firstBestOrder = GetResultOrder(engine.PlaceOrder(
            GetLimitCommand(OrderSide.Sell, 10, 100m)));
        var secondBestOrder = GetResultOrder(engine.PlaceOrder(
            GetLimitCommand(OrderSide.Sell, 20, 100m)));

        Assert.Equal(
            [new OrderBookLevel(100m, 30), new OrderBookLevel(110m, 30)],
            engine.GetOrderBookSnapshot().Asks);

        // Act
        var stateBeforeInvalidCommand = CaptureState(engine);
        var invalidResult = engine.PlaceOrder(
            GetLimitCommand(OrderSide.Buy, 15, 110m));
        
        // Assert
        Assert.False(invalidResult.IsSuccess);
        Assert.Equal(OrderRejectionReason.QuantityNotMultipleOfLotSize, invalidResult.RejectionReason);
        AssertStateIsUnchanged(stateBeforeInvalidCommand, engine);

        // Act
        var aggressiveResult = engine.PlaceOrder(
            GetLimitCommand(OrderSide.Buy, 50, 110m));
        
        // Assert
        var aggressiveOrder = GetResultOrder(aggressiveResult);
        Assert.Collection(aggressiveResult.Trades,
            firstTrade =>
            {
                Assert.Equal(firstBestOrder.Id, firstTrade.SellOrderId);
                Assert.Equal(100m, firstTrade.Price);
                Assert.Equal(10, firstTrade.Size);
            },
            secondTrade =>
            {
                Assert.Equal(secondBestOrder.Id, secondTrade.SellOrderId);
                Assert.Equal(100m, secondTrade.Price);
                Assert.Equal(20, secondTrade.Size);
            },
            thirdTrade =>
            {
                Assert.Equal(nextLevelOrder.Id, thirdTrade.SellOrderId);
                Assert.Equal(110m, thirdTrade.Price);
                Assert.Equal(20, thirdTrade.Size);
            });
        
        Assert.All(aggressiveResult.Trades, trade => Assert.Equal(aggressiveOrder.Id, trade.BuyOrderId));
        Assert.Equal(aggressiveResult.Trades, engine.GetTrades());

        var storedAggressiveOrder = GetStoredOrder(engine, aggressiveOrder.Id);
        var storedFirstBestOrder = GetStoredOrder(engine, firstBestOrder.Id);
        var storedSecondBestOrder = GetStoredOrder(engine, secondBestOrder.Id);
        var storedNextLevelOrder = GetStoredOrder(engine, nextLevelOrder.Id);

        Assert.Equal(OrderStatus.Filled, storedAggressiveOrder.OrderStatus);
        Assert.Equal(OrderStatus.Filled, storedFirstBestOrder.OrderStatus);
        Assert.Equal(OrderStatus.Filled, storedSecondBestOrder.OrderStatus);
        Assert.Equal(OrderStatus.Active, storedNextLevelOrder.OrderStatus);
        Assert.Equal(20, storedNextLevelOrder.FilledSize);
        Assert.Equal(10, storedNextLevelOrder.RemainingSize);

        var bookAfterMatching = engine.GetOrderBookSnapshot();
        Assert.Empty(bookAfterMatching.Bids);
        Assert.Equal([new OrderBookLevel(110m, 10)], bookAfterMatching.Asks);
        Assert.Equal(storedNextLevelOrder, Assert.Single(engine.GetActiveOrders()));
        Assert.Equal(110m, engine.GetReferencePrice());

        Assert.All(
            [
                storedAggressiveOrder,
                storedFirstBestOrder,
                storedSecondBestOrder,
                storedNextLevelOrder
            ],
            order => Assert.Equal(order.Size, order.FilledSize + order.RemainingSize));

        // Act
        var cancelResult = engine.CancelOrder(nextLevelOrder.Id);

        // Assert
        var cancelledOrder = GetResultOrder(cancelResult);
        Assert.True(cancelResult.IsSuccess);
        Assert.Equal(OrderStatus.Cancelled, cancelledOrder.OrderStatus);
        Assert.Equal(20, cancelledOrder.FilledSize);
        Assert.Equal(10, cancelledOrder.RemainingSize);
        Assert.Empty(engine.GetActiveOrders());
        Assert.Empty(engine.GetOrderBookSnapshot().Bids);
        Assert.Empty(engine.GetOrderBookSnapshot().Asks);
        Assert.Equal(aggressiveResult.Trades, engine.GetTrades());
    }
}