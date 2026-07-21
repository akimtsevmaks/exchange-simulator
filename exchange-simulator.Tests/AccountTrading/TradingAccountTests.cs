using exchange_simulator.Enums;
using exchange_simulator.Models;

namespace exchange_simulator.Tests.AccountTrading;

public class TradingAccountTests : AccountTradingServiceTestBase
{
    [Fact]
    public void Ctor_ShouldCreateEmptyAccountForProvidedInstrument()
    {
        // Arrange
        var instrument = GetAccountTestInstrument();
        var accountId = Guid.NewGuid();

        // Act
        var account = new TradingAccount(accountId, instrument);

        // Assert
        var snapshot = account.GetSnapshot();
        Assert.Equal(accountId, snapshot.Id);
        Assert.Equal(0m, snapshot.CashBalance);
        Assert.Equal(0m, snapshot.ReservedCash);
        Assert.Equal(0m, snapshot.AvailableCash);
        Assert.Equal(instrument.Id, snapshot.Position.InstrumentId);
        Assert.Equal(0, snapshot.Position.Quantity);
        Assert.Equal(0, snapshot.Position.ReservedQuantity);
        Assert.Equal(0, snapshot.Position.AvailableQuantity);
        Assert.Equal(0m, snapshot.Position.AveragePrice);
        Assert.Empty(account.GetOperations());
    }
    
    [Fact]
    public void Ctor_ShouldThrow_WhenAccountIdIsEmpty()
    {
        // Act
        var act = () =>
            new TradingAccount(Guid.Empty, GetAccountTestInstrument());

        // Assert
        Assert.Throws<ArgumentException>(act);
    }
    
    [Fact]
    public void Ctor_ShouldThrow_WhenInstrumentIsNull()
    {
        // Act
        var act = () =>
            new TradingAccount(Guid.NewGuid(), null!);

        // Assert
        Assert.Throws<ArgumentNullException>(act);
    }
    
    [Fact]
    public void GrantInitialCash_ShouldIncreaseTotalAndAvailableCash()
    {
        // Arrange
        var account = new TradingAccount(Guid.NewGuid(), GetAccountTestInstrument());

        // Act
        account.GrantInitialCash(1000m);

        // Assert
        Assert.Equal(1000m, account.CashBalance);
        Assert.Equal(0m, account.ReservedCash);
        Assert.Equal(1000m, account.AvailableCash);
    }
    
    [Fact]
    public void GrantInitialCash_ShouldCreateExplicitCashOperation()
    {
        // Arrange
        var accountId = Guid.NewGuid();
        var account = new TradingAccount(accountId, GetAccountTestInstrument());

        // Act
        var before = DateTimeOffset.UtcNow;
        var operation = account.GrantInitialCash(1000m);
        var after = DateTimeOffset.UtcNow;

        // Assert
        Assert.NotEqual(Guid.Empty, operation.Id);
        Assert.Equal(accountId, operation.AccountId);
        Assert.Equal(AccountOperationType.InitialCashGranted, operation.Type);
        Assert.Equal(1000m, operation.CashChange);
        Assert.Null(operation.InstrumentId);
        Assert.Equal(0, operation.InstrumentQuantityChange);
        Assert.Null(operation.OrderId);
        Assert.Null(operation.TradeId);
        Assert.InRange(operation.CreatedAt, before, after);
        Assert.Equal(operation, Assert.Single(account.GetOperations()));
    }
    
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void GrantInitialCash_ShouldThrow_WhenAmountIsNotPositive(decimal amount)
    {
        // Arrange
        var account = new TradingAccount(Guid.NewGuid(), GetAccountTestInstrument());

        // Act
        var act = () =>
            account.GrantInitialCash(amount);

        // Assert
        Assert.Throws<ArgumentOutOfRangeException>(act);
        Assert.Empty(account.GetOperations());
    }
    
    [Fact]
    public void GrantInitialCash_ShouldLeaveAccountUnchanged_WhenBalanceOverflows()
    {
        // Arrange
        var account = new TradingAccount(Guid.NewGuid(), GetAccountTestInstrument());
        account.GrantInitialCash(decimal.MaxValue);

        // Act
        var act = () =>
            account.GrantInitialCash(1m);

        // Assert
        Assert.Throws<OverflowException>(act);
        Assert.Equal(decimal.MaxValue, account.CashBalance);
        Assert.Single(account.GetOperations());
    }
    
