using exchange_simulator.Enums;
using exchange_simulator.Models;
using exchange_simulator.Models.TradingCore;
using exchange_simulator.Services;

namespace exchange_simulator.Tests.AccountTrading;

public class AccountTradingServiceRiskTests : AccountTradingServiceTestBase
{
    [Fact]
    public void PlaceOrder_ShouldThrow_WhenCommandIsNull()
    {
        // Arrange
        var service = new AccountTradingService(GetAccountTestInstrument());

        // Act
        var act = () =>
            service.PlaceOrder(null!);

        // Assert
        Assert.Throws<ArgumentNullException>(act);
    }
    
    [Fact]
    public void PlaceOrder_ShouldRejectInvalidCommandWithoutChangingAccount()
    {
        // Arrange
        var service = new AccountTradingService(GetAccountTestInstrument(lotSize: 10));
        var accountId = RegisterAccount(service, cash: 1000m);
        var accountBefore = GetAccount(service, accountId);
        var operationsBefore = service.GetAccountOperations(accountId);
        var command = new PlaceOrderCommand(accountId, OrderSide.Buy, OrderType.Limit, 15, 100m);

        // Act
        var result = service.PlaceOrder(command);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(OrderRejectionReason.QuantityNotMultipleOfLotSize, result.RejectionReason);
        Assert.Null(result.Order);
        Assert.Empty(result.Trades);
        Assert.Equal(accountBefore, GetAccount(service, accountId));
        Assert.Equal(operationsBefore, service.GetAccountOperations(accountId));
        Assert.Empty(service.GetActiveOrders(accountId));
        Assert.Empty(service.GetAccountOrderHistory(accountId));
    }
    
    [Fact]
    public void PlaceOrder_ShouldRejectValidCommand_WhenAccountDoesNotExist()
    {
        // Arrange
        var service = new AccountTradingService(GetAccountTestInstrument());
        var command = new PlaceOrderCommand(Guid.NewGuid(), OrderSide.Buy, OrderType.Limit, 10, 100m);

        // Act
        var result = service.PlaceOrder(command);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(OrderRejectionReason.AccountNotFound, result.RejectionReason);
        Assert.Null(result.Order);
        Assert.Empty(result.Trades);
    }
    
    [Fact]
    public void PlaceLimitBuy_ShouldReserveFullLimitValue()
    {
        // Arrange
        var service = new AccountTradingService(GetAccountTestInstrument());
        var buyerId = RegisterAccount(service, cash: 2000m);

        // Act
        var result = PlaceLimit(service, buyerId, OrderSide.Buy, 10, 120m);

        // Assert
        Assert.True(result.IsSuccess);
        var buyer = GetAccount(service, buyerId);
        Assert.Equal(2000m, buyer.CashBalance);
        Assert.Equal(1200m, buyer.ReservedCash);
        Assert.Equal(800m, buyer.AvailableCash);
    }
    
    [Fact]
    public void PlaceLimitSell_ShouldReserveFullOrderQuantity()
    {
        // Arrange
        var service = new AccountTradingService(GetAccountTestInstrument());
        var sellerId = RegisterAccount(service, instruments: 30);

        // Act
        var result = PlaceLimit(service, sellerId, OrderSide.Sell, 20, 120m);

        // Assert
        Assert.True(result.IsSuccess);
        var seller = GetAccount(service, sellerId);
        Assert.Equal(30, seller.Position.Quantity);
        Assert.Equal(20, seller.Position.ReservedQuantity);
        Assert.Equal(10, seller.Position.AvailableQuantity);
    }
    
    [Fact]
    public void PlaceLimitBuy_ShouldRejectWithoutSideEffects_WhenAvailableCashIsInsufficient()
    {
        // Arrange
        var service = new AccountTradingService(GetAccountTestInstrument());
        var buyerId = RegisterAccount(service, cash: 500m);
        var before = GetAccount(service, buyerId);

        // Act
        var result = PlaceLimit(service, buyerId, OrderSide.Buy, 10, 100m);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(OrderRejectionReason.InsufficientAvailableCash, result.RejectionReason);
        Assert.Equal(before, GetAccount(service, buyerId));
        Assert.Empty(service.GetActiveOrders(buyerId));
        Assert.Empty(service.GetAccountOrderHistory(buyerId));
    }
    
