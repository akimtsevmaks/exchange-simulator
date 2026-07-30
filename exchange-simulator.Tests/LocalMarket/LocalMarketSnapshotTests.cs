using exchange_simulator.Enums;

namespace exchange_simulator.Tests.LocalMarket;

public class LocalMarketSnapshotTests : LocalMarketTestBase
{
    [Fact]
    public void GetSnapshot_ShouldContainMarketAndManualParticipantState()
    {
        // Arrange
        var market = GetMarketWithScriptedNoiseBot(_ => { });
        market.Step();
        
        var manualOrder = PlaceLimit(market, market.ManualAccountId, OrderSide.Buy, size: 10, price: 90m);

        // Act
        var snapshot = market.GetSnapshot();

        // Assert
        var expectedOrderBook = market.GetOrderBookSnapshot();

        Assert.Equal(
            expectedOrderBook.InstrumentId,
            snapshot.OrderBook.InstrumentId);
        Assert.Equal(expectedOrderBook.Bids, snapshot.OrderBook.Bids);
        Assert.Equal(expectedOrderBook.Asks, snapshot.OrderBook.Asks);
        Assert.Equal(market.GetReferencePrice(), snapshot.ReferencePrice);
        Assert.Equal(market.GetTrades(), snapshot.Trades);
        Assert.Equal(
            GetAccount(market, market.ManualAccountId),
            snapshot.ManualAccount);
        Assert.Equal(
            GetOrder(manualOrder),
            Assert.Single(snapshot.ManualActiveOrders));
    }

    [Fact]
    public void GetSnapshot_ShouldRemainUnchangedAfterLaterMarketCommand()
    {
        // Arrange
        var market = GetMarket();
        var manualOrder = PlaceLimit(market, market.ManualAccountId, OrderSide.Buy, size: 10, price: 90m);
        var snapshotBeforeCancellation = market.GetSnapshot();

        // Act
        market.CancelOrder(GetOrder(manualOrder).Id);

        // Assert
        Assert.Single(snapshotBeforeCancellation.ManualActiveOrders);
        Assert.Empty(market.GetSnapshot().ManualActiveOrders);
    }
}