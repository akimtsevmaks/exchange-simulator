using exchange_simulator.Enums;
using exchange_simulator.Models;

namespace exchange_simulator.Tests.TradingCore;

public class TradingEngineTradeTests : TradingEngineTestBase
{
    [Fact]
    public void GetTrades_ShouldPreserveHistoryOrder()
    {
        // Arrange
        var engine = new TradingEngine(GetTestInstrument());

        engine.PlaceOrder(GetLimitCommand(OrderSide.Sell, price: 100m));
        var firstTrade = Assert.Single(
            engine.PlaceOrder(GetMarketCommand(OrderSide.Buy)).Trades);

        engine.PlaceOrder(GetLimitCommand(OrderSide.Buy, price: 90m));
        var secondTrade = Assert.Single(
            engine.PlaceOrder(GetMarketCommand(OrderSide.Sell)).Trades);

        // Act
        var history = engine.GetTrades();

        // Assert
        Assert.Equal([firstTrade, secondTrade], history);
    }
    
    [Fact]
    public void PlaceOrder_ShouldReturnOnlyTradesCreatedDuringCurrentPlacement()
    {
        // Arrange
        var engine = new TradingEngine(GetTestInstrument());

        engine.PlaceOrder(GetLimitCommand(OrderSide.Sell));
        var previousTrade = Assert.Single(
            engine.PlaceOrder(GetMarketCommand(OrderSide.Buy)).Trades);

        engine.PlaceOrder(GetLimitCommand(OrderSide.Buy, price: 90m));

        // Act
        var result = engine.PlaceOrder(GetMarketCommand(OrderSide.Sell));

        // Assert
        var currentTrade = Assert.Single(result.Trades);

        Assert.NotEqual(previousTrade, currentTrade);
    }
    
    [Fact]
    public void PlaceOrder_ShouldCreateTradeWithCompleteExecutionData()
    {
        // Arrange
        var instrument = GetTestInstrument();
        var engine = new TradingEngine(instrument);
        var sellOrder = GetResultOrder(engine.PlaceOrder(GetLimitCommand(OrderSide.Sell)));

        // Act
        var buyResult = engine.PlaceOrder(GetMarketCommand(OrderSide.Buy));

        // Assert
        var buyOrder = GetResultOrder(buyResult);
        var trade = Assert.Single(buyResult.Trades);

        Assert.NotEqual(Guid.Empty, trade.Id);
        Assert.Equal(instrument.Id, trade.InstrumentId);
        Assert.Equal(buyOrder.Id, trade.BuyOrderId);
        Assert.Equal(sellOrder.Id, trade.SellOrderId);
        Assert.Equal(100m, trade.Price);
        Assert.Equal(10, trade.Size);
        Assert.NotEqual(default, trade.ExecutedAt);
    }
    
    [Fact]
    public void PlaceOrder_ShouldAllowSelfTrade_WhenOwnersAreEqual()
    {
        // Arrange
        var engine = new TradingEngine(GetTestInstrument());
        var ownerId = Guid.NewGuid();

        engine.PlaceOrder(GetLimitCommand(OrderSide.Sell, ownerId: ownerId));

        // Act
        var incomingResult = engine.PlaceOrder(GetMarketCommand(OrderSide.Buy, ownerId: ownerId));

        // Assert
        Assert.True(incomingResult.IsSuccess);
        var trade = Assert.Single(incomingResult.Trades);
        
        Assert.Equal(
            GetStoredOrder(engine, trade.BuyOrderId).OwnerId,
            GetStoredOrder(engine, trade.SellOrderId).OwnerId);
    }
}