    [Fact]
    public void PlaceLimitSell_ShouldRejectWithoutSideEffects_WhenAvailablePositionIsInsufficient()
    {
        // Arrange
        var service = new AccountTradingService(GetAccountTestInstrument());
        var sellerId = RegisterAccount(service, instruments: 10);
        var before = GetAccount(service, sellerId);

        // Act
        var result = PlaceLimit(service, sellerId, OrderSide.Sell, 20, 100m);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(OrderRejectionReason.InsufficientAvailablePosition, result.RejectionReason);
        Assert.Equal(before, GetAccount(service, sellerId));
        Assert.Empty(service.GetActiveOrders(sellerId));
        Assert.Empty(service.GetAccountOrderHistory(sellerId));
    }
    
    [Fact]
    public void PlaceLimitBuy_ShouldNotReuseCashReservedByAnotherActiveOrder()
    {
        // Arrange
        var service = new AccountTradingService(GetAccountTestInstrument());
        var buyerId = RegisterAccount(service, cash: 1500m);
        PlaceLimit(service, buyerId, OrderSide.Buy, 10, 100m);

        // Act
        var result = PlaceLimit(service, buyerId, OrderSide.Buy, 10, 60m);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(OrderRejectionReason.InsufficientAvailableCash, result.RejectionReason);
        Assert.Equal(1000m, GetAccount(service, buyerId).ReservedCash);
        Assert.Single(service.GetActiveOrders(buyerId));
    }
    
    [Fact]
    public void PlaceLimitSell_ShouldNotReuseInstrumentsReservedByAnotherActiveOrder()
    {
        // Arrange
        var service = new AccountTradingService(GetAccountTestInstrument());
        var sellerId = RegisterAccount(service, instruments: 20);
        PlaceLimit(service, sellerId, OrderSide.Sell, 10, 100m);

        // Act
        var result = PlaceLimit(service, sellerId, OrderSide.Sell, 20, 110m);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(OrderRejectionReason.InsufficientAvailablePosition, result.RejectionReason);
        Assert.Equal(10, GetAccount(service, sellerId).Position.ReservedQuantity);
        Assert.Single(service.GetActiveOrders(sellerId));
    }
    
    [Fact]
    public void PlaceMarketBuy_ShouldRequireCashOnlyForCurrentlyExecutableLiquidity()
    {
        // Arrange
        var service = new AccountTradingService(GetAccountTestInstrument());
        var sellerId = RegisterAccount(service, instruments: 10);
        var buyerId = RegisterAccount(service, cash: 1000m);
        PlaceLimit(service, sellerId, OrderSide.Sell, 10, 100m);

        // Act
        var result = PlaceMarket(service, buyerId, OrderSide.Buy, 30);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(OrderStatus.Cancelled, GetOrder(result).OrderStatus);
        Assert.Equal(10, GetOrder(result).FilledSize);
        Assert.Equal(20, GetOrder(result).RemainingSize);
    }
    
    [Fact]
    public void PlaceMarketBuy_ShouldRejectBeforeMatching_WhenExecutableCostExceedsAvailableCash()
    {
        // Arrange
        var service = new AccountTradingService(GetAccountTestInstrument());
        var sellerId = RegisterAccount(service, instruments: 10);
        var buyerId = RegisterAccount(service, cash: 999m);
        var sell = PlaceLimit(service, sellerId, OrderSide.Sell, 10, 100m);

        // Act
        var result = PlaceMarket(service, buyerId, OrderSide.Buy, 10);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(OrderRejectionReason.InsufficientAvailableCash, result.RejectionReason);
        Assert.Equal(GetOrder(sell), Assert.Single(service.GetActiveOrders(sellerId)));
        Assert.Empty(service.GetAccountTrades(sellerId));
    }
    
    [Fact]
    public void PlaceMarketSell_ShouldRequireFullRequestedQuantityBeforeMatching()
    {
        // Arrange
        var service = new AccountTradingService(GetAccountTestInstrument());
        var buyerId = RegisterAccount(service, cash: 1000m);
        var sellerId = RegisterAccount(service, instruments: 10);
        var buy = PlaceLimit(service, buyerId, OrderSide.Buy, 10, 100m);

        // Act
        var result = PlaceMarket(service, sellerId, OrderSide.Sell, 20);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(OrderRejectionReason.InsufficientAvailablePosition, result.RejectionReason);
        Assert.Equal(GetOrder(buy), Assert.Single(service.GetActiveOrders(buyerId)));
        Assert.Empty(service.GetAccountTrades(buyerId));
    }
    