    [Fact]
    public void GrantInitialInstruments_ShouldIncreasePositionAtInitialPrice()
    {
        // Arrange
        var instrument = GetAccountTestInstrument(initialPrice: 125m);
        var account = new TradingAccount(Guid.NewGuid(), instrument);

        // Act
        account.GrantInitialInstruments(20);

        // Assert
        Assert.Equal(20, account.Position.Quantity);
        Assert.Equal(0, account.Position.ReservedQuantity);
        Assert.Equal(20, account.Position.AvailableQuantity);
        Assert.Equal(125m, account.Position.AveragePrice);
    }
    
    [Fact]
    public void GrantInitialInstruments_ShouldCreateExplicitInstrumentOperation()
    {
        // Arrange
        var instrument = GetAccountTestInstrument();
        var accountId = Guid.NewGuid();
        var account = new TradingAccount(accountId, instrument);

        // Act
        var before = DateTimeOffset.UtcNow;
        var operation = account.GrantInitialInstruments(20);
        var after = DateTimeOffset.UtcNow;

        // Assert
        Assert.NotEqual(Guid.Empty, operation.Id);
        Assert.Equal(accountId, operation.AccountId);
        Assert.Equal(AccountOperationType.InitialInstrumentsGranted, operation.Type);
        Assert.Equal(0m, operation.CashChange);
        Assert.Equal(instrument.Id, operation.InstrumentId);
        Assert.Equal(20, operation.InstrumentQuantityChange);
        Assert.Null(operation.OrderId);
        Assert.Null(operation.TradeId);
        Assert.InRange(operation.CreatedAt, before, after);
        Assert.Equal(operation, Assert.Single(account.GetOperations()));
    }
    
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void GrantInitialInstruments_ShouldThrow_WhenQuantityIsNotPositive(long quantity)
    {
        // Arrange
        var account = new TradingAccount(Guid.NewGuid(), GetAccountTestInstrument());

        // Act
        var act = () =>
            account.GrantInitialInstruments(quantity);

        // Assert
        Assert.Throws<ArgumentOutOfRangeException>(act);
        Assert.Empty(account.GetOperations());
    }
    
    [Fact]
    public void GrantInitialInstruments_ShouldLeaveAccountUnchanged_WhenQuantityOverflows()
    {
        // Arrange
        var account = new TradingAccount(Guid.NewGuid(), GetAccountTestInstrument());
        account.GrantInitialInstruments(long.MaxValue);

        // Act
        var act = () =>
            account.GrantInitialInstruments(1);

        // Assert
        Assert.Throws<OverflowException>(act);
        Assert.Equal(long.MaxValue, account.Position.Quantity);
        Assert.Single(account.GetOperations());
    }
    
    [Fact]
    public void TryReserveCash_ShouldMoveCashFromAvailableToReserved()
    {
        // Arrange
        var account = new TradingAccount(Guid.NewGuid(), GetAccountTestInstrument());
        account.GrantInitialCash(1000m);

        // Act
        var isReserved = account.TryReserveCash(600m);

        // Assert
        Assert.True(isReserved);
        Assert.Equal(1000m, account.CashBalance);
        Assert.Equal(600m, account.ReservedCash);
        Assert.Equal(400m, account.AvailableCash);
    }
    
    [Fact]
    public void TryReserveCash_ShouldReturnFalseWithoutChanges_WhenAvailableCashIsInsufficient()
    {
        // Arrange
        var account = new TradingAccount(Guid.NewGuid(), GetAccountTestInstrument());
        account.GrantInitialCash(1000m);

        // Act
        var isReserved = account.TryReserveCash(1001m);

        // Assert
        Assert.False(isReserved);
        Assert.Equal(0m, account.ReservedCash);
        Assert.Equal(1000m, account.AvailableCash);
    }
    
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void TryReserveCash_ShouldThrow_WhenAmountIsNotPositive(decimal amount)
    {
        // Arrange
        var account = new TradingAccount(Guid.NewGuid(), GetAccountTestInstrument());

        // Act
        Action act = () =>
            account.TryReserveCash(amount);

        // Assert
        Assert.Throws<ArgumentOutOfRangeException>(act);
    }
    
    [Fact]
    public void ReleaseCash_ShouldReturnReservedCashToAvailable()
    {
        // Arrange
        var account = new TradingAccount(Guid.NewGuid(), GetAccountTestInstrument());
        account.GrantInitialCash(1000m);
        account.TryReserveCash(600m);

        // Act
        account.ReleaseCash(500m);

        // Assert
        Assert.Equal(100m, account.ReservedCash);
        Assert.Equal(900m, account.AvailableCash);
    }
    
