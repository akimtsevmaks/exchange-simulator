using exchange_simulator.Enums;
using exchange_simulator.Models;
using exchange_simulator.Services;

namespace exchange_simulator.Tests.AccountTrading;

public class AccountTradingServiceAccountTests : AccountTradingServiceTestBase
{
    [Fact]
    public void Ctor_ShouldUseProvidedInstrument()
    {
        // Arrange
        var instrument = GetAccountTestInstrument();

        // Act
        var service = new AccountTradingService(instrument);

        // Assert
        Assert.Same(instrument, service.Instrument);
    }
    
    [Fact]
    public void Ctor_ShouldThrow_WhenInstrumentIsNull()
    {
        // Act
        var act = () =>
            new AccountTradingService(null!);

        // Assert
        Assert.Throws<ArgumentNullException>(act);
    }
    
    [Fact]
    public void RegisterAccount_ShouldReturnEmptyAccountSnapshot()
    {
        // Arrange
        var instrument = GetAccountTestInstrument();
        var service = new AccountTradingService(instrument);
        var accountId = Guid.NewGuid();

        // Act
        var account = service.RegisterAccount(accountId);

        // Assert
        Assert.Equal(accountId, account.Id);
        Assert.Equal(0m, account.CashBalance);
        Assert.Equal(0m, account.ReservedCash);
        Assert.Equal(0m, account.AvailableCash);
        Assert.Equal(instrument.Id, account.Position.InstrumentId);
        Assert.Equal(0, account.Position.Quantity);
    }
    
    [Fact]
    public void RegisterAccount_ShouldThrow_WhenAccountIdIsEmpty()
    {
        // Arrange
        var service = new AccountTradingService(GetAccountTestInstrument());

        // Act
        var act = () =>
            service.RegisterAccount(Guid.Empty);

        // Assert
        Assert.Throws<ArgumentException>(act);
    }
    
    [Fact]
    public void RegisterAccount_ShouldThrow_WhenAccountIsAlreadyRegistered()
    {
        // Arrange
        var service = new AccountTradingService(GetAccountTestInstrument());
        var accountId = Guid.NewGuid();
        service.RegisterAccount(accountId);

        // Act
        var act = () =>
            service.RegisterAccount(accountId);

        // Assert
        Assert.Throws<InvalidOperationException>(act);
    }
    
    [Fact]
    public void TryGetAccount_ShouldReturnRegisteredAccount()
    {
        // Arrange
        var service = new AccountTradingService(GetAccountTestInstrument());
        var accountId = RegisterAccount(service, cash: 1000m, instruments: 20);

        // Act
        var isFound = service.TryGetAccount(accountId, out var account);

        // Assert
        Assert.True(isFound);
        var snapshot = Assert.IsType<TradingAccountSnapshot>(account);
        Assert.Equal(accountId, snapshot.Id);
        Assert.Equal(1000m, snapshot.CashBalance);
        Assert.Equal(20, snapshot.Position.Quantity);
    }
    
    [Fact]
    public void TryGetAccount_ShouldReturnFalseAndNull_WhenAccountDoesNotExist()
    {
        // Arrange
        var service = new AccountTradingService(GetAccountTestInstrument());

        // Act
        var isFound = service.TryGetAccount(Guid.NewGuid(), out var account);

        // Assert
        Assert.False(isFound);
        Assert.Null(account);
    }
    
    [Fact]
    public void GrantInitialCash_ShouldThrow_WhenAccountDoesNotExist()
    {
        // Arrange
        var service = new AccountTradingService(GetAccountTestInstrument());

        // Act
        var act = () =>
            service.GrantInitialCash(Guid.NewGuid(), 1000m);

        // Assert
        Assert.Throws<KeyNotFoundException>(act);
    }
    
    [Fact]
    public void GrantInitialInstruments_ShouldThrow_WhenAccountDoesNotExist()
    {
        // Arrange
        var service = new AccountTradingService(GetAccountTestInstrument());

        // Act
        var act = () =>
            service.GrantInitialInstruments(Guid.NewGuid(), 10);

        // Assert
        Assert.Throws<KeyNotFoundException>(act);
    }
    
    [Fact]
    public void AccountSnapshot_ShouldNotChangeAfterLaterServiceCommands()
    {
        // Arrange
        var service = new AccountTradingService(GetAccountTestInstrument());
        var accountId = RegisterAccount(service);
        service.TryGetAccount(accountId, out var accountBeforeGrant);

        // Act
        service.GrantInitialCash(accountId, 1_000m);

        // Assert
        Assert.Equal(0m, Assert.IsType<TradingAccountSnapshot>(accountBeforeGrant).CashBalance);
        Assert.Equal(1000m, GetAccount(service, accountId).CashBalance);
    }
    
    [Fact]
    public void TradingEngineWithSameInstrument_ShouldNotChangeServiceOrderBook()
    {
        // Arrange
        var instrument = GetAccountTestInstrument();
        var externalEngine = new TradingEngine(instrument);
        var service = new AccountTradingService(instrument);
        var buyerId = RegisterAccount(service, cash: 1000m);
        externalEngine.PlaceOrder(
            new PlaceOrderCommand(Guid.NewGuid(), OrderSide.Sell, OrderType.Limit, 10, 100m));

        // Act
        var result = PlaceMarket(service, buyerId, OrderSide.Buy, 10);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Empty(result.Trades);
        Assert.Equal(OrderStatus.Cancelled, GetOrder(result).OrderStatus);
        Assert.Equal(1000m, GetAccount(service, buyerId).AvailableCash);
        Assert.Empty(service.GetAccountTrades(buyerId));
    }
}