    [Fact]
    public void PlaceMarketBuy_ShouldAcceptAndCancelWithoutCash_WhenThereIsNoLiquidity()
    {
        // Arrange
        var service = new AccountTradingService(GetAccountTestInstrument());
        var buyerId = RegisterAccount(service);

        // Act
        var result = PlaceMarket(service, buyerId, OrderSide.Buy, 10);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(OrderStatus.Cancelled, GetOrder(result).OrderStatus);
        Assert.Empty(result.Trades);
        Assert.Equal(0m, GetAccount(service, buyerId).ReservedCash);
    }
    
    [Fact]
    public void PlaceMarketSell_ShouldReleaseFullReservation_WhenThereIsNoLiquidity()
    {
        // Arrange
        var service = new AccountTradingService(GetAccountTestInstrument());
        var sellerId = RegisterAccount(service, instruments: 10);

        // Act
        var result = PlaceMarket(service, sellerId, OrderSide.Sell, 10);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(OrderStatus.Cancelled, GetOrder(result).OrderStatus);
        Assert.Empty(result.Trades);
        Assert.Equal(0, GetAccount(service, sellerId).Position.ReservedQuantity);
        Assert.Equal(10, GetAccount(service, sellerId).Position.AvailableQuantity);
    }
    
    [Fact]
    public void PlaceLimitBuy_ShouldLeaveAccountUnchanged_WhenReservationValueOverflows()
    {
        // Arrange
        var service = new AccountTradingService(GetAccountTestInstrument());
        var buyerId = RegisterAccount(service, cash: 1000m);
        var before = GetAccount(service, buyerId);

        // Act
        var act = () =>
            PlaceLimit(service, buyerId, OrderSide.Buy, 10, decimal.MaxValue);

        // Assert
        Assert.Throws<OverflowException>(act);
        Assert.Equal(before, GetAccount(service, buyerId));
        Assert.Empty(service.GetActiveOrders(buyerId));
    }
    
    [Fact]
    public void PlaceMarketSell_ShouldLeaveAllStateUnchanged_WhenBuyerPositionWouldOverflow()
    {
        // Arrange
        var service = new AccountTradingService(GetAccountTestInstrument(lotSize: 1, initialPrice: 1m));
        
        var buyerId = RegisterAccount(service, cash: 1m, instruments: long.MaxValue);
        var sellerId = RegisterAccount(service, instruments: 1);
        
        var buyOrder = GetOrder(PlaceLimit(service, buyerId, OrderSide.Buy, 1, 1m));
        
        var buyerBefore = GetAccount(service, buyerId);
        var sellerBefore = GetAccount(service, sellerId);
        var buyerOperationsBefore = service.GetAccountOperations(buyerId);
        var sellerOperationsBefore = service.GetAccountOperations(sellerId);
        var buyHistoryBefore = service.GetOrderHistory(buyOrder.Id);

        // Act
        var act = () =>
            PlaceMarket(service, sellerId, OrderSide.Sell, 1);

        // Assert
        Assert.Throws<OverflowException>(act);
        Assert.Equal(buyerBefore, GetAccount(service, buyerId));
        Assert.Equal(sellerBefore, GetAccount(service, sellerId));
        Assert.Equal(buyerOperationsBefore, service.GetAccountOperations(buyerId));
        Assert.Equal(sellerOperationsBefore, service.GetAccountOperations(sellerId));
        Assert.Equal(buyHistoryBefore, service.GetOrderHistory(buyOrder.Id));
        Assert.Equal(buyOrder, Assert.Single(service.GetActiveOrders(buyerId)));
        Assert.Empty(service.GetActiveOrders(sellerId));
        Assert.Empty(service.GetTrades());
    }

