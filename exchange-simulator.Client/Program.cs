using exchange_simulator.Client;

const string defaultServerAddress = "http://localhost:5000/";

if (args.Length > 1)
{
    Console.Error.WriteLine("Usage: exchange-simulator.Client [server-url]");
    return 1;
}

var serverAddress = args.Length == 1 ? args[0] : defaultServerAddress;

if (!TryCreateServerUri(serverAddress, out var serverUri))
{
    Console.Error.WriteLine("Server URL must be an absolute HTTP or HTTPS address without a query or fragment.");
    return 1;
}

using var httpClient = new HttpClient
{
    BaseAddress = serverUri,
    Timeout = TimeSpan.FromSeconds(5)
};

httpClient.DefaultRequestHeaders.Accept.ParseAdd("application/json");

var apiClient = new MarketApiClient(httpClient);
var consoleClient = new MarketConsoleClient(apiClient);
using var shutdown = new CancellationTokenSource();

ConsoleCancelEventHandler cancelHandler = (_, eventArgs) =>
{
    eventArgs.Cancel = true;
    shutdown.Cancel();
};

Console.CancelKeyPress += cancelHandler;

try
{
    await consoleClient.RunAsync(shutdown.Token);
    return 0;
}
finally
{
    Console.CancelKeyPress -= cancelHandler;
}


static bool TryCreateServerUri(string value, out Uri serverUri)
{
    serverUri = null!;

    if (!Uri.TryCreate(value.Trim(), UriKind.Absolute, out var parsedUri) ||
        parsedUri.Scheme != Uri.UriSchemeHttp &&
        parsedUri.Scheme != Uri.UriSchemeHttps ||
        !string.IsNullOrEmpty(parsedUri.Query) ||
        !string.IsNullOrEmpty(parsedUri.Fragment))
    {
        return false;
    }

    var normalizedAddress = parsedUri.AbsoluteUri.EndsWith('/')
        ? parsedUri.AbsoluteUri
        : parsedUri.AbsoluteUri + "/";

    serverUri = new Uri(normalizedAddress, UriKind.Absolute);
    return true;
}