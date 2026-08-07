using exchange_simulator.Services;

namespace exchange_simulator.Server;

internal sealed class LocalMarketHostedService(LocalMarket market): IHostedService
{
    private readonly LocalMarket _market = market;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        
        return _market.StartAsync(cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return _market.StopAsync().WaitAsync(cancellationToken);
    }

}