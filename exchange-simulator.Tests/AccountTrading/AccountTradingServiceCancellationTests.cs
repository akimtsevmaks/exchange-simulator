using exchange_simulator.Enums;
using exchange_simulator.Services;

namespace exchange_simulator.Tests.AccountTrading;

public class AccountTradingServiceCancellationTests : AccountTradingServiceTestBase
{
    [Fact]
    public void CancelOrder_ShouldRejectUnknownOrder()
    {
        // Arrange
        var service = new AccountTradingService(GetAccountTestInstrument());

        // Act
        var result = service.CancelOrder(Guid.NewGuid());

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(OrderRejectionReason.OrderNotFound, result.RejectionReason);
        Assert.Null(result.Order);
        Assert.Empty(result.Trades);
    }
    
    [Fact]
    public void CancelBuyOrder_ShouldReleaseAllReservedCash()
    {
        // Arrange
        var service = new AccountTradingService(GetAccountTestInstrument());
        var buyerId = RegisterAccount(service, cash: 2000m);
        var buy = PlaceLimit(service, buyerId, OrderSide.Buy, 10, 100m);

        // Act
        var result = service.CancelOrder(GetOrder(buy).Id);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(OrderStatus.Cancelled, GetOrder(result).OrderStatus);
        var buyer = GetAccount(service, buyerId);
        Assert.Equal(2000m, buyer.CashBalance);
        Assert.Equal(0m, buyer.ReservedCash);
        Assert.Equal(2000m, buyer.AvailableCash);
    }
    
    [Fact]
    public void CancelSellOrder_ShouldReleaseAllReservedInstruments()
    {
        // Arrange
        var service = new AccountTradingService(GetAccountTestInstrument());
        var sellerId = RegisterAccount(service, instruments: 20);
        var sell = PlaceLimit(service, sellerId, OrderSide.Sell, 10, 100m);

        // Act
        var result = service.CancelOrder(GetOrder(sell).Id);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(OrderStatus.Cancelled, GetOrder(result).OrderStatus);
        var seller = GetAccount(service, sellerId);
        Assert.Equal(20, seller.Position.Quantity);
        Assert.Equal(0, seller.Position.ReservedQuantity);
        Assert.Equal(20, seller.Position.AvailableQuantity);
    }
    
    [Fact]
    public void CancelPartiallyFilledBuy_ShouldReleaseRemainderAndPreserveExecutedPart()
    {
        // Arrange
        var service = new AccountTradingService(GetAccountTestInstrument());
        var sellerId = RegisterAccount(service, instruments: 10);
        var buyerId = RegisterAccount(service, cash: 3000m);
        PlaceLimit(service, sellerId, OrderSide.Sell, 10, 90m);
        var buy = PlaceLimit(service, buyerId, OrderSide.Buy, 30, 100m);

        // Act
        service.CancelOrder(GetOrder(buy).Id);

        // Assert
        var buyer = GetAccount(service, buyerId);
        Assert.Equal(2100m, buyer.CashBalance);
        Assert.Equal(0m, buyer.ReservedCash);
        Assert.Equal(2100m, buyer.AvailableCash);
        Assert.Equal(10, buyer.Position.Quantity);
        Assert.Equal(90m, buyer.Position.AveragePrice);
    }
    
    [Fact]
    public void CancelPartiallyFilledSell_ShouldReleaseRemainderAndPreserveExecutedPart()
    {
        // Arrange
        var service = new AccountTradingService(GetAccountTestInstrument());
        var sellerId = RegisterAccount(service, instruments: 30);
        var buyerId = RegisterAccount(service, cash: 1000m);
        var sell = PlaceLimit(service, sellerId, OrderSide.Sell, 30, 100m);
        PlaceMarket(service, buyerId, OrderSide.Buy, 10);

        // Act
        service.CancelOrder(GetOrder(sell).Id);

        // Assert
        var seller = GetAccount(service, sellerId);
        Assert.Equal(1000m, seller.CashBalance);
        Assert.Equal(20, seller.Position.Quantity);
        Assert.Equal(0, seller.Position.ReservedQuantity);
        Assert.Equal(20, seller.Position.AvailableQuantity);
    }
    
    [Fact]
    public void CancelOrder_ShouldRejectFilledOrderWithoutChangingAccounts()
    {
        // Arrange
        var service = new AccountTradingService(GetAccountTestInstrument());
        var sellerId = RegisterAccount(service, instruments: 10);
        var buyerId = RegisterAccount(service, cash: 1000m);
        var sell = PlaceLimit(service, sellerId, OrderSide.Sell, 10, 100m);
        PlaceMarket(service, buyerId, OrderSide.Buy, 10);
        var sellerBefore = GetAccount(service, sellerId);

        // Act
        var result = service.CancelOrder(GetOrder(sell).Id);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(OrderRejectionReason.OrderNotActive, result.RejectionReason);
        Assert.Equal(sellerBefore, GetAccount(service, sellerId));
    }
    
    [Fact]
    public void CancelOrder_ShouldRejectRepeatedCancellationWithoutChangingAccount()
    {
        // Arrange
        var service = new AccountTradingService(GetAccountTestInstrument());
        var buyerId = RegisterAccount(service, cash: 1_000m);
        var buy = PlaceLimit(service, buyerId, OrderSide.Buy, 10, 100m);
        service.CancelOrder(GetOrder(buy).Id);
        var buyerBefore = GetAccount(service, buyerId);

        // Act
        var result = service.CancelOrder(GetOrder(buy).Id);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(OrderRejectionReason.OrderNotActive, result.RejectionReason);
        Assert.Equal(buyerBefore, GetAccount(service, buyerId));
    }
}
