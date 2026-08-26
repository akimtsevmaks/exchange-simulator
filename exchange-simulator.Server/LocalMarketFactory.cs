using exchange_simulator.Bots;
using exchange_simulator.Models.AccountTrading;
using exchange_simulator.Models.TradingCore;
using exchange_simulator.Services;


namespace exchange_simulator.Server;


internal sealed class LocalMarketFactory
{
    private static readonly TimeSpan StepInterval = TimeSpan.FromSeconds(1);
    
    private static readonly MarketMakerBotOptions MarketMakerOptions = new(
        QuoteOffset: 1m,
        OrderSize: 10);
    private static readonly NoiseBotOptions NoiseBotOptions = new(
        RandomSeed: 111,
        PriceOffset: 3m,
        MaxOrderLots: 5,
        MaxActiveOrders: 20);
    
    internal decimal InitialCashPerAccount => 100000m;
    internal long InitialInstrumentsPerAccount => 1000;
    
    internal LocalMarket CreateNew()
    {
        var instrument = new Instrument(
            Guid.NewGuid(),
            "TEST",
            "Test Instrument",
            lotSize: 1,
            initialPrice: 100m);

        return new LocalMarket(
            instrument,
            InitialCashPerAccount,
            InitialInstrumentsPerAccount,
            StepInterval,
            MarketMakerOptions,
            NoiseBotOptions);
    }
    
    internal LocalMarket Restore(
        Instrument instrument,
        AccountTradingRestoreState tradingState,
        Guid marketMakerAccountId,
        Guid noiseBotAccountId,
        Guid manualAccountId) =>
        LocalMarket.Restore(
            instrument,
            tradingState,
            marketMakerAccountId,
            noiseBotAccountId,
            manualAccountId,
            StepInterval,
            MarketMakerOptions,
            NoiseBotOptions);
}