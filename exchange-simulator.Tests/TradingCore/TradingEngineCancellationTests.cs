using exchange_simulator.Enums;
using exchange_simulator.Models;
using exchange_simulator.Models.TradingCore;

namespace exchange_simulator.Tests.TradingCore;

public class TradingEngineCancellationTests : TradingEngineTestBase
{
    [Theory]
    [InlineData(OrderSide.Buy)]
    [InlineData(OrderSide.Sell)]
    public void CancelOrder_ShouldCancelActiveOrder_AndRemoveItFromOrderBook(OrderSide side)
    {
        // Arrange
        var engine = new TradingEngine(GetTestInstrument());
        var placedOrder = GetResultOrder(engine.PlaceOrder(GetLimitCommand(side, 20)));
        
        // Act
        var cancelResult = engine.CancelOrder(placedOrder.Id);
        
        // Assert
        Assert.True(cancelResult.IsSuccess);
        Assert.Null(cancelResult.RejectionReason);
        Assert.Empty(cancelResult.Trades);
        
        var cancelledOrder = GetResultOrder(cancelResult);
        
        Assert.Equal(OrderStatus.Cancelled, cancelledOrder.OrderStatus);
        Assert.Equal(cancelledOrder, GetStoredOrder(engine, placedOrder.Id));

        var orderBook = engine.GetOrderBookSnapshot();
        
        Assert.Empty(side ==  OrderSide.Buy ? orderBook.Bids : orderBook.Asks);
    }

    [Theory]
    [InlineData(OrderSide.Buy)]
    [InlineData(OrderSide.Sell)]
    public void CancelOrder_ShouldKeepExecutedQuantity_WhenOrderIsPartiallyFilled(OrderSide restingSide)
    {
        // Arrange
        var engine = new TradingEngine(GetTestInstrument());
        var restingOrder = GetResultOrder(engine.PlaceOrder(GetLimitCommand(restingSide, 30)));
        var incomingSide = GetOppositeSide(restingSide);
        
        engine.PlaceOrder(GetMarketCommand(incomingSide, 10));
        
        // Act
        var cancelResult = engine.CancelOrder(restingOrder.Id);
        
        // Assert
        var cancelledOrder = GetResultOrder(cancelResult);
        
        Assert.Equal(OrderStatus.Cancelled, cancelledOrder.OrderStatus);
        Assert.Equal(10, cancelledOrder.FilledSize);
        Assert.Equal(20, cancelledOrder.RemainingSize);
    }
    
    [Fact]
    public void CancelOrder_ShouldReturnOrderNotFound_WithoutSideEffects_WhenOrderDoesNotExist()
    {
        // Arrange
        var engine = new TradingEngine(GetTestInstrument());
        engine.PlaceOrder(GetLimitCommand(OrderSide.Buy));
        
        var stateBefore = CaptureState(engine);

        // Act
        var result = engine.CancelOrder(Guid.NewGuid());

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(OrderRejectionReason.OrderNotFound, result.RejectionReason);
        Assert.Null(result.Order);
        Assert.Empty(result.Trades);
        
        AssertStateIsUnchanged(stateBefore, engine);
    }
    
    [Fact]
    public void CancelOrder_ShouldReturnOrderNotActive_WhenOrderIsFilled()
    {
        // Arrange
        var engine = new TradingEngine(GetTestInstrument());
        
        const OrderSide incomingSide = OrderSide.Buy;
        
        var restingOrder = GetResultOrder(engine.PlaceOrder(GetLimitCommand(GetOppositeSide(incomingSide))));

        engine.PlaceOrder(GetMarketCommand(incomingSide));

        // Act
        var result = engine.CancelOrder(restingOrder.Id);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(OrderRejectionReason.OrderNotActive, result.RejectionReason);
        Assert.Null(result.Order);
        Assert.Empty(result.Trades);
    }
    
    [Fact]
    public void CancelOrder_ShouldReturnOrderNotActive_WhenOrderWasAlreadyCancelled()
    {
        // Arrange
        var engine = new TradingEngine(GetTestInstrument());
        var placedOrder = GetResultOrder(engine.PlaceOrder(GetLimitCommand(OrderSide.Buy)));

        engine.CancelOrder(placedOrder.Id);

        // Act
        var secondCancelResult = engine.CancelOrder(placedOrder.Id);

        // Assert
        Assert.False(secondCancelResult.IsSuccess);
        Assert.Equal(OrderRejectionReason.OrderNotActive, secondCancelResult.RejectionReason);
    }
    
    [Theory]
    [InlineData(OrderSide.Buy)]
    [InlineData(OrderSide.Sell)]
    public void CancelOrder_ShouldReduceAndThenRemoveAggregatedPriceLevel(OrderSide side)
    {
        // Arrange
        var engine = new TradingEngine(GetTestInstrument());
        var firstOrder = GetResultOrder(engine.PlaceOrder(GetLimitCommand(side, 10)));
        var secondOrder = GetResultOrder(engine.PlaceOrder(GetLimitCommand(side, 20)));

        // Act
        engine.CancelOrder(firstOrder.Id);

        // Assert
        var snapshotAfterFirstCancel = engine.GetOrderBookSnapshot();
        var level = Assert.Single(side == OrderSide.Buy ? snapshotAfterFirstCancel.Bids : snapshotAfterFirstCancel.Asks);

        Assert.Equal(20, level.Size);

        // Act
        engine.CancelOrder(secondOrder.Id);

        // Assert
        var snapshotAfterSecondCancel = engine.GetOrderBookSnapshot();

        Assert.Empty(side == OrderSide.Buy ? snapshotAfterSecondCancel.Bids : snapshotAfterSecondCancel.Asks);
    }
}