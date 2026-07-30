using exchange_simulator.Enums;
using exchange_simulator.Models.TradingCore;

namespace exchange_simulator.Tests.LocalMarket;

public class LocalMarketEndToEndTests : LocalMarketTestBase
{
    [Fact]
    public async Task ManualTradingWorkflow_ShouldMovePriceRequoteAndRemainConsistent()
    {
        // Arrange
        const decimal initialCashPerAccount = 100000m;
        const long initialInstrumentsPerAccount = 1000;
        var market = GetMarketWithScriptedNoiseBot(_ => { });

        PlaceLimit(market, market.NoiseBotAccountId, OrderSide.Sell, size: 10, price: 102m);
        PlaceLimit(market, market.NoiseBotAccountId, OrderSide.Sell, size: 10, price: 103m);

        // Act
        market.Step();

        // Assert
        Assert.Equal(
            [
                new OrderBookLevel(101m, 10),
                new OrderBookLevel(102m, 10),
                new OrderBookLevel(103m, 10)
            ],
            market.GetOrderBookSnapshot().Asks);
        Assert.Empty(market.GetTrades());

        // Act
        var manualMarketBuy = PlaceMarket(market, market.ManualAccountId, OrderSide.Buy, size: 25);

        // Assert
        Assert.True(manualMarketBuy.IsSuccess);
        Assert.Collection(
            manualMarketBuy.Trades,
            firstTrade =>
            {
                Assert.Equal(101m, firstTrade.Price);
                Assert.Equal(10, firstTrade.Size);
            },
            secondTrade =>
            {
                Assert.Equal(102m, secondTrade.Price);
                Assert.Equal(10, secondTrade.Size);
            },
            thirdTrade =>
            {
                Assert.Equal(103m, thirdTrade.Price);
                Assert.Equal(5, thirdTrade.Size);
            });
        Assert.Equal(103m, market.GetReferencePrice());

        // Act
        var noiseBotStopped = market.TryStopBot(market.NoiseBotAccountId);
        market.Step();

        // Assert
        Assert.True(noiseBotStopped);
        Assert.Empty(market.GetActiveOrders(market.NoiseBotAccountId));

        var marketMakerOrders =
            market.GetActiveOrders(market.MarketMakerAccountId);
        var marketMakerBid = Assert.Single(
            marketMakerOrders,
            order => order.OrderSide == OrderSide.Buy);
        var marketMakerAsk = Assert.Single(
            marketMakerOrders,
            order => order.OrderSide == OrderSide.Sell);

        Assert.Equal(102m, marketMakerBid.Price);
        Assert.Equal(104m, marketMakerAsk.Price);
        Assert.Equal(103m, market.GetReferencePrice());

        // Act
        var manualLimit = PlaceLimit(market, market.ManualAccountId, OrderSide.Buy, size: 10, price: 90m);
        var cancellation = market.CancelOrder(GetOrder(manualLimit).Id);
        market.Step();

        // Assert
        Assert.True(manualLimit.IsSuccess);
        Assert.True(cancellation.IsSuccess);
        Assert.Empty(market.GetActiveOrders(market.ManualAccountId));
        Assert.Equal(3, market.GetTrades().Count);
        AssertEconomicStateIsConsistent(
            market,
            expectedTotalCash: initialCashPerAccount * 3,
            expectedTotalInstruments: initialInstrumentsPerAccount * 3);

        // Act
        await market.StopAsync();

        // Assert
        Assert.Equal(LocalMarketStatus.Stopped, market.Status);
        Assert.Empty(market.GetActiveOrders(market.MarketMakerAccountId));
        Assert.Empty(market.GetActiveOrders(market.NoiseBotAccountId));
        Assert.Empty(market.GetActiveOrders(market.ManualAccountId));
    }
}
