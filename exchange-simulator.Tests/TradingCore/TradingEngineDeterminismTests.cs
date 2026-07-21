using exchange_simulator.Enums;
using exchange_simulator.Models;

namespace exchange_simulator.Tests;

public class TradingEngineDeterminismTests : TradingEngineTestBase
{
    private sealed record OrderState(
        OrderType Type,
        OrderSide Side,
        OrderStatus Status,
        decimal? Price,
        long Size,
        long RemainingSize,
        long FillSize
        );

    private sealed record TradeState(
        decimal Price,
        long Size
        );

    [Fact]
    public void SameCommandSequence_ShouldProduceSameEconomicState()
    {
        // Arrange
        var instrumentId = Guid.NewGuid();
        var owners = new[]
        {
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid()
        };
        
        // Act
        var firstState = RunScenario(GetTestInstrument(instrumentId), owners);
        var secondState = RunScenario(GetTestInstrument(instrumentId), owners);
        
        // Assert
        Assert.Equal(firstState.Orders, secondState.Orders);
        Assert.Equal(firstState.Trades, secondState.Trades);
        Assert.Equal(firstState.Bids, secondState.Bids);
        Assert.Equal(firstState.Asks, secondState.Asks);
        Assert.Equal(firstState.ReferencePrice, secondState.ReferencePrice);
    }

    private static
        (OrderState[] Orders,
        TradeState[] Trades,
        OrderBookLevel[] Bids,
        OrderBookLevel[] Asks,
        decimal ReferencePrice) RunScenario(Instrument instrument, IReadOnlyList<Guid> owners)
    {
        var engine = new TradingEngine(instrument);

        var results = new[]
        {
            engine.PlaceOrder(GetLimitCommand(OrderSide.Sell, 20, 100m, owners[0])),
            engine.PlaceOrder(GetLimitCommand(OrderSide.Sell, 30, 110m, owners[1])),
            engine.PlaceOrder(GetLimitCommand(OrderSide.Buy, 10, 90m, owners[2])),
            engine.PlaceOrder(GetMarketCommand(OrderSide.Buy, 30, owners[3]))
        };
        
        var orderToCancel = GetResultOrder(results[2]);
        engine.CancelOrder(orderToCancel.Id);
        
        var orders = results.Select(result =>
        {
            var resultOrder = GetResultOrder(result);
            var snapshot = GetStoredOrder(engine, resultOrder.Id);

            return new OrderState(
                snapshot.OrderType,
                snapshot.OrderSide,
                snapshot.OrderStatus,
                snapshot.Price,
                snapshot.Size,
                snapshot.RemainingSize,
                snapshot.FilledSize);
        }).ToArray();
        
        var trades = engine.GetTrades().Select(trade => 
            new TradeState(trade.Price, trade.Size)).ToArray();

        var orderBook = engine.GetOrderBookSnapshot();

        return (orders,
            trades,
            orderBook.Bids.ToArray(),
            orderBook.Asks.ToArray(),
            engine.GetReferencePrice());
    }
}