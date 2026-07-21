using exchange_simulator.Enums;
using exchange_simulator.Models;

namespace exchange_simulator.Tests.TradingCore;

public class TradingEngineTestBase
{
    protected sealed record EngineState(
        OrderBookLevel[] Bids,
        OrderBookLevel[] Asks,
        OrderSnapshot[] ActiveOrders,
        Trade[] Trades
    );
    
    protected static Instrument GetTestInstrument(
        Guid? id = null,
        long lotSize = 10,
        decimal initialPrice = 100m) =>
        new Instrument(id ?? Guid.NewGuid(), "TEST", "Test Instrument", lotSize, initialPrice);
    
    protected static OrderSide GetOppositeSide(OrderSide side) =>
        side == OrderSide.Buy ? OrderSide.Sell : OrderSide.Buy;
    
    protected static PlaceOrderCommand GetLimitCommand(
        OrderSide side,
        long size = 10,
        decimal price = 100m,
        Guid? ownerId = null) =>
        new PlaceOrderCommand(ownerId ?? Guid.NewGuid(), side, OrderType.Limit, size, price);
    
    protected static PlaceOrderCommand GetMarketCommand(
        OrderSide side,
        long size = 10,
        Guid? ownerId = null) =>
        new PlaceOrderCommand(ownerId ?? Guid.NewGuid(), side, OrderType.Market, size);

    protected static OrderSnapshot GetResultOrder(OrderCommandResult result) =>
        Assert.IsType<OrderSnapshot>(result.Order);

    protected static OrderSnapshot GetStoredOrder(TradingEngine engine, Guid orderId)
    {
        engine.TryGetOrder(orderId, out var order);
        return Assert.IsType<OrderSnapshot>(order);
    }

    protected static EngineState CaptureState(TradingEngine engine)
    {
        var orderBook = engine.GetOrderBookSnapshot();

        return new EngineState(
            orderBook.Bids.ToArray(),
            orderBook.Asks.ToArray(),
            engine.GetActiveOrders().ToArray(),
            engine.GetTrades().ToArray());
    }

    protected static void AssertStateIsUnchanged(EngineState expected, TradingEngine engine)
    {
        var actual = CaptureState(engine);
        
        Assert.Equal(expected.Bids, actual.Bids);
        Assert.Equal(expected.Asks, actual.Asks);
        Assert.Equal(expected.ActiveOrders, actual.ActiveOrders);
        Assert.Equal(expected.Trades, actual.Trades);
    }
}