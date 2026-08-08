using System.Text.Json;
using System.Text.Json.Serialization;

using exchange_simulator.Bots;
using exchange_simulator.Models.TradingCore;
using exchange_simulator.Server;
using exchange_simulator.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.ConfigureHttpJsonOptions(option => 
    option.SerializerOptions.Converters.Add(
        new JsonStringEnumConverter(
            JsonNamingPolicy.CamelCase,
            allowIntegerValues: false)));

builder.Services.AddSingleton(CreateMarket());
builder.Services.AddHostedService<LocalMarketHostedService>();

var app = builder.Build();

app.MapPublicMarketEndpoints();

app.Run();

static LocalMarket CreateMarket()
{
    var instrument = new Instrument(
        Guid.NewGuid(),
        "TEST",
        "Test Instrument",
        lotSize: 1,
        initialPrice: 100m);

    return new LocalMarket(
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
            MaxActiveOrders: 20));
}
