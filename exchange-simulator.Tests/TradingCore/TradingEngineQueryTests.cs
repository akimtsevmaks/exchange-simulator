using exchange_simulator.Enums;
using exchange_simulator.Models;

namespace exchange_simulator.Tests.TradingCore;

public class TradingEngineQueryTests : TradingEngineTestBase
{
    [Theory]
    [InlineData(OrderSide.Buy)]
    [InlineData(OrderSide.Sell)]
    public void TryGetOrder_ShouldReturnCompleteOrderSnapshot_WhenOrderExists(OrderSide side)
    {
        // Arrange
        var instrument = GetTestInstrument();
        var engine = new TradingEngine(instrument);
        var ownerId = Guid.NewGuid();
        const long size = 20;
        const decimal price = 123.45m;

        var placedOrder = GetResultOrder(engine.PlaceOrder(GetLimitCommand(side, size, price, ownerId)));
        
        // Act
        var isFound = engine.TryGetOrder(placedOrder.Id, out var storedOrder);
        
        // Assert
        Assert.True(isFound);
        
        var snapshot = Assert.IsType<OrderSnapshot>(storedOrder);
        
        Assert.NotEqual(default, snapshot.CreatedAt);
        Assert.Equal(placedOrder.Id, snapshot.Id);
        Assert.Equal(ownerId, snapshot.OwnerId);
        Assert.Equal(instrument.Id, snapshot.InstrumentId);
        Assert.Equal(OrderType.Limit, snapshot.OrderType);
        Assert.Equal(side, snapshot.OrderSide);
        Assert.Equal(OrderStatus.Active, snapshot.OrderStatus);
        Assert.Equal(price, snapshot.Price);
        Assert.Equal(size, snapshot.Size);
        Assert.Equal(size, snapshot.RemainingSize);
        Assert.Equal(0, snapshot.FilledSize);
        Assert.Equal(placedOrder.CreatedAt, snapshot.CreatedAt);
    }

    [Fact]
    public void ReturnedSnapshots_ShouldRemainUnchanged_WhenEngineStateChanges()
    {
        // Arrange
        var engine = new TradingEngine(GetTestInstrument());
        
        var returnedOrderBefore = GetResultOrder(engine.PlaceOrder(GetLimitCommand(OrderSide.Buy)));
        engine.TryGetOrder(returnedOrderBefore.Id, out var queriedOrder);
        
        var queriedOrderBefore = Assert.IsType<OrderSnapshot>(queriedOrder);
        var orderBookBefore = engine.GetOrderBookSnapshot();
        var activeOrdersBefore = engine.GetActiveOrders();
        var tradesBefore = engine.GetTrades();
        
        // Act
        engine.PlaceOrder(GetMarketCommand(OrderSide.Sell));
        
        // Assert
        var storedOrderAfter = GetStoredOrder(engine, returnedOrderBefore.Id);
        
        Assert.Equal(OrderStatus.Active, returnedOrderBefore.OrderStatus);
        Assert.Equal(OrderStatus.Active, queriedOrderBefore.OrderStatus);
        Assert.Equal([new OrderBookLevel(100m, 10)], orderBookBefore.Bids);
        Assert.Equal([returnedOrderBefore], activeOrdersBefore);
        Assert.Empty(tradesBefore);
        
        Assert.Equal(OrderStatus.Filled, storedOrderAfter.OrderStatus);
        Assert.Empty(engine.GetOrderBookSnapshot().Bids);
        Assert.Empty(engine.GetActiveOrders());
        Assert.Single(engine.GetTrades());
    }
    
    [Fact]
    public void GetOrderBookSnapshot_ShouldSortAndAggregateBothSides()
    {
        // Arrange
        var engine = new TradingEngine(GetTestInstrument());

        engine.PlaceOrder(GetLimitCommand(OrderSide.Buy, 10, 90m));
        engine.PlaceOrder(GetLimitCommand(OrderSide.Buy, 10, 100m));
        engine.PlaceOrder(GetLimitCommand(OrderSide.Buy, 20, 100m));

        engine.PlaceOrder(GetLimitCommand(OrderSide.Sell, 20, 120m));
        engine.PlaceOrder(GetLimitCommand(OrderSide.Sell, 10, 110m));
        engine.PlaceOrder(GetLimitCommand(OrderSide.Sell, 10, 120m));

        // Act
        var snapshot = engine.GetOrderBookSnapshot();

        // Assert
        Assert.Equal(
            [new OrderBookLevel(100m, 30), new OrderBookLevel(90m, 10)],
            snapshot.Bids);
        Assert.Equal(
            [new OrderBookLevel(110m, 10), new OrderBookLevel(120m, 30)],
            snapshot.Asks);
    }
    
    [Fact]
    public void GetActiveOrders_ShouldReturnOnlyActiveOrders()
    {
        // Arrange
        var engine = new TradingEngine(GetTestInstrument());

        var bid = GetResultOrder(engine.PlaceOrder(GetLimitCommand(OrderSide.Buy, price: 90m)));
        var activeAsk = GetResultOrder(engine.PlaceOrder(GetLimitCommand(OrderSide.Sell, price: 110m)));
        engine.PlaceOrder(GetLimitCommand(OrderSide.Sell, price: 100m));

        engine.PlaceOrder(GetMarketCommand(OrderSide.Buy));
        engine.CancelOrder(bid.Id);

        // Act
        var activeOrders = engine.GetActiveOrders();

        // Assert
        Assert.Equal(activeAsk.Id, Assert.Single(activeOrders).Id);
    }
}