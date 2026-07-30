using exchange_simulator.Bots;
using exchange_simulator.Enums;
using exchange_simulator.Models.AccountTrading;
using exchange_simulator.Models.TradingCore;
using LocalMarketService = exchange_simulator.Services.LocalMarket;

namespace exchange_simulator.Tests.LocalMarket;

public abstract class LocalMarketTestBase
{
    protected const int DeterministicNoiseSeed = 111;
    
    protected sealed class ScriptedTradingBot(
        LocalMarketService market,
        Guid accountId,
        Action<BotTradingContext> executeStep) : ITradingBot
    {
        private readonly BotTradingContext _context = new BotTradingContext(market, accountId);
        private readonly Action<BotTradingContext> _executeStep = executeStep;
        private bool _isStopped;

        public Guid AccountId => _context.AccountId;

        public void ExecuteStep()
        {
            if (!_isStopped)
                _executeStep(_context);
        }

        public void Stop()
        {
            _isStopped = true;
            _context.CancelAllActiveOrders();
        }
    }
    
    protected static LocalMarketService GetMarket(
        decimal initialCash = 100000m,
        long initialInstruments = 1000,
        long lotSize = 1,
        decimal initialPrice = 100m,
        TimeSpan? stepInterval = null,
        MarketMakerBotOptions? marketMakerOptions = null,
        NoiseBotOptions? noiseBotOptions = null,
        Guid? instrumentId = null)
    {
        var instrument = new Instrument(
            instrumentId ?? Guid.NewGuid(),
            "TEST",
            "Test Instrument",
            lotSize,
            initialPrice);

        return new LocalMarketService(
            instrument,
            initialCash,
            initialInstruments,
            stepInterval ?? TimeSpan.FromSeconds(1),
            marketMakerOptions ?? new MarketMakerBotOptions(1m, 10),
            noiseBotOptions ?? new NoiseBotOptions(DeterministicNoiseSeed, 3m, 5, 10));
    }

    protected static LocalMarketService GetMarket(
        Func<LocalMarketService, IReadOnlyList<ITradingBot>> botFactory,
        TimeSpan? stepInterval = null)
    {
        var instrument = new Instrument(
            Guid.NewGuid(),
            "TEST",
            "Test Instrument",
            lotSize: 1,
            initialPrice: 100m);

        return new LocalMarketService(
            instrument,
            initialCashPerAccount: 100000m,
            initialInstrumentsPerAccount: 1000,
            stepInterval: stepInterval ?? TimeSpan.FromSeconds(1),
            botFactory: botFactory);
    }
    
    protected static LocalMarketService GetMarketWithScriptedNoiseBot(
        Action<BotTradingContext> executeNoiseStep) =>
        GetMarket(market =>
        [
            new MarketMakerBot(
                market,
                quoteOffset: 1m,
                orderSize: 10),
            new ScriptedTradingBot(
                market,
                market.NoiseBotAccountId,
                executeNoiseStep)
        ]);

    protected static TradingAccountSnapshot GetAccount(LocalMarketService market, Guid accountId)
    {
        var isFound = market.TryGetAccount(accountId, out var account);

        Assert.True(isFound);
        return Assert.IsType<TradingAccountSnapshot>(account);
    }

    protected static OrderCommandResult PlaceLimit(
        LocalMarketService market,
        Guid accountId,
        OrderSide side,
        long size,
        decimal price) =>
        market.PlaceOrder(new PlaceOrderCommand(accountId, side, OrderType.Limit, size, price));

    protected static OrderCommandResult PlaceMarket(
        LocalMarketService market,
        Guid accountId,
        OrderSide side,
        long size) =>
        market.PlaceOrder(new PlaceOrderCommand(accountId, side, OrderType.Market, size));

    protected static OrderSnapshot GetOrder(OrderCommandResult result) =>
        Assert.IsType<OrderSnapshot>(result.Order);

    protected static OrderSnapshot GetStoredOrder(LocalMarketService market, Guid accountId, Guid orderId)
    {
        var isFound = market.TryGetOrder(accountId, orderId, out var order);

        Assert.True(isFound);
        return Assert.IsType<OrderSnapshot>(order);
    }
}
