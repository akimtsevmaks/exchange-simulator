using exchange_simulator.Enums;
using exchange_simulator.Services;

namespace exchange_simulator.Tests.AccountTrading;

public class AccountTradingServiceQueryTests : AccountTradingServiceTestBase
{
    [Fact]
    public void GetActiveOrders_ShouldReturnOnlyOrdersOwnedByRequestedAccount()
    {
        // Arrange
        var service = new AccountTradingService(GetAccountTestInstrument());
        var firstBuyer = RegisterAccount(service, cash: 2000m);
        var secondBuyer = RegisterAccount(service, cash: 1000m);
        var firstOrder = PlaceLimit(service, firstBuyer, OrderSide.Buy, 10, 90m);
        PlaceLimit(service, secondBuyer, OrderSide.Buy, 10, 80m);

        // Act
        var activeOrders = service.GetActiveOrders(firstBuyer);

        // Assert
        Assert.Equal(GetOrder(firstOrder), Assert.Single(activeOrders));
    }
    
    [Fact]
    public void GetActiveOrders_ShouldReturnListUnaffectedByLaterCancellation()
    {
        // Arrange
        var service = new AccountTradingService(GetAccountTestInstrument());
        var buyerId = RegisterAccount(service, cash: 1000m);
        var buy = PlaceLimit(service, buyerId, OrderSide.Buy, 10, 100m);
        var activeOrdersBeforeCancellation = service.GetActiveOrders(buyerId);

        // Act
        service.CancelOrder(GetOrder(buy).Id);

        // Assert
        Assert.Single(activeOrdersBeforeCancellation);
        Assert.Empty(service.GetActiveOrders(buyerId));
    }
    
    [Fact]
    public void GetAccountTrades_ShouldReturnTradesWhereAccountWasBuyerOrSeller()
    {
        // Arrange
        var service = new AccountTradingService(GetAccountTestInstrument());
        
        var firstAccount = RegisterAccount(service, cash: 1000m, instruments: 10);
        var secondAccount = RegisterAccount(service, cash: 1000m, instruments: 10);
        
        PlaceLimit(service, secondAccount, OrderSide.Sell, 10, 100m);
        var buyResult = PlaceMarket(service, firstAccount, OrderSide.Buy, 10);
        PlaceLimit(service, secondAccount, OrderSide.Buy, 10, 100m);
        var sellResult = PlaceMarket(service, firstAccount, OrderSide.Sell, 10);

        // Act
        var trades = service.GetAccountTrades(firstAccount);

        // Assert
        Assert.Equal([Assert.Single(buyResult.Trades), Assert.Single(sellResult.Trades)], trades);
    }
    
    [Fact]
    public void GetAccountTrades_ShouldExcludeTradesOfOtherAccounts()
    {
        // Arrange
        var service = new AccountTradingService(GetAccountTestInstrument());
        
        var firstSeller = RegisterAccount(service, instruments: 10);
        var firstBuyer = RegisterAccount(service, cash: 1000m);
        var unrelatedAccount = RegisterAccount(service);
        
        PlaceLimit(service, firstSeller, OrderSide.Sell, 10, 100m);
        PlaceMarket(service, firstBuyer, OrderSide.Buy, 10);

        // Act
        var trades = service.GetAccountTrades(unrelatedAccount);

        // Assert
        Assert.Empty(trades);
    }
    
    [Fact]
    public void GetAccountTrades_ShouldReturnSelfTradeOnlyOnce()
    {
        // Arrange
        var service = new AccountTradingService(GetAccountTestInstrument());
        var accountId = RegisterAccount(service, cash: 1000m, instruments: 10);
        PlaceLimit(service, accountId, OrderSide.Sell, 10, 100m);
        PlaceMarket(service, accountId, OrderSide.Buy, 10);

        // Act
        var trades = service.GetAccountTrades(accountId);

        // Assert
        Assert.Single(trades);
    }
    
    [Fact]
    public void GetAccountOperations_ShouldPreserveOperationOrderForRequestedAccount()
    {
        // Arrange
        var service = new AccountTradingService(GetAccountTestInstrument());
        var sellerId = RegisterAccount(service, instruments: 10);
        var buyerId = RegisterAccount(service, cash: 1000m);
        PlaceLimit(service, sellerId, OrderSide.Sell, 10, 100m);
        PlaceMarket(service, buyerId, OrderSide.Buy, 10);

        // Act
        var operations = service.GetAccountOperations(buyerId);

        // Assert
        Assert.Equal(
            [AccountOperationType.InitialCashGranted, AccountOperationType.TradeBuy],
            operations.Select(operation => operation.Type));
        Assert.All(operations, operation => Assert.Equal(buyerId, operation.AccountId));
    }
    
    [Fact]
    public void GetAccountOperations_ShouldThrow_WhenAccountDoesNotExist()
    {
        // Arrange
        var service = new AccountTradingService(GetAccountTestInstrument());

        // Act
        var act = () =>
            service.GetAccountOperations(Guid.NewGuid());

        // Assert
        Assert.Throws<KeyNotFoundException>(act);
    }
    
    [Fact]
    public void GetActiveOrders_ShouldThrow_WhenAccountDoesNotExist()
    {
        // Arrange
        var service = new AccountTradingService(GetAccountTestInstrument());

        // Act
        var act = () =>
            service.GetActiveOrders(Guid.NewGuid());

        // Assert
        Assert.Throws<KeyNotFoundException>(act);
    }
    
    [Fact]
    public void GetAccountOrderHistory_ShouldThrow_WhenAccountDoesNotExist()
    {
        // Arrange
        var service = new AccountTradingService(GetAccountTestInstrument());

        // Act
        var act = () =>
            service.GetAccountOrderHistory(Guid.NewGuid());

        // Assert
        Assert.Throws<KeyNotFoundException>(act);
    }
    
    [Fact]
    public void GetAccountTrades_ShouldThrow_WhenAccountDoesNotExist()
    {
        // Arrange
        var service = new AccountTradingService(GetAccountTestInstrument());

        // Act
        var act = () =>
            service.GetAccountTrades(Guid.NewGuid());

        // Assert
        Assert.Throws<KeyNotFoundException>(act);
    }
}