using exchange_simulator.Enums;
using exchange_simulator.Services;

namespace exchange_simulator.Tests.AccountTrading;

public class AccountTradingServiceHistoryTests : AccountTradingServiceTestBase
{
    [Fact]
    public void ActiveOrderHistory_ShouldContainAcceptedAndActivatedEvents()
    {
        // Arrange
        var service = new AccountTradingService(GetAccountTestInstrument());
        var buyerId = RegisterAccount(service, cash: 1000m);

        // Act
        var result = PlaceLimit(service, buyerId, OrderSide.Buy, 10, 100m);

        // Assert
        var order = GetOrder(result);
        var history = service.GetOrderHistory(order.Id);
        Assert.Collection(history,
            accepted =>
            {
                Assert.Equal(OrderHistoryEventType.Accepted, accepted.EventType);
                Assert.Equal(0, accepted.FilledSize);
                Assert.Equal(10, accepted.RemainingSize);
                Assert.Null(accepted.TradeId);
                Assert.Equal(order.CreatedAt, accepted.OccurredAt);
            },
            activated =>
            {
                Assert.Equal(OrderHistoryEventType.Activated, activated.EventType);
                Assert.Equal(0, activated.FilledSize);
                Assert.Equal(10, activated.RemainingSize);
                Assert.Null(activated.TradeId);
            });
    }
    
    [Fact]
    public void FilledIncomingOrderHistory_ShouldContainAcceptedAndFilledEvents()
    {
        // Arrange
        var service = new AccountTradingService(GetAccountTestInstrument());
        var sellerId = RegisterAccount(service, instruments: 10);
        var buyerId = RegisterAccount(service, cash: 1000m);
        PlaceLimit(service, sellerId, OrderSide.Sell, 10, 100m);

        // Act
        var result = PlaceMarket(service, buyerId, OrderSide.Buy, 10);

        // Assert
        var order = GetOrder(result);
        var trade = Assert.Single(result.Trades);
        Assert.Collection(service.GetOrderHistory(order.Id),
            accepted =>
                Assert.Equal(OrderHistoryEventType.Accepted, accepted.EventType),
            filled =>
            {
                Assert.Equal(OrderHistoryEventType.Filled, filled.EventType);
                Assert.Equal(10, filled.FilledSize);
                Assert.Equal(0, filled.RemainingSize);
                Assert.Equal(trade.Id, filled.TradeId);
                Assert.Equal(trade.ExecutedAt, filled.OccurredAt);
            });
    }
    
    [Fact]
    public void RestingOrderHistory_ShouldAccumulatePartialAndFullFills()
    {
        // Arrange
        var service = new AccountTradingService(GetAccountTestInstrument());
        var sellerId = RegisterAccount(service, instruments: 30);
        var firstBuyer = RegisterAccount(service, cash: 1000m);
        var secondBuyer = RegisterAccount(service, cash: 2000m);
        var sell = PlaceLimit(service, sellerId, OrderSide.Sell, 30, 100m);

        // Act
        var firstBuy = PlaceMarket(service, firstBuyer, OrderSide.Buy, 10);
        var secondBuy = PlaceMarket(service, secondBuyer, OrderSide.Buy, 20);

        // Assert
        var firstTrade = Assert.Single(firstBuy.Trades);
        var secondTrade = Assert.Single(secondBuy.Trades);
        Assert.Collection(service.GetOrderHistory(GetOrder(sell).Id),
            accepted =>
                Assert.Equal(OrderHistoryEventType.Accepted, accepted.EventType),
            activated =>
                Assert.Equal(OrderHistoryEventType.Activated, activated.EventType),
            partiallyFilled =>
            {
                Assert.Equal(OrderHistoryEventType.PartiallyFilled, partiallyFilled.EventType);
                Assert.Equal(10, partiallyFilled.FilledSize);
                Assert.Equal(20, partiallyFilled.RemainingSize);
                Assert.Equal(firstTrade.Id, partiallyFilled.TradeId);
            },
            filled =>
            {
                Assert.Equal(OrderHistoryEventType.Filled, filled.EventType);
                Assert.Equal(30, filled.FilledSize);
                Assert.Equal(0, filled.RemainingSize);
                Assert.Equal(secondTrade.Id, filled.TradeId);
            });
    }
    
