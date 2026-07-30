using exchange_simulator.Bots;
using exchange_simulator.Enums;
using exchange_simulator.Models.AccountTrading;
using exchange_simulator.Models.TradingCore;

namespace exchange_simulator.Tests.LocalMarket;

public class NoiseBotTests : LocalMarketTestBase
{
    private sealed record AccountState(
        decimal Cash,
        decimal ReservedCash,
        long Quantity,
        long ReservedQuantity);

    private sealed record ActiveOrderState(
        OrderSide Side,
        decimal? Price,
        long Size,
        long RemainingSize);

    private sealed record TradeState(
        decimal Price,
        long Size);

    private sealed record ScenarioState(
        decimal ReferencePrice,
        OrderBookLevel[] Bids,
        OrderBookLevel[] Asks,
        AccountState MarketMaker,
        AccountState NoiseBot,
        ActiveOrderState[] ActiveOrders,
        TradeState[] Trades);

    [Fact]
    public void Constructor_ShouldBindBotToNoiseAccount()
    {
        // Arrange
        var market = GetMarket();

        // Act
        var noiseBot = new NoiseBot(
            market,
            randomSeed: DeterministicNoiseSeed,
            priceOffset: 3m,
            maxOrderLots: 3,
            maxActiveOrders: 5);

        // Assert
        Assert.Equal(
            market.NoiseBotAccountId,
            noiseBot.AccountId);
    }

    [Fact]
    public void SameSeed_ShouldProduceSameEconomicState()
    {
        // Act
        var firstState = RunScenario(DeterministicNoiseSeed);
        var secondState = RunScenario(DeterministicNoiseSeed);

        // Assert
        Assert.Equal(firstState.ReferencePrice, secondState.ReferencePrice);
        Assert.Equal(firstState.Bids, secondState.Bids);
        Assert.Equal(firstState.Asks, secondState.Asks);
        Assert.Equal(firstState.MarketMaker, secondState.MarketMaker);
        Assert.Equal(firstState.NoiseBot, secondState.NoiseBot);
        Assert.Equal(firstState.ActiveOrders, secondState.ActiveOrders);
        Assert.Equal(firstState.Trades, secondState.Trades);
    }

    [Fact]
    public void ExecuteStep_ShouldNeverExceedActiveOrderLimit()
    {
        // Arrange
        const int maxActiveOrders = 2;
        var market = GetMarket();
        var marketMaker = new MarketMakerBot(market, quoteOffset: 1m, orderSize: 10);
        var noiseBot = new NoiseBot(market, randomSeed: DeterministicNoiseSeed, priceOffset: 3m, maxOrderLots: 3, maxActiveOrders);

        marketMaker.ExecuteStep();

        // Act & Assert
        for (var step = 0; step < 500; step++)
        {
            noiseBot.ExecuteStep();

            Assert.InRange(
                market.GetActiveOrders(market.NoiseBotAccountId).Count,
                0, maxActiveOrders);
        }
    }

    [Fact]
    public void RepeatedExecution_WithInsufficientResources_ShouldPreserveAccountInvariants()
    {
        // Arrange
        const int stressStepCount = 500;

        var market = GetMarket(initialCash: 1m, initialInstruments: 1);
        var noiseBot = new NoiseBot(market, randomSeed: DeterministicNoiseSeed, priceOffset: 3m, maxOrderLots: 2, maxActiveOrders: 2);

        // Act & Assert
        for (var step = 0; step < stressStepCount; step++)
        {
            noiseBot.ExecuteStep();

            var account = GetAccount(market, market.NoiseBotAccountId);

            Assert.True(account.CashBalance >= 0);
            Assert.True(account.ReservedCash >= 0);
            Assert.True(account.AvailableCash >= 0);
            Assert.Equal(
                account.CashBalance,
                account.ReservedCash + account.AvailableCash);

            Assert.True(account.Position.Quantity >= 0);
            Assert.True(account.Position.ReservedQuantity >= 0);
            Assert.True(account.Position.AvailableQuantity >= 0);
            Assert.Equal(
                account.Position.Quantity,
                account.Position.ReservedQuantity +
                account.Position.AvailableQuantity);
        }
    }

    private static ScenarioState RunScenario(int randomSeed)
    {
        var instrumentId = Guid.NewGuid();
        var market = GetMarket(instrumentId: instrumentId);
        var marketMaker = new MarketMakerBot(market, quoteOffset: 1m, orderSize: 10);
        var noiseBot = new NoiseBot(market, randomSeed, priceOffset: 3m, maxOrderLots: 3, maxActiveOrders: 5);

        marketMaker.ExecuteStep();

        for (var step = 0; step < 100; step++)
            noiseBot.ExecuteStep();

        var orderBook = market.GetOrderBookSnapshot();
        var activeOrders = new[]
            {
                market.GetActiveOrders(market.MarketMakerAccountId),
                market.GetActiveOrders(market.NoiseBotAccountId)
            }
            .SelectMany(orders => orders)
            .Select(order => new ActiveOrderState(
                order.OrderSide,
                order.Price,
                order.Size,
                order.RemainingSize))
            .OrderBy(order => order.Side)
            .ThenBy(order => order.Price)
            .ThenBy(order => order.Size)
            .ThenBy(order => order.RemainingSize)
            .ToArray();
        var trades = market.GetTrades()
            .Select(trade => new TradeState(trade.Price, trade.Size)).ToArray();

        return new ScenarioState(
            market.GetReferencePrice(),
            orderBook.Bids.ToArray(),
            orderBook.Asks.ToArray(),
            GetAccountState(GetAccount(market, market.MarketMakerAccountId)),
            GetAccountState(GetAccount(market, market.NoiseBotAccountId)),
            activeOrders,
            trades);
    }

    private static AccountState GetAccountState(TradingAccountSnapshot account) =>
        new(
            account.CashBalance,
            account.ReservedCash,
            account.Position.Quantity,
            account.Position.ReservedQuantity);
}
