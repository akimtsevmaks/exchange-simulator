namespace exchange_simulator.Tests.LocalMarket;

public class LocalMarketInvariantTests : LocalMarketTestBase
{
    [Fact]
    public void LongDiscreteRun_ShouldPreserveTradingAndAccountingInvariants()
    {
        // Arrange
        const decimal initialCashPerAccount = 100000m;
        const long initialInstrumentsPerAccount = 1000;
        
        var market = GetMarket(initialCash: initialCashPerAccount, initialInstruments: initialInstrumentsPerAccount);

        // Act
        for (var step = 0; step < 1_000; step++)
            market.Step();

        // Assert
        AssertEconomicStateIsConsistent(
            market,
            expectedTotalCash: initialCashPerAccount * 3,
            expectedTotalInstruments: initialInstrumentsPerAccount * 3);

        Assert.InRange(
            market.GetActiveOrders(market.MarketMakerAccountId).Count,
            0, 2);
        Assert.InRange(
            market.GetActiveOrders(market.NoiseBotAccountId).Count,
            0, 10);
        
        Assert.Empty(market.GetBotFailures());
        
        Assert.All(
            market.GetTrades(),
            trade =>
            {
                Assert.True(trade.Price > 0);
                Assert.True(trade.Size > 0);
            });

        var trades = market.GetTrades();
        var expectedReferencePrice = trades.Count == 0
            ? market.Instrument.InitialPrice : trades[^1].Price;

        Assert.Equal(expectedReferencePrice, market.GetReferencePrice());
    }
}