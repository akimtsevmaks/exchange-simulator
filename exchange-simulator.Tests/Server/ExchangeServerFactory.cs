using exchange_simulator.Bots;
using exchange_simulator.Enums;
using exchange_simulator.Models.TradingCore;
using exchange_simulator.Server;
using exchange_simulator.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using TestMarket = exchange_simulator.Services.LocalMarket;

namespace exchange_simulator.Tests.Server;

internal sealed class ExchangeServerFactory : WebApplicationFactory<LocalMarketHostedService>
{
    private ExchangeServerFactory(TestMarket market, ObservableTradingBot? observableBot)
    {
        Market =  market;
        ObservableBot =  observableBot;
    }

    public ExchangeServerFactory() : this(CreateMarket(TimeSpan.FromHours(1)), null) { }
    
    public TestMarket Market { get; }
    public ObservableTradingBot? ObservableBot { get; }


    public static ExchangeServerFactory CreateWithObservableBot(TimeSpan stepInterval)
    {
        ObservableTradingBot? observableBot = null;

        var market = new TestMarket(
            CreateInstrument(),
            initialCashPerAccount: 100000m,
            initialInstrumentsPerAccount: 1000,
            stepInterval,
            botFactory: currentMarket =>
            {
                observableBot = new ObservableTradingBot(currentMarket);
                return [observableBot];
            });
        
        return new ExchangeServerFactory(market, observableBot ??
                                                 throw new InvalidOperationException("The observable bot was not created"));
    }

    public static ExchangeServerFactory CreateWithInitialInstruments(long initialInstrumentsPerAccount) =>
        new(CreateMarket(TimeSpan.FromHours(1), initialInstrumentsPerAccount), null);

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Production");
        builder.ConfigureLogging(logging => logging.ClearProviders());
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<TestMarket>();
            services.AddSingleton(Market);
        });
    }
    

    private static TestMarket CreateMarket(TimeSpan stepInterval, long initialInstrumentsPerAccount = 1000) =>
        new(
            CreateInstrument(),
            initialCashPerAccount: 100000m,
            initialInstrumentsPerAccount,
            stepInterval,
            marketMakerOptions: new MarketMakerBotOptions(
                QuoteOffset: 1m,
                OrderSize: 10),
            noiseBotOptions: new NoiseBotOptions(
                RandomSeed: 111,
                PriceOffset: 3m,
                MaxOrderLots: 5,
                MaxActiveOrders: 10));
    
    private static Instrument CreateInstrument() =>
        new(
            Guid.NewGuid(),
            "TEST",
            "Test Instrument",
            lotSize: 1,
            initialPrice: 100m);
}



internal sealed class ObservableTradingBot(TestMarket market) : ITradingBot
{
    private readonly TestMarket _market = market;
    
    private int _stepCount;
    public int StepCount => Volatile.Read(ref _stepCount);
    
    public Guid AccountId { get; } = market.MarketMakerAccountId;


    public void ExecuteStep()
    {
        var nextStep = StepCount + 1;
        var result = market.PlaceOrder(new PlaceOrderCommand(
            AccountId,
            OrderSide.Buy,
            OrderType.Limit,
            Size: 1,
            Price: 50m + nextStep));

        if (!result.IsSuccess)
        {
            throw new InvalidOperationException(
                $"The observable bot order was rejected with '{result.RejectionReason}'");
        }
        
        Volatile.Write(ref _stepCount, nextStep);
    }

    public void Stop()
    {
        foreach (var order in _market.GetActiveOrders(AccountId))
            _market.CancelOrder(order.Id);
    }
}