    [Fact]
    public void ReleaseCash_ShouldThrowWithoutChanges_WhenAmountExceedsReservation()
    {
        // Arrange
        var account = new TradingAccount(Guid.NewGuid(), GetAccountTestInstrument());
        account.GrantInitialCash(1000m);
        account.TryReserveCash(600m);

        // Act
        var act = () =>
            account.ReleaseCash(601m);

        // Assert
        Assert.Throws<InvalidOperationException>(act);
        Assert.Equal(600m, account.ReservedCash);
    }
    
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void ReleaseCash_ShouldThrow_WhenAmountIsNotPositive(decimal amount)
    {
        // Arrange
        var account = new TradingAccount(Guid.NewGuid(), GetAccountTestInstrument());

        // Act
        var act = () =>
            account.ReleaseCash(amount);

        // Assert
        Assert.Throws<ArgumentOutOfRangeException>(act);
    }
    
    [Fact]
    public void TryReserveInstruments_ShouldMoveQuantityFromAvailableToReserved()
    {
        // Arrange
        var account = new TradingAccount(Guid.NewGuid(), GetAccountTestInstrument());
        account.GrantInitialInstruments(30);

        // Act
        var isReserved = account.TryReserveInstruments(20);

        // Assert
        Assert.True(isReserved);
        Assert.Equal(30, account.Position.Quantity);
        Assert.Equal(20, account.Position.ReservedQuantity);
        Assert.Equal(10, account.Position.AvailableQuantity);
    }
    
    [Fact]
    public void TryReserveInstruments_ShouldReturnFalseWithoutChanges_WhenAvailableQuantityIsInsufficient()
    {
        // Arrange
        var account = new TradingAccount(Guid.NewGuid(), GetAccountTestInstrument());
        account.GrantInitialInstruments(30);

        // Act
        var isReserved = account.TryReserveInstruments(31);

        // Assert
        Assert.False(isReserved);
        Assert.Equal(0, account.Position.ReservedQuantity);
        Assert.Equal(30, account.Position.AvailableQuantity);
    }
    
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void TryReserveInstruments_ShouldThrow_WhenQuantityIsNotPositive(long quantity)
    {
        // Arrange
        var account = new TradingAccount(Guid.NewGuid(), GetAccountTestInstrument());

        // Act
        Action act = () =>
            account.TryReserveInstruments(quantity);

        // Assert
        Assert.Throws<ArgumentOutOfRangeException>(act);
    }
    
    [Fact]
    public void ReleaseInstruments_ShouldReturnReservedQuantityToAvailable()
    {
        // Arrange
        var account = new TradingAccount(Guid.NewGuid(), GetAccountTestInstrument());
        account.GrantInitialInstruments(50);
        account.TryReserveInstruments(20);

        // Act
        account.ReleaseInstruments(10);

        // Assert
        Assert.Equal(10, account.Position.ReservedQuantity);
        Assert.Equal(40, account.Position.AvailableQuantity);
    }
    
    [Fact]
    public void ReleaseInstruments_ShouldThrowWithoutChanges_WhenQuantityExceedsReservation()
    {
        // Arrange
        var account = new TradingAccount(Guid.NewGuid(), GetAccountTestInstrument());
        account.GrantInitialInstruments(30);
        account.TryReserveInstruments(20);

        // Act
        var act = () =>
            account.ReleaseInstruments(21);

        // Assert
        Assert.Throws<InvalidOperationException>(act);
        Assert.Equal(20, account.Position.ReservedQuantity);
    }
    
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void ReleaseInstruments_ShouldThrow_WhenQuantityIsNotPositive(long quantity)
    {
        // Arrange
        var account = new TradingAccount(Guid.NewGuid(), GetAccountTestInstrument());

        // Act
        var act = () =>
            account.ReleaseInstruments(quantity);

        // Assert
        Assert.Throws<ArgumentOutOfRangeException>(act);
    }
    
    [Fact]
    public void GetSnapshot_ShouldReturnValueUnaffectedByLaterChanges()
    {
        // Arrange
        var account = new TradingAccount(Guid.NewGuid(), GetAccountTestInstrument());
        var snapshot = account.GetSnapshot();

        // Act
        account.GrantInitialCash(1000m);
        account.GrantInitialInstruments(20);

        // Assert
        Assert.Equal(0m, snapshot.CashBalance);
        Assert.Equal(0, snapshot.Position.Quantity);
    }
    
    [Fact]
    public void GetOperations_ShouldReturnListUnaffectedByLaterChanges()
    {
        // Arrange
        var account = new TradingAccount(Guid.NewGuid(), GetAccountTestInstrument());
        account.GrantInitialCash(1000m);
        var operations = account.GetOperations();

        // Act
        account.GrantInitialInstruments(20);

        // Assert
        Assert.Single(operations);
        Assert.Equal(2, account.GetOperations().Count);
    }
}