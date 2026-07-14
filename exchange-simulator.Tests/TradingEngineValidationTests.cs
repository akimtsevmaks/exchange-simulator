using exchange_simulator.Enums;
using exchange_simulator.Models;

namespace exchange_simulator.Tests;

public class TradingEngineValidationTests
{
    private static readonly Guid InstrumentId = Guid.NewGuid();
    private static readonly Guid OwnerId = Guid.NewGuid();
    private static Instrument GetTestInstrument() => 
        new Instrument( InstrumentId, "TEST", "Test Instrument", 10, 100);

    public static TheoryData<PlaceOrderCommand, OrderRejectionReason> InvalidCommands =>
    new () {
        {
            new PlaceOrderCommand(
                Guid.Empty,
                OrderSide.Buy,
                OrderType.Limit,
                10,
                100m),
            OrderRejectionReason.InvalidOwnerId
        },
        {
            new PlaceOrderCommand(
                OwnerId,
                (OrderSide)9999,
                OrderType.Limit,
                10,
                100m),
            OrderRejectionReason.InvalidOrderSide
        },
        {
            new PlaceOrderCommand(
                OwnerId,
                OrderSide.Buy,
                (OrderType)9999,
                10,
                100m),
            OrderRejectionReason.InvalidOrderType
        },
        {
            new PlaceOrderCommand(
                OwnerId,
                OrderSide.Buy,
                OrderType.Limit,
                0,
                100m),
            OrderRejectionReason.InvalidSize
        },
        {
            new PlaceOrderCommand(
                OwnerId,
                OrderSide.Buy,
                OrderType.Limit,
                -10,
                100m),
            OrderRejectionReason.InvalidSize
        },
        {
            new PlaceOrderCommand(
                OwnerId,
                OrderSide.Buy,
                OrderType.Limit,
                1,
                100m),
            OrderRejectionReason.QuantityNotMultipleOfLotSize
        },
        {
            new PlaceOrderCommand(
                OwnerId,
                OrderSide.Buy,
                OrderType.Limit,
                101,
                100m),
            OrderRejectionReason.QuantityNotMultipleOfLotSize
        },
        {
            new PlaceOrderCommand(
                OwnerId,
                OrderSide.Buy,
                OrderType.Limit,
                10,
                null),
            OrderRejectionReason.PriceRequiredForLimitOrder
        },
        {
            new PlaceOrderCommand(
                OwnerId,
                OrderSide.Buy,
                OrderType.Market,
                10,
                100m),
            OrderRejectionReason.PriceNotAllowedForMarketOrder
        },
        {
            new PlaceOrderCommand(
                OwnerId,
                OrderSide.Buy,
                OrderType.Limit,
                10,
                0),
            OrderRejectionReason.InvalidPrice
        },
        {
            new PlaceOrderCommand(
                OwnerId,
                OrderSide.Buy,
                OrderType.Limit,
                10,
                -100m),
            OrderRejectionReason.InvalidPrice
        }
    };

    [Theory]
    [MemberData(nameof(InvalidCommands))]
    public void PlaceOrder_ShouldRejectInvalidCommand_WithoutSideEffects(
        PlaceOrderCommand invalidCommand, OrderRejectionReason expectedReason)
    {
        // Arrange
        var engine = new TradingEngine(GetTestInstrument());

        var existingOrderResult = engine.PlaceOrder(
            new PlaceOrderCommand(Guid.NewGuid(), OrderSide.Buy, OrderType.Limit, 100, 100m));

        Assert.True(existingOrderResult.IsSuccess);
        Assert.NotNull(existingOrderResult.Order);
        
        var existingOrderId = existingOrderResult.Order.Id;

        var orderBookBefore = engine.GetOrderBookSnapshot();
        var tradesBefore = engine.GetTrades();
        var activeOrdersBefore = engine.GetActiveOrders();
        var referencePriceBefore = engine.GetReferencePrice();
        var existingOrderFoundBefore = engine.TryGetOrder(existingOrderId, out var existingOrderBefore);
        
        Assert.True(existingOrderFoundBefore);
        Assert.NotNull(existingOrderBefore);
        
        // Act
        var result = engine.PlaceOrder(invalidCommand);
        
        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(expectedReason, result.RejectionReason);
        Assert.Null(result.Order);
        Assert.Empty(result.Trades);
        
        var orderBookAfter = engine.GetOrderBookSnapshot();
        
        Assert.Equal(orderBookBefore.InstrumentId, orderBookAfter.InstrumentId);
        Assert.Equal(orderBookBefore.Bids, orderBookAfter.Bids);
        Assert.Equal(orderBookBefore.Asks, orderBookAfter.Asks);
        
        Assert.Equal(tradesBefore, engine.GetTrades());
        Assert.Equal(activeOrdersBefore, engine.GetActiveOrders());
        
        var existingOrderFoundAfter = engine.TryGetOrder(existingOrderId, out var existingOrderAfter);
        
        Assert.True(existingOrderFoundAfter);
        Assert.NotNull(existingOrderAfter);
        Assert.Equal(existingOrderBefore,  existingOrderAfter);
        Assert.Equal(referencePriceBefore, engine.GetReferencePrice());
    }
}