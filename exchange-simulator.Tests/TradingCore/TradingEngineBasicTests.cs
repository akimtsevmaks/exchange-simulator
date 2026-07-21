using exchange_simulator.Enums;
using exchange_simulator.Models;
using exchange_simulator.Models.TradingCore;

namespace exchange_simulator.Tests.TradingCore;

public class TradingEngineBasicTests : TradingEngineTestBase
{
    [Fact]
    public void NewEngine_ShouldStartEmpty()
    {
        // Arrange
        var instrument = GetTestInstrument();
        
        // Act 
        var engine = new TradingEngine(instrument);
        
        // Assert
        var orderBookSnapshot = engine.GetOrderBookSnapshot();
        
        Assert.Empty(orderBookSnapshot.Bids);
        Assert.Empty(orderBookSnapshot.Asks);
        Assert.Empty(engine.GetTrades());
        Assert.Empty(engine.GetActiveOrders());
    }

    [Fact]
    public void Ctor_ShouldUseProvidedInstrument()
    {
        // Arrange
        var instrument = GetTestInstrument();
        
        // Act
        var engine = new TradingEngine(instrument);
        
        // Assert
        Assert.Same(instrument, engine.Instrument);
        Assert.Equal(instrument.Id, engine.GetOrderBookSnapshot().InstrumentId);
    }

    [Fact]
    public void TryGetOrder_ShouldReturnFalseAndNull_WhenOrderDoesNotExist()
    {
        // Arrange
        var engine = new TradingEngine(GetTestInstrument());
        
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
        
        // Assert
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
        var engine = new TradingEngine(GetTestInstrument());

        const long size = 10;
        const decimal price = 100m;

        // Act
        var result = engine.PlaceOrder(GetLimitCommand(side, size, price));

        // Assert
        var order = GetResultOrder(result);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Trades);
        Assert.Equal(OrderStatus.Active, order.OrderStatus);

        var level = new OrderBookLevel(price, size);
        var orderBook = engine.GetOrderBookSnapshot();

        Assert.Equal(side == OrderSide.Buy ? [level] : [], orderBook.Bids);
        Assert.Equal(side == OrderSide.Sell ? [level] : [], orderBook.Asks);
    }
    
    [Theory]
    [InlineData(OrderSide.Buy)]
    [InlineData(OrderSide.Sell)]
    public void PlaceOppositeOrders_ShouldRemainActive_WhenPricesDoNotCross(OrderSide incomingSide)
    {
        // Arrange
        var engine = new TradingEngine(GetTestInstrument());

        var restingSide = GetOppositeSide(incomingSide);
        var restingPrice = incomingSide == OrderSide.Buy ? 200m : 100m;
        var incomingPrice = incomingSide == OrderSide.Buy ? 100m : 200m;

        // Act
        engine.PlaceOrder(GetLimitCommand(restingSide, price: restingPrice));
        var incomingResult = engine.PlaceOrder(GetLimitCommand(incomingSide, price: incomingPrice));

        // Assert
        Assert.Empty(incomingResult.Trades);
        Assert.Equal(2, engine.GetActiveOrders().Count);

        var orderBook = engine.GetOrderBookSnapshot();

        Assert.Equal([new OrderBookLevel(100m, 10)], orderBook.Bids);
        Assert.Equal([new OrderBookLevel(200m, 10)], orderBook.Asks);
    }
    
    [Theory]
    [InlineData(OrderSide.Buy)]
    [InlineData(OrderSide.Sell)]
    public void PlaceSameSideLimitOrders_ShouldNotMatch_AndShouldAggregateLevel(OrderSide side)
    {
        // Arrange
        var engine = new TradingEngine(GetTestInstrument());

        // Act
        engine.PlaceOrder(GetLimitCommand(side, 10, 100m));
        var secondResult = engine.PlaceOrder(GetLimitCommand(side, 20, 100m));

        // Assert
        Assert.Empty(secondResult.Trades);

        var orderBook = engine.GetOrderBookSnapshot();
        var expectedLevel = new OrderBookLevel(100m, 30);

        Assert.Equal(side == OrderSide.Buy ? [expectedLevel] : [], orderBook.Bids);
        Assert.Equal(side == OrderSide.Sell ? [expectedLevel] : [], orderBook.Asks);
    }
    
    [Theory]
    [InlineData(OrderSide.Buy)]
    [InlineData(OrderSide.Sell)]
    public void PlaceOrder_ShouldPreserveFractionalPrice_WhenPriceIsPositive(OrderSide side)
    {
        // Arrange
        var engine = new TradingEngine(GetTestInstrument());

        const decimal price = 100.123m;

        // Act
        engine.PlaceOrder(GetLimitCommand(side, price: price));

        // Assert
        var orderBook = engine.GetOrderBookSnapshot();
        var level = Assert.Single(side == OrderSide.Buy ? orderBook.Bids : orderBook.Asks);

        Assert.Equal(price, level.Price);
    }
}