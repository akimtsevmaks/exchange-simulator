using exchange_simulator.Enums;
using exchange_simulator.Services;

namespace exchange_simulator.Tests.AccountTrading;

public class AccountTradingServiceEndToEndTests : AccountTradingServiceTestBase
{
    [Fact]
    public void PartialFillAndCancellationWorkflow_ShouldKeepAccountsAndTradingStateConsistent()
    {
        // Arrange
        var service = new AccountTradingService(GetAccountTestInstrument(initialPrice: 100m));
        var buyerId = RegisterAccount(service, cash: 10000m);
        var sellerId = RegisterAccount(service, instruments: 100);

        // Act
        var sellResult = PlaceLimit(service, sellerId, OrderSide.Sell, 100, 100m);
        var buyResult = PlaceMarket(service, buyerId, OrderSide.Buy, 40);
        var cancelResult = service.CancelOrder(GetOrder(sellResult).Id);

        // Assert
        Assert.True(cancelResult.IsSuccess);
        
        var buyer = GetAccount(service, buyerId);
        Assert.Equal(6000m, buyer.CashBalance);
        Assert.Equal(0m, buyer.ReservedCash);
        Assert.Equal(40, buyer.Position.Quantity);
        
        var seller = GetAccount(service, sellerId);
        Assert.Equal(4000m, seller.CashBalance);
        Assert.Equal(60, seller.Position.Quantity);
        Assert.Equal(0, seller.Position.ReservedQuantity);
        Assert.Equal(60, seller.Position.AvailableQuantity);
        
        
        var trade = Assert.Single(buyResult.Trades);
        Assert.Equal(trade, Assert.Single(service.GetAccountTrades(buyerId)));
        Assert.Equal(trade, Assert.Single(service.GetAccountTrades(sellerId)));
        Assert.Equal(2, service.GetAccountOperations(buyerId).Count);
        Assert.Equal(2, service.GetAccountOperations(sellerId).Count);
        Assert.Equal(
            [OrderHistoryEventType.Accepted, OrderHistoryEventType.Activated,
                OrderHistoryEventType.PartiallyFilled, OrderHistoryEventType.Cancelled],
            service.GetOrderHistory(GetOrder(sellResult).Id).Select(entry => entry.EventType));
        Assert.Equal(
            [OrderHistoryEventType.Accepted, OrderHistoryEventType.Filled],
            service.GetOrderHistory(GetOrder(buyResult).Id).Select(entry => entry.EventType));
        Assert.Empty(service.GetActiveOrders(sellerId));
        
        
        Assert.Equal(10000m, buyer.CashBalance + seller.CashBalance);
        Assert.Equal(100, buyer.Position.Quantity + seller.Position.Quantity);
        Assert.Equal(
            buyer.CashBalance,
            service.GetAccountOperations(buyerId).Sum(operation => operation.CashChange));
        Assert.Equal(
            seller.CashBalance,
            service.GetAccountOperations(sellerId).Sum(operation => operation.CashChange));
        Assert.Equal(
            buyer.Position.Quantity,
            service.GetAccountOperations(buyerId).Sum(operation => operation.InstrumentQuantityChange));
        Assert.Equal(
            seller.Position.Quantity,
            service.GetAccountOperations(sellerId).Sum(operation => operation.InstrumentQuantityChange));
    }
}