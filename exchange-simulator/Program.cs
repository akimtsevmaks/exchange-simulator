using exchange_simulator.Bots;
using exchange_simulator.Clients;
using exchange_simulator.Models.TradingCore;
using exchange_simulator.Services;

namespace exchange_simulator;

internal static class Program
{
    public static async Task Main()
    {
        var instrument = new Instrument(
            Guid.NewGuid(),
            "TEST",
            "Test Instrument",
            lotSize: 1,
            initialPrice: 100m);

        var market = new LocalMarket(
            instrument,
            initialCashPerAccount: 100000m,
            initialInstrumentsPerAccount: 1000,
            stepInterval: TimeSpan.FromSeconds(1),
            marketMakerOptions: new MarketMakerBotOptions(
                QuoteOffset: 1m,
                OrderSize: 10),
            noiseBotOptions: new NoiseBotOptions(
                RandomSeed: 111,
                PriceOffset: 3m,
                MaxOrderLots: 5,
                MaxActiveOrders: 10));

        var client = new LocalMarketConsoleClient(market);
        using var shutdown = new CancellationTokenSource();

        ConsoleCancelEventHandler cancelHandler = (_, eventArgs) =>
        {
            eventArgs.Cancel = true;
            shutdown.Cancel();
        };

        Console.CancelKeyPress += cancelHandler;

        try
        {
            await market.StartAsync(shutdown.Token);
            await client.RunAsync(shutdown.Token);
        }
        finally
        {
            shutdown.Cancel();
            await market.StopAsync();
            Console.CancelKeyPress -= cancelHandler;
        }
    }
}