    [Fact]
    public void PlaceMarketBuy_ShouldLeaveAllStateUnchanged_WhenSellerCashWouldOverflow()
    {
        // Arrange
        var service = new AccountTradingService(GetAccountTestInstrument(lotSize: 1, initialPrice: 1m));
        
        var sellerId = RegisterAccount(service, cash: decimal.MaxValue, instruments: 1);
        var buyerId = RegisterAccount(service, cash: 1m);
        
        var sellOrder = GetOrder(PlaceLimit(service, sellerId, OrderSide.Sell, 1, 1m));
        
        var sellerBefore = GetAccount(service, sellerId);
        var buyerBefore = GetAccount(service, buyerId);
        var sellerOperationsBefore = service.GetAccountOperations(sellerId);
        var buyerOperationsBefore = service.GetAccountOperations(buyerId);
        var sellHistoryBefore = service.GetOrderHistory(sellOrder.Id);

        // Act
        var act = () =>
            PlaceMarket(service, buyerId, OrderSide.Buy, 1);

        // Assert
        Assert.Throws<OverflowException>(act);
        Assert.Equal(sellerBefore, GetAccount(service, sellerId));
        Assert.Equal(buyerBefore, GetAccount(service, buyerId));
        Assert.Equal(sellerOperationsBefore, service.GetAccountOperations(sellerId));
        Assert.Equal(buyerOperationsBefore, service.GetAccountOperations(buyerId));
        Assert.Equal(sellHistoryBefore, service.GetOrderHistory(sellOrder.Id));
        Assert.Equal(sellOrder, Assert.Single(service.GetActiveOrders(sellerId)));
        Assert.Empty(service.GetActiveOrders(buyerId));
        Assert.Empty(service.GetTrades());
    }

    [Fact]
    public void PlaceMarketBuy_ShouldLeaveAllPlannedTradesUnapplied_WhenPositionOverflowsOnSecondTrade()
    {
        // Arrange
        var service = new AccountTradingService(GetAccountTestInstrument(lotSize: 1, initialPrice: 1m));
        
        var firstSellerId = RegisterAccount(service, instruments: 1);
        var secondSellerId = RegisterAccount(service, instruments: 1);
        var buyerId = RegisterAccount(service, cash: 2m, instruments: long.MaxValue - 1);
        
        var firstSellOrder = GetOrder(PlaceLimit(service, firstSellerId, OrderSide.Sell, 1, 1m));
        var secondSellOrder = GetOrder(PlaceLimit(service, secondSellerId, OrderSide.Sell, 1, 1m));
        
        var firstSellerBefore = GetAccount(service, firstSellerId);
        var secondSellerBefore = GetAccount(service, secondSellerId);
        var buyerBefore = GetAccount(service, buyerId);
        var firstSellerOperationsBefore = service.GetAccountOperations(firstSellerId);
        var secondSellerOperationsBefore = service.GetAccountOperations(secondSellerId);
        var buyerOperationsBefore = service.GetAccountOperations(buyerId);
        var firstSellHistoryBefore = service.GetOrderHistory(firstSellOrder.Id);
        var secondSellHistoryBefore = service.GetOrderHistory(secondSellOrder.Id);

        // Act
        var act = () =>
            PlaceMarket(service, buyerId, OrderSide.Buy, 2);

        // Assert
        Assert.Throws<OverflowException>(act);
        Assert.Equal(firstSellerBefore, GetAccount(service, firstSellerId));
        Assert.Equal(secondSellerBefore, GetAccount(service, secondSellerId));
        Assert.Equal(buyerBefore, GetAccount(service, buyerId));
        Assert.Equal(firstSellerOperationsBefore, service.GetAccountOperations(firstSellerId));
        Assert.Equal(secondSellerOperationsBefore, service.GetAccountOperations(secondSellerId));
        Assert.Equal(buyerOperationsBefore, service.GetAccountOperations(buyerId));
        Assert.Equal(firstSellHistoryBefore, service.GetOrderHistory(firstSellOrder.Id));
        Assert.Equal(secondSellHistoryBefore, service.GetOrderHistory(secondSellOrder.Id));
        Assert.Equal(firstSellOrder, Assert.Single(service.GetActiveOrders(firstSellerId)));
        Assert.Equal(secondSellOrder, Assert.Single(service.GetActiveOrders(secondSellerId)));
        Assert.Empty(service.GetActiveOrders(buyerId));
        Assert.Empty(service.GetTrades());
    }
}