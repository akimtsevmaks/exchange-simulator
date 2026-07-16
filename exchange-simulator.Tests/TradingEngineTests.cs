using exchange_simulator.Enums;
using exchange_simulator.Models;

namespace exchange_simulator.Tests;

public class TradingEngineTests
{
    private static Instrument GetTestInstrument() => 
        new Instrument( Guid.NewGuid(), "TEST", "Test Instrument", 10, 100);
    
    [Theory]
    [InlineData(OrderSide.Buy)]
    [InlineData(OrderSide.Sell)]
    public void PlaceLimitOrder_ShouldMatch_WhenPriceEqualBestOppositePrice(OrderSide side)
    {
        // Arrange
        var engine =  new TradingEngine(GetTestInstrument());
        
        var oppositeSide = side == OrderSide.Buy ? OrderSide.Sell : OrderSide.Buy;
        var oppositeCommand = new PlaceOrderCommand(Guid.NewGuid(), oppositeSide, OrderType.Limit, 10, 100);
        var crossingCommand = new PlaceOrderCommand(Guid.NewGuid(), side, OrderType.Limit, 10, 100);
        
        // Act 
        var oppositeResult = engine.PlaceOrder(oppositeCommand);
        var crossingResult = engine.PlaceOrder(crossingCommand);
        
        // Assert
        Assert.True(oppositeResult.IsSuccess);
        Assert.True(crossingResult.IsSuccess);
        
        var trade = Assert.Single(crossingResult.Trades);
        Assert.Single(engine.GetTrades());
        
        Assert.Equal(100, trade.Price);
        Assert.Equal(10, trade.Size);
        
        Assert.True(engine.TryGetOrder(oppositeResult.Order!.Id, out var oppositeOrder));
        Assert.True(engine.TryGetOrder(crossingResult.Order!.Id, out var crossingOrder));
        
        Assert.Equal(OrderStatus.Filled, oppositeOrder!.OrderStatus);
        Assert.Equal(OrderStatus.Filled, crossingOrder!.OrderStatus);
        
        var snapshot = engine.GetOrderBookSnapshot();
        
        Assert.Empty(snapshot.Bids);
        Assert.Empty(snapshot.Asks);
        
        var expectedBuyOrderId = side == OrderSide.Buy ? 
            crossingResult.Order.Id : oppositeResult.Order.Id;
        var expectedSellOrderId = side == OrderSide.Sell ?
            crossingResult.Order.Id : oppositeResult.Order.Id;
        
        Assert.Equal(expectedBuyOrderId, trade.BuyOrderId);
        Assert.Equal(expectedSellOrderId, trade.SellOrderId);
    }

    [Theory]
    [InlineData(OrderSide.Buy)]
    [InlineData(OrderSide.Sell)]
    public void PlaceLimitOrder_ShouldRestRemainingQuantity_WhenPartiallyFilled(OrderSide side)
    {
        // Arrange
        var engine =  new TradingEngine(GetTestInstrument());
        
        var oppositeSide = side == OrderSide.Buy ? OrderSide.Sell : OrderSide.Buy;
        const decimal restingPrice = 100;
        var incomingPrice = side == OrderSide.Buy ? 110 : 90;
        
        var restingCommand = new PlaceOrderCommand(Guid.NewGuid(), oppositeSide, OrderType.Limit, 20, restingPrice);
        var incomingCommand = new PlaceOrderCommand(Guid.NewGuid(), side, OrderType.Limit, 30, incomingPrice);
        
        // Act
        var restingResult = engine.PlaceOrder(restingCommand);
        var incomingResult = engine.PlaceOrder(incomingCommand);
        
        // Assert
        Assert.True(restingResult.IsSuccess);
        Assert.True(incomingResult.IsSuccess);
        
        var trade = Assert.Single(incomingResult.Trades);
        Assert.Single(engine.GetTrades());
        
        Assert.Equal(restingPrice, trade.Price);
        Assert.Equal(20, trade.Size);
        
        Assert.True(engine.TryGetOrder(restingResult.Order!.Id, out var restingOrder));
        Assert.True(engine.TryGetOrder(incomingResult.Order!.Id, out var incomingOrder));
        
        Assert.Equal(OrderStatus.Filled, restingOrder!.OrderStatus);
        Assert.Equal(OrderStatus.Active, incomingOrder!.OrderStatus);
        
        Assert.Equal(20, restingOrder.Size);
        Assert.Equal(20, restingOrder.FilledSize);
        Assert.Equal(0, restingOrder.RemainingSize);
        
        Assert.Equal(30, incomingOrder.Size);
        Assert.Equal(20, incomingOrder.FilledSize);
        Assert.Equal(10, incomingOrder.RemainingSize);
        
        var snapshot = engine.GetOrderBookSnapshot();
        
        var remainingLevel = Assert.Single(side == OrderSide.Buy ? snapshot.Bids : snapshot.Asks);
        
        Assert.Empty(side == OrderSide.Buy ? snapshot.Asks : snapshot.Bids);
        
        Assert.Equal(incomingPrice, remainingLevel.Price);
        Assert.Equal(10, remainingLevel.Size);
    }
}