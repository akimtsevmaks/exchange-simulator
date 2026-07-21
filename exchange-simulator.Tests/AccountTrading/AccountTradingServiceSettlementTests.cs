using exchange_simulator.Enums;
using exchange_simulator.Services;

namespace exchange_simulator.Tests.AccountTrading;

public class AccountTradingServiceSettlementTests : AccountTradingServiceTestBase
{
    [Fact]
    public void FilledBuy_ShouldDecreaseCashAndIncreasePositionByTradeValue()
    {
        // Arrange
        var service = new AccountTradingService(GetAccountTestInstrument());
        var sellerId = RegisterAccount(service, instruments: 10);
        var buyerId = RegisterAccount(service, cash: 1500m);
        PlaceLimit(service, sellerId, OrderSide.Sell, 10, 100m);

        // Act
        PlaceMarket(service, buyerId, OrderSide.Buy, 10);

        // Assert
        var buyer = GetAccount(service, buyerId);
        Assert.Equal(500m, buyer.CashBalance);
        Assert.Equal(0m, buyer.ReservedCash);
        Assert.Equal(500m, buyer.AvailableCash);
        Assert.Equal(10, buyer.Position.Quantity);
        Assert.Equal(100m, buyer.Position.AveragePrice);
    }
    
    [Fact]
    public void FilledSell_ShouldIncreaseCashAndDecreasePositionByTradeValue()
    {
        // Arrange
        var service = new AccountTradingService(GetAccountTestInstrument());
        var sellerId = RegisterAccount(service, instruments: 30);
        var buyerId = RegisterAccount(service, cash: 1000m);
        PlaceLimit(service, sellerId, OrderSide.Sell, 10, 100m);

        // Act
        PlaceMarket(service, buyerId, OrderSide.Buy, 10);

        // Assert
        var seller = GetAccount(service, sellerId);
        Assert.Equal(1000m, seller.CashBalance);
        Assert.Equal(20, seller.Position.Quantity);
        Assert.Equal(0, seller.Position.ReservedQuantity);
        Assert.Equal(20, seller.Position.AvailableQuantity);
    }
    
    [Fact]
    public void FilledLimitBuy_ShouldPayRestingPriceAndReleasePriceImprovement()
    {
        // Arrange
        var service = new AccountTradingService(GetAccountTestInstrument());
        var sellerId = RegisterAccount(service, instruments: 10);
        var buyerId = RegisterAccount(service, cash: 1000m);
        PlaceLimit(service, sellerId, OrderSide.Sell, 10, 90m);

        // Act
        PlaceLimit(service, buyerId, OrderSide.Buy, 10, 100m);

        // Assert
        var buyer = GetAccount(service, buyerId);
        Assert.Equal(100m, buyer.CashBalance);
        Assert.Equal(0m, buyer.ReservedCash);
        Assert.Equal(100m, buyer.AvailableCash);
        Assert.Equal(90m, buyer.Position.AveragePrice);
    }
    
    [Fact]
    public void PartiallyFilledLimitBuy_ShouldReserveRemainderAtLimitPrice()
    {
        // Arrange
        var service = new AccountTradingService(GetAccountTestInstrument());
        var sellerId = RegisterAccount(service, instruments: 10);
        var buyerId = RegisterAccount(service, cash: 3000m);
        PlaceLimit(service, sellerId, OrderSide.Sell, 10, 90m);

        // Act
        var result = PlaceLimit(service, buyerId, OrderSide.Buy, 30, 100m);

        // Assert
        var buyer = GetAccount(service, buyerId);
        Assert.Equal(2100m, buyer.CashBalance);
        Assert.Equal(2000m, buyer.ReservedCash);
        Assert.Equal(100m, buyer.AvailableCash);
        Assert.Equal(20, GetOrder(result).RemainingSize);
    }
    
    [Fact]
    public void PartiallyFilledRestingBuy_ShouldReduceReservationByFilledLimitValue()
    {
        // Arrange
        var service = new AccountTradingService(GetAccountTestInstrument());
        var buyerId = RegisterAccount(service, cash: 3000m);
        var sellerId = RegisterAccount(service, instruments: 10);
        PlaceLimit(service, buyerId, OrderSide.Buy, 30, 100m);

        // Act
        PlaceMarket(service, sellerId, OrderSide.Sell, 10);

        // Assert
        var buyer = GetAccount(service, buyerId);
        Assert.Equal(2000m, buyer.CashBalance);
        Assert.Equal(2000m, buyer.ReservedCash);
        Assert.Equal(0m, buyer.AvailableCash);
        Assert.Equal(10, buyer.Position.Quantity);
    }
    
    [Fact]
    public void PartiallyFilledRestingSell_ShouldKeepOnlyRemainingQuantityReserved()
    {
        // Arrange
        var service = new AccountTradingService(GetAccountTestInstrument());
        var sellerId = RegisterAccount(service, instruments: 30);
        var buyerId = RegisterAccount(service, cash: 1000m);
        PlaceLimit(service, sellerId, OrderSide.Sell, 30, 100m);

        // Act
        PlaceMarket(service, buyerId, OrderSide.Buy, 10);

        // Assert
        var seller = GetAccount(service, sellerId);
        Assert.Equal(20, seller.Position.Quantity);
        Assert.Equal(20, seller.Position.ReservedQuantity);
        Assert.Equal(0, seller.Position.AvailableQuantity);
    }
    
    [Fact]
    public void PartiallyFilledMarketBuy_ShouldReleaseAllUnusedCash()
    {
        // Arrange
        var service = new AccountTradingService(GetAccountTestInstrument());
        var sellerId = RegisterAccount(service, instruments: 10);
        var buyerId = RegisterAccount(service, cash: 2000m);
        PlaceLimit(service, sellerId, OrderSide.Sell, 10, 100m);

        // Act
        var result = PlaceMarket(service, buyerId, OrderSide.Buy, 20);

        // Assert
        var buyer = GetAccount(service, buyerId);
        Assert.Equal(OrderStatus.Cancelled, GetOrder(result).OrderStatus);
        Assert.Equal(1000m, buyer.CashBalance);
        Assert.Equal(0m, buyer.ReservedCash);
        Assert.Equal(1000m, buyer.AvailableCash);
    }
    
    [Fact]
    public void PartiallyFilledMarketSell_ShouldReleaseAllUnfilledInstruments()
    {
        // Arrange
        var service = new AccountTradingService(GetAccountTestInstrument());
        var buyerId = RegisterAccount(service, cash: 1000m);
        var sellerId = RegisterAccount(service, instruments: 30);
        PlaceLimit(service, buyerId, OrderSide.Buy, 10, 100m);

        // Act
        var result = PlaceMarket(service, sellerId, OrderSide.Sell, 30);

        // Assert
        var seller = GetAccount(service, sellerId);
        Assert.Equal(OrderStatus.Cancelled, GetOrder(result).OrderStatus);
        Assert.Equal(20, seller.Position.Quantity);
        Assert.Equal(0, seller.Position.ReservedQuantity);
        Assert.Equal(20, seller.Position.AvailableQuantity);
    }
    
    [Fact]
    public void BuyAcrossSeveralPrices_ShouldCalculateWeightedAveragePrice()
    {
        // Arrange
        var service = new AccountTradingService(GetAccountTestInstrument());
        var firstSeller = RegisterAccount(service, instruments: 10);
        var secondSeller = RegisterAccount(service, instruments: 20);
        var buyerId = RegisterAccount(service, cash: 4000m);
        PlaceLimit(service, firstSeller, OrderSide.Sell, 10, 100m);
        PlaceLimit(service, secondSeller, OrderSide.Sell, 20, 150m);

        // Act
        PlaceMarket(service, buyerId, OrderSide.Buy, 30);

        // Assert
        var buyer = GetAccount(service, buyerId);
        Assert.Equal(30, buyer.Position.Quantity);
        Assert.Equal(4000m / 30m, buyer.Position.AveragePrice);
        Assert.Equal(0m, buyer.CashBalance);
    }
    
    [Fact]
    public void BuyIntoExistingPosition_ShouldRecalculateWeightedAveragePrice()
    {
        // Arrange
        var service = new AccountTradingService(GetAccountTestInstrument(initialPrice: 100m));
        var buyerId = RegisterAccount(service, cash: 2000m, instruments: 10);
        var sellerId = RegisterAccount(service, instruments: 10);
        PlaceLimit(service, sellerId, OrderSide.Sell, 10, 200m);

        // Act
        PlaceMarket(service, buyerId, OrderSide.Buy, 10);

        // Assert
        var position = GetAccount(service, buyerId).Position;
        Assert.Equal(20, position.Quantity);
        Assert.Equal(150m, position.AveragePrice);
    }
    
    [Fact]
    public void PartialSell_ShouldKeepExistingAveragePrice()
    {
        // Arrange
        var service = new AccountTradingService(GetAccountTestInstrument(initialPrice: 150m));
        var buyerId = RegisterAccount(service, cash: 1200m);
        var sellerId = RegisterAccount(service, instruments: 20);
        PlaceLimit(service, buyerId, OrderSide.Buy, 10, 120m);

        // Act
        PlaceMarket(service, sellerId, OrderSide.Sell, 10);

        // Assert
        var position = GetAccount(service, sellerId).Position;
        Assert.Equal(10, position.Quantity);
        Assert.Equal(150m, position.AveragePrice);
    }
    
    [Fact]
    public void FullSell_ShouldResetAveragePriceToZero()
    {
        // Arrange
        var service = new AccountTradingService(GetAccountTestInstrument(initialPrice: 100m));
        var buyerId = RegisterAccount(service, cash: 1200m);
        var sellerId = RegisterAccount(service, instruments: 10);
        PlaceLimit(service, buyerId, OrderSide.Buy, 10, 120m);

        // Act
        PlaceMarket(service, sellerId, OrderSide.Sell, 10);

        // Assert
        var position = GetAccount(service, sellerId).Position;
        Assert.Equal(0, position.Quantity);
        Assert.Equal(0m, position.AveragePrice);
    }
    
    [Fact]
    public void Trade_ShouldCreateBuyOperationWithTradeReferencesAndDeltas()
    {
        // Arrange
        var instrument = GetAccountTestInstrument();
        var service = new AccountTradingService(instrument);
        var sellerId = RegisterAccount(service, instruments: 10);
        var buyerId = RegisterAccount(service, cash: 1000m);
        PlaceLimit(service, sellerId, OrderSide.Sell, 10, 100m);

        // Act
        var result = PlaceMarket(service, buyerId, OrderSide.Buy, 10);

        // Assert
        var order = GetOrder(result);
        var trade = Assert.Single(result.Trades);
        var operation = Assert.Single(
            service.GetAccountOperations(buyerId),
            item => item.Type == AccountOperationType.TradeBuy);
        Assert.Equal(-1000m, operation.CashChange);
        Assert.Equal(instrument.Id, operation.InstrumentId);
        Assert.Equal(10, operation.InstrumentQuantityChange);
        Assert.Equal(order.Id, operation.OrderId);
        Assert.Equal(trade.Id, operation.TradeId);
        Assert.Equal(trade.ExecutedAt, operation.CreatedAt);
    }
    
    [Fact]
    public void Trade_ShouldCreateSellOperationWithTradeReferencesAndDeltas()
    {
        // Arrange
        var instrument = GetAccountTestInstrument();
        var service = new AccountTradingService(instrument);
        var sellerId = RegisterAccount(service, instruments: 10);
        var buyerId = RegisterAccount(service, cash: 1000m);
        var sell = PlaceLimit(service, sellerId, OrderSide.Sell, 10, 100m);

        // Act
        var result = PlaceMarket(service, buyerId, OrderSide.Buy, 10);

        // Assert
        var trade = Assert.Single(result.Trades);
        var operation = Assert.Single(
            service.GetAccountOperations(sellerId),
            item => item.Type == AccountOperationType.TradeSell);
        Assert.Equal(1000m, operation.CashChange);
        Assert.Equal(instrument.Id, operation.InstrumentId);
        Assert.Equal(-10, operation.InstrumentQuantityChange);
        Assert.Equal(GetOrder(sell).Id, operation.OrderId);
        Assert.Equal(trade.Id, operation.TradeId);
        Assert.Equal(trade.ExecutedAt, operation.CreatedAt);
    }
    
    [Fact]
    public void SelfTrade_ShouldKeepTotalsUnchangedAndReleaseBothReservations()
    {
        // Arrange
        var service = new AccountTradingService(GetAccountTestInstrument(initialPrice: 100m));
        var accountId = RegisterAccount(service, cash: 2000m, instruments: 20);
        PlaceLimit(service, accountId, OrderSide.Sell, 10, 110m);

        // Act
        PlaceMarket(service, accountId, OrderSide.Buy, 10);

        // Assert
        var account = GetAccount(service, accountId);
        Assert.Equal(2000m, account.CashBalance);
        Assert.Equal(0m, account.ReservedCash);
        Assert.Equal(20, account.Position.Quantity);
        Assert.Equal(0, account.Position.ReservedQuantity);
        Assert.Equal(100m, account.Position.AveragePrice);
        Assert.Single(service.GetAccountOperations(accountId),
            item => item.Type == AccountOperationType.TradeBuy);
        Assert.Single(service.GetAccountOperations(accountId),
            item => item.Type == AccountOperationType.TradeSell);
    }
}