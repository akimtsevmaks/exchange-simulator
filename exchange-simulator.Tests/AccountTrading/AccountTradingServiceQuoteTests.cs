using exchange_simulator.Enums;
using exchange_simulator.Services;

namespace exchange_simulator.Tests.AccountTrading;

public class AccountTradingServiceQuoteTests : AccountTradingServiceTestBase
{
    [Theory]
    [InlineData(0)]
    [InlineData(-10)]
    public void GetMarketBuyQuote_ShouldThrow_WhenRequestedSizeIsNotPositive(long requestedSize)
    {
        // Arrange
        var service = new AccountTradingService(GetAccountTestInstrument());

        // Act
        var act = () =>
            service.GetMarketBuyQuote(requestedSize);

        // Assert
        Assert.Throws<ArgumentOutOfRangeException>(act);
    }
    
    [Fact]
    public void GetMarketBuyQuote_ShouldThrow_WhenRequestedSizeIsNotMultipleOfLot()
    {
        // Arrange
        var service = new AccountTradingService(GetAccountTestInstrument(lotSize: 10));

        // Act
        var act = () =>
            service.GetMarketBuyQuote(15);

        // Assert
        Assert.Throws<ArgumentException>(act);
    }
    
    [Fact]
    public void GetMarketBuyQuote_ShouldReportEntireRequestAsUnfilled_WhenBookHasNoAsks()
    {
        // Arrange
        var service = new AccountTradingService(GetAccountTestInstrument());

        // Act
        var quote = service.GetMarketBuyQuote(30);

        // Assert
        Assert.Equal(30, quote.RequestedSize);
        Assert.Equal(0, quote.ExecutableSize);
        Assert.Equal(30, quote.UnfilledSize);
        Assert.Equal(0m, quote.Cost);
    }
    
    [Fact]
    public void GetMarketBuyQuote_ShouldUseBestAskLevelsUntilRequestedSizeIsReached()
    {
        // Arrange
        var service = new AccountTradingService(GetAccountTestInstrument());
        var firstSeller = RegisterAccount(service, instruments: 20);
        var secondSeller = RegisterAccount(service, instruments: 30);
        var thirdSeller = RegisterAccount(service, instruments: 10);
        PlaceLimit(service, secondSeller, OrderSide.Sell, 30, 100m);
        PlaceLimit(service, thirdSeller, OrderSide.Sell, 10, 120m);
        PlaceLimit(service, firstSeller, OrderSide.Sell, 20, 110m);

        // Act
        var quote = service.GetMarketBuyQuote(40);

        // Assert
        Assert.Equal(40, quote.RequestedSize);
        Assert.Equal(40, quote.ExecutableSize);
        Assert.Equal(0, quote.UnfilledSize);
        Assert.Equal(4100m, quote.Cost);
    }
    
    [Fact]
    public void GetMarketBuyQuote_ShouldReportUnfilledRemainder_WhenLiquidityIsInsufficient()
    {
        // Arrange
        var service = new AccountTradingService(GetAccountTestInstrument());
        var sellerId = RegisterAccount(service, instruments: 20);
        PlaceLimit(service, sellerId, OrderSide.Sell, 20, 100m);

        // Act
        var quote = service.GetMarketBuyQuote(50);

        // Assert
        Assert.Equal(20, quote.ExecutableSize);
        Assert.Equal(30, quote.UnfilledSize);
        Assert.Equal(2000m, quote.Cost);
    }
    
    [Fact]
    public void GetMarketBuyQuote_ShouldNotChangeOrdersOrReservations()
    {
        // Arrange
        var service = new AccountTradingService(GetAccountTestInstrument());
        var sellerId = RegisterAccount(service, instruments: 20);
        var sell = PlaceLimit(service, sellerId, OrderSide.Sell, 20, 100m);
        var accountBeforeQuote = GetAccount(service, sellerId);

        // Act
        service.GetMarketBuyQuote(10);

        // Assert
        Assert.Equal(accountBeforeQuote, GetAccount(service, sellerId));
        Assert.Equal(GetOrder(sell), Assert.Single(service.GetActiveOrders(sellerId)));
        Assert.Empty(service.GetAccountTrades(sellerId));
    }
}