    [Fact]
    public void PartiallyFilledMarketOrderHistory_ShouldEndWithCancelledRemainder()
    {
        // Arrange
        var service = new AccountTradingService(GetAccountTestInstrument());
        var sellerId = RegisterAccount(service, instruments: 10);
        var buyerId = RegisterAccount(service, cash: 1000m);
        PlaceLimit(service, sellerId, OrderSide.Sell, 10, 100m);

        // Act
        var result = PlaceMarket(service, buyerId, OrderSide.Buy, 20);

        // Assert
        Assert.Collection(service.GetOrderHistory(GetOrder(result).Id),
            accepted =>
                Assert.Equal(OrderHistoryEventType.Accepted, accepted.EventType),
            partiallyFilled =>
            {
                Assert.Equal(OrderHistoryEventType.PartiallyFilled, partiallyFilled.EventType);
                Assert.Equal(10, partiallyFilled.FilledSize);
                Assert.Equal(10, partiallyFilled.RemainingSize);
            },
            cancelled =>
            {
                Assert.Equal(OrderHistoryEventType.Cancelled, cancelled.EventType);
                Assert.Equal(10, cancelled.FilledSize);
                Assert.Equal(10, cancelled.RemainingSize);
                Assert.Null(cancelled.TradeId);
            });
    }
    
    [Fact]
    public void CancelOrder_ShouldAppendCancelledEventToExistingHistory()
    {
        // Arrange
        var service = new AccountTradingService(GetAccountTestInstrument());
        var buyerId = RegisterAccount(service, cash: 1000m);
        var buy = PlaceLimit(service, buyerId, OrderSide.Buy, 10, 100m);

        // Act
        service.CancelOrder(GetOrder(buy).Id);

        // Assert
        var history = service.GetOrderHistory(GetOrder(buy).Id);
        Assert.Equal(
            [OrderHistoryEventType.Accepted, OrderHistoryEventType.Activated, OrderHistoryEventType.Cancelled],
            history.Select(item => item.EventType));
        Assert.Equal(0, history[^1].FilledSize);
        Assert.Equal(10, history[^1].RemainingSize);
    }
    
    [Fact]
    public void GetOrderHistory_ShouldThrow_WhenOrderDoesNotExist()
    {
        // Arrange
        var service = new AccountTradingService(GetAccountTestInstrument());

        // Act
        var act = () =>
            service.GetOrderHistory(Guid.NewGuid());

        // Assert
        Assert.Throws<KeyNotFoundException>(act);
    }
    
    [Fact]
    public void GetOrderHistory_ShouldReturnListUnaffectedByLaterEvents()
    {
        // Arrange
        var service = new AccountTradingService(GetAccountTestInstrument());
        var buyerId = RegisterAccount(service, cash: 1000m);
        var buy = PlaceLimit(service, buyerId, OrderSide.Buy, 10, 100m);
        var historyBeforeCancellation = service.GetOrderHistory(GetOrder(buy).Id);

        // Act
        service.CancelOrder(GetOrder(buy).Id);

        // Assert
        Assert.Equal(2, historyBeforeCancellation.Count);
        Assert.Equal(3, service.GetOrderHistory(GetOrder(buy).Id).Count);
    }
    
    [Fact]
    public void GetAccountOrderHistory_ShouldReturnOnlyOrdersOwnedByRequestedAccount()
    {
        // Arrange
        var service = new AccountTradingService(GetAccountTestInstrument());
        var firstBuyer = RegisterAccount(service, cash: 2000m);
        var secondBuyer = RegisterAccount(service, cash: 1000m);
        var firstOrder = PlaceLimit(service, firstBuyer, OrderSide.Buy, 10, 90m);
        var secondOrder = PlaceLimit(service, secondBuyer, OrderSide.Buy, 10, 80m);

        // Act
        var history = service.GetAccountOrderHistory(firstBuyer);

        // Assert
        Assert.NotEmpty(history);
        Assert.All(history, entry => Assert.Equal(GetOrder(firstOrder).Id, entry.OrderId));
        Assert.DoesNotContain(history, entry => entry.OrderId == GetOrder(secondOrder).Id);
    }
    
    [Fact]
    public void GetAccountOrderHistory_ShouldReturnEventsInChronologicalOrder()
    {
        // Arrange
        var service = new AccountTradingService(GetAccountTestInstrument());
        var buyerId = RegisterAccount(service, cash: 2000m);
        PlaceLimit(service, buyerId, OrderSide.Buy, 10, 90m);
        PlaceLimit(service, buyerId, OrderSide.Buy, 10, 80m);

        // Act
        var history = service.GetAccountOrderHistory(buyerId);

        // Assert
        Assert.True(history.Zip(
            history.Skip(1),
            (left, right) => left.OccurredAt <= right.OccurredAt).All(x => x));
    }
}