using exchange_simulator.Models;

namespace exchange_simulator.Tests;

public class InstrumentTests
{
    private static Instrument GetTestInstrument(
        Guid id,
        string ticker = "TEST",
        string name = "Test Instrument",
        long lotSize = 10,
        decimal initialPrice = 100m) =>
        new Instrument(id, ticker, name, lotSize, initialPrice);
    
    [Fact]
    public void Create_ShouldInitializeProperties()
    {
        // Arrange
        var id = Guid.NewGuid();
        const string ticker = "TEST";
        const string name = "Test Instrument";
        const long lotSize = 10;
        const decimal initialPrice = 100m;
        
        // Act
        var instrument = new Instrument(id, ticker, name, lotSize, initialPrice);
        
        // Assert
        Assert.Equal(id, instrument.Id);
        Assert.Equal(ticker, instrument.Ticker);
        Assert.Equal(name, instrument.Name);
        Assert.Equal(lotSize, instrument.LotSize);
        Assert.Equal(initialPrice, instrument.InitialPrice);
    }

    [Fact]
    public void Ctor_ShouldThrow_WhenIdIsEmpty()
    {
        // Arrange
        var errId = Guid.Empty;
        
        // Act
        var act = () =>
            GetTestInstrument(errId);
        
        // Assert
        Assert.Throws<ArgumentException>(act);
    }

    [Fact]
    public void Ctor_ShouldThrow_WhenTickerIsNull()
    {
        // Act
        var act = () =>
            GetTestInstrument(Guid.NewGuid(), ticker: null!);
        
        // Assert
        Assert.Throws<ArgumentNullException>(act);
    }
    
    [Theory]
    [InlineData("")]
    [InlineData("ABC")]
    [InlineData("ABCDE")]
    public void Ctor_ShouldThrow_WhenTickerIsIncorrect(string errTicker)
    {
        // Act
        var act = () =>
            GetTestInstrument(Guid.NewGuid(), ticker: errTicker);
        
        // Assert
        Assert.Throws<ArgumentException>(act);
    }

    [Fact]
    public void Ctor_ShouldThrow_WhenNameIsNull()
    {
        // Act
        var act = () =>
            GetTestInstrument(Guid.NewGuid(), name: null!);
        
        // Assert
        Assert.Throws<ArgumentNullException>(act);
    }

    public static TheoryData<string> InvalidNames =>
        ["", new string('A', 100)];
    
    [Theory]
    [MemberData(nameof(InvalidNames))]
    public void Ctor_ShouldThrow_WhenNameIsIncorrect(string errName)
    {
        // Act
        var act = () =>
            GetTestInstrument(Guid.NewGuid(), name: errName);
        
        // Assert
        Assert.Throws<ArgumentException>(act);
    }
    
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Ctor_ShouldThrow_WhenLotSizeIsNotPositive(int errLotSize)
    {
        // Act
        var act = () =>
            GetTestInstrument(Guid.NewGuid(), lotSize: errLotSize);
        
        // Assert
        Assert.Throws<ArgumentOutOfRangeException>(act);
    }
    
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Ctor_ShouldThrow_WhenPriceIsNotPositive(decimal errInitialPrice)
    {
        // Act
        var act = () =>
            GetTestInstrument(Guid.NewGuid(), initialPrice: errInitialPrice);
        
        // Assert
        Assert.Throws<ArgumentOutOfRangeException>(act);
    }

    [Fact]
    public void Ctor_ShouldAcceptNameAtMaximumLength()
    {
        // Arrange
        var maxLengthName = new string('A', 99);
        
        // Act
        var result = GetTestInstrument(Guid.NewGuid(), name: maxLengthName);
        
        // Assert
        Assert.Equal(maxLengthName, result.Name);
    }
}