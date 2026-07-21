using exchange_simulator.Enums;
using exchange_simulator.Models;
using exchange_simulator.Models.TradingCore;

namespace exchange_simulator.Tests.TradingCore;

public class TradingEngineValidationTests : TradingEngineTestBase
{
    private static readonly Guid OwnerId = Guid.NewGuid();

    private static PlaceOrderCommand GetCommand(
        Guid? ownerId = null,
        OrderSide side = OrderSide.Buy,
        OrderType type = OrderType.Limit,
        long size = 10,
        decimal? price = 100m) =>
        new(ownerId ?? OwnerId, side, type, size, price);
    
    public static TheoryData<PlaceOrderCommand, OrderRejectionReason> InvalidCommands =>
    new () {
        { 
            GetCommand(ownerId: Guid.Empty),
            OrderRejectionReason.InvalidOwnerId },
        {
            GetCommand(side: (OrderSide)9999),
            OrderRejectionReason.InvalidOrderSide },
        {
            GetCommand(type: (OrderType)9999),
            OrderRejectionReason.InvalidOrderType
        },
        {
            GetCommand(size: 0),
            OrderRejectionReason.InvalidSize
        },
        {
            GetCommand(size: -10),
            OrderRejectionReason.InvalidSize
        },
        {
            GetCommand(size: 1),
            OrderRejectionReason.QuantityNotMultipleOfLotSize
        },
        {
            GetCommand(size: 101),
            OrderRejectionReason.QuantityNotMultipleOfLotSize
        },
        {
            GetCommand(type: OrderType.Limit, price: null),
            OrderRejectionReason.PriceRequiredForLimitOrder
        },
        {
            GetCommand(type: OrderType.Market, price: 100m),
            OrderRejectionReason.PriceNotAllowedForMarketOrder
        },
        {
            GetCommand(price: 0),
            OrderRejectionReason.InvalidPrice
        },
        {
            GetCommand(price: -100m),
            OrderRejectionReason.InvalidPrice
        }
    };

    [Theory]
    [MemberData(nameof(InvalidCommands))]
    public void PlaceOrder_ShouldRejectInvalidCommand(
        PlaceOrderCommand invalidCommand, OrderRejectionReason expectedReason)
    {
        // Arrange
        var engine = new TradingEngine(GetTestInstrument());
        
        // Act
        var result = engine.PlaceOrder(invalidCommand);
        
        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(expectedReason, result.RejectionReason);
        Assert.Null(result.Order);
        Assert.Empty(result.Trades);
    }

    [Fact]
    public void PlaceOrder_ShouldNotChangeState_WhenCommandIsRejected()
    {
        // Arrange
        var engine = new TradingEngine(GetTestInstrument());

        engine.PlaceOrder(GetLimitCommand(OrderSide.Sell, price: 90m));
        engine.PlaceOrder(GetMarketCommand(OrderSide.Buy));
        engine.PlaceOrder(GetLimitCommand(OrderSide.Buy, price: 100m));
        
        var stateBefore = CaptureState(engine);
        var invalidCommand = GetLimitCommand(OrderSide.Buy, price: 0m);
        
        // Act
        engine.PlaceOrder(invalidCommand);
        
        // Assert
        AssertStateIsUnchanged(stateBefore, engine);
    }
}