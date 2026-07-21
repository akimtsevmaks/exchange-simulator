using exchange_simulator.Enums;
using exchange_simulator.Models;

namespace exchange_simulator.Tests.TradingCore;

public class TradingEngineReferencePriceTests : TradingEngineTestBase
{
    [Fact]
    public void GetReferencePrice_ShouldReturnInitialPrice_WhenNoTradesHaveOccurred()
    {
        // Arrange
        const decimal initialPrice = 123.45m;
        var engine = new TradingEngine(GetTestInstrument(initialPrice: initialPrice));

        engine.PlaceOrder(GetLimitCommand(OrderSide.Buy, price: 90m));
        engine.PlaceOrder(GetLimitCommand(OrderSide.Sell, price: 110m));
        
        // Act
        var referencePrice = engine.GetReferencePrice();
        
        // Assert
        Assert.Equal(initialPrice, referencePrice);
    }

    [Fact]
    public void GetReferencePrice_ShouldReturnPriceOfLastTrade()
    {
        // Arrange
        var engine = new TradingEngine(GetTestInstrument(initialPrice: 50m));
        const decimal firstPrice = 90m;
        const decimal lastPrice = 100m;
        
        engine.PlaceOrder(GetLimitCommand(OrderSide.Sell, price: firstPrice));
        engine.PlaceOrder(GetLimitCommand(OrderSide.Sell, price: lastPrice));
        engine.PlaceOrder(GetMarketCommand(OrderSide.Buy, size: 20));
        
        // Act
        var referencePrice = engine.GetReferencePrice();
        
        // Assert
        Assert.Equal(lastPrice, referencePrice);

    }
}