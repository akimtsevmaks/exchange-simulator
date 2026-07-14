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
            new TradingEngine(null);
        
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
            engine.PlaceOrder(null);

        // Assert
        Assert.Throws<ArgumentNullException>(act); 
    }
}