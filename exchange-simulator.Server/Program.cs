using System.Text.Json;
using System.Text.Json.Serialization;

using exchange_simulator.Server.Persistence;
using Microsoft.EntityFrameworkCore;

using exchange_simulator.Server;
using exchange_simulator.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddConsole();

builder.Services.ConfigureHttpJsonOptions(option => 
    option.SerializerOptions.Converters.Add(
        new JsonStringEnumConverter(
            JsonNamingPolicy.CamelCase,
            allowIntegerValues: false)));

builder.Services.Configure<RouteHandlerOptions>(options =>
    options.ThrowOnBadRequest = true);

builder.Services.AddDbContext<ExchangeDbContext>(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("ExchangeDatabase") ??
        throw new InvalidOperationException(
            "Connection string 'ExchangeDatabase' is not configured")));

builder.Services.AddSingleton<LocalMarketFactory>();
builder.Services.AddSingleton<LocalMarketAccessor>();

builder.Services.AddSingleton<LocalMarket>(services =>
    services.GetRequiredService<LocalMarketAccessor>().Market);

builder.Services.AddSingleton<TestParticipant>();
builder.Services.AddHostedService<LocalMarketHostedService>();

var app = builder.Build();

app.UseMiddleware<ApiErrorMiddleware>();

app.MapPublicMarketEndpoints();
app.MapPersonalAccountEndpoints();
app.MapTradingCommandEndpoints();

app.Run();
