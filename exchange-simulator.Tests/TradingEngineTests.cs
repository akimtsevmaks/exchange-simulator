using exchange_simulator.Enums;
using exchange_simulator.Models;

namespace exchange_simulator.Tests;

public class TradingEngineTests
{
    private static Instrument GetTestInstrument() => 
        new Instrument( Guid.NewGuid(), "TEST", "Test Instrument", 10, 100);
    
    [Fact]
    public void NewEngine_ShouldHaveCorrectInitialState()
    {
        // Arrange
        var instrument = GetTestInstrument();
        
        // Act 
        var newEngine = new TradingEngine(instrument);

        // Assert
        var orderBookSnapshot = newEngine.GetOrderBookSnapshot();
        
        Assert.Empty(orderBookSnapshot.Bids);
        Assert.Empty(orderBookSnapshot.Asks);
        Assert.Empty(newEngine.GetTrades());
        Assert.Empty(newEngine.GetActiveOrders());
        
        Assert.Equal(instrument.InitialPrice, newEngine.GetReferencePrice());
        Assert.Equal(instrument.Id, orderBookSnapshot.InstrumentId);
        Assert.Equal(instrument.Id, newEngine.Instrument.Id);
    }

    [Fact]
    public void TryGetOrder_ShouldReturnFalseAndNull_WhenOrderDoesNotExist()
    {
        // Arrange
        var instrument = GetTestInstrument();
        var engine = new TradingEngine(instrument);
        
        // Act
        var isFound = engine.TryGetOrder(Guid.NewGuid(), out var orderSnapshot);
        
        // Assert
        Assert.False(isFound);
        Assert.Null(orderSnapshot);
    }
    
    [Fact]
    public void Ctor_ShouldThrow_WhenInstrumentIsNull()
    {
        // Act
        var act = () =>
            new TradingEngine(null!);
        
        //Assert
        Assert.Throws<ArgumentNullException>(act);
    }

    [Fact]
    public void PlaceOrder_ShouldThrow_WhenCommandIsNull()
    {
        // Arrange
        var engine = new TradingEngine(GetTestInstrument());
        
        // Act
        var act = () =>
            engine.PlaceOrder(null!);

        // Assert
        Assert.Throws<ArgumentNullException>(act); 
    }

    [Theory]
    [InlineData(OrderSide.Buy)]
    [InlineData(OrderSide.Sell)]
    public void PlaceOrder_ShouldCreateActiveLimitOrder_WhenOrderBookIsEmpty(OrderSide side)
    {
        // Arrange
        var engine =  new TradingEngine(GetTestInstrument()); 
        
        const long size = 10;
        var command = new PlaceOrderCommand(Guid.NewGuid(), side, OrderType.Limit, size, 100);
        
        // Act
        var result = engine.PlaceOrder(command);
        
        // Assert
        Assert.True(result.IsSuccess);
        Assert.Null(result.RejectionReason);
        Assert.NotNull(result.Order);
        Assert.Empty(result.Trades);
        
        Assert.Equal(size, result.Order.Size);
        Assert.Equal(size, result.Order.RemainingSize);
        Assert.Equal(0, result.Order.FilledSize);
        
        Assert.Equal(OrderStatus.Active, result.Order.OrderStatus);
        
        var orderBookSnapshot = engine.GetOrderBookSnapshot();
        var sameLevels = side == OrderSide.Sell ? orderBookSnapshot.Asks :  orderBookSnapshot.Bids;
        var oppositeLevels = side == OrderSide.Buy ? orderBookSnapshot.Asks :  orderBookSnapshot.Bids;
        
        var sameLevel = Assert.Single(sameLevels);
        Assert.Empty(oppositeLevels);
        
        Assert.Equal(100, sameLevel.Price);
        Assert.Equal(size, sameLevel.Size);
        
        var activeOrder = Assert.Single(engine.GetActiveOrders());
        Assert.Equal(result.Order.Id, activeOrder.Id);
    }

    [Theory]
    [InlineData(OrderSide.Buy)]
    [InlineData(OrderSide.Sell)]
    public void PlaceOppositeOrders_ShouldRemainActive_WhenPriceDoNotCross(OrderSide side)
    {
        // Arrange
        var engine =  new TradingEngine(GetTestInstrument());
        
        var restingSide = side == OrderSide.Buy ? OrderSide.Sell : OrderSide.Buy;
        var restingPrice = side == OrderSide.Buy ? 200 : 100;
        var incomingPrice = side == OrderSide.Buy ? 100 : 200;
        
        var restingCommand = new PlaceOrderCommand(Guid.NewGuid(), restingSide, OrderType.Limit, 10, restingPrice);
        var incomingCommand = new PlaceOrderCommand(Guid.NewGuid(), side, OrderType.Limit, 10, incomingPrice);
        
        // Act
        var restingResult = engine.PlaceOrder(restingCommand);
        var incomingResult = engine.PlaceOrder(incomingCommand);
        
        // Assert
        Assert.True(restingResult.IsSuccess);
        Assert.True(incomingResult.IsSuccess);
        
        Assert.Empty(incomingResult.Trades);
        Assert.Empty(engine.GetTrades());
        
        var activeOrders = engine.GetActiveOrders();
        
        Assert.Equal(2, activeOrders.Count);
        Assert.All(activeOrders, order => Assert.Equal(OrderStatus.Active, order.OrderStatus));
        
        var snapshot = engine.GetOrderBookSnapshot();
        
        var bid = Assert.Single(snapshot.Bids);
        var ask = Assert.Single(snapshot.Asks);
        
        Assert.Equal(100, bid.Price);
        Assert.Equal(10, bid.Size);
        
        Assert.Equal(200, ask.Price);
        Assert.Equal(10, ask.Size);
    }

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

    [Theory]
    [InlineData(OrderSide.Buy)]
    [InlineData(OrderSide.Sell)]
    public void PlaceSameSideLimitOrders_ShouldNotMatch(OrderSide side)
    {
        // Arrange
        var engine = new TradingEngine(GetTestInstrument());
        
        var firstCommand = new PlaceOrderCommand(Guid.NewGuid(), side, OrderType.Limit, 10, 100);
        var secondCommand = new PlaceOrderCommand(Guid.NewGuid(), side, OrderType.Limit, 20, 100);
        
        // Act
        var firstResult = engine.PlaceOrder(firstCommand);
        var secondResult = engine.PlaceOrder(secondCommand);
        
        // Assert
        Assert.True(firstResult.IsSuccess);
        Assert.True(secondResult.IsSuccess);
        
        Assert.Empty(firstResult.Trades);
        Assert.Empty(secondResult.Trades);
        Assert.Empty(engine.GetTrades());
        
        Assert.NotNull(firstResult.Order);
        Assert.NotNull(secondResult.Order);
        
        Assert.Equal(OrderStatus.Active, firstResult.Order.OrderStatus);
        Assert.Equal(OrderStatus.Active, secondResult.Order.OrderStatus);

        var activeOrders = engine.GetActiveOrders();
        Assert.Equal(2, activeOrders.Count);
        
        var snapshot = engine.GetOrderBookSnapshot();
        
        var sameSideLevels = side == OrderSide.Buy ? snapshot.Bids : snapshot.Asks;
        var oppositeSideLevels = side == OrderSide.Sell ? snapshot.Bids : snapshot.Asks;
        
        var level = Assert.Single(sameSideLevels);
        Assert.Empty(oppositeSideLevels);
        
        Assert.Equal(100, level.Price);
        Assert.Equal(30, level.Size);
    }
}