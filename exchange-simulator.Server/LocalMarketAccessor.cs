using exchange_simulator.Services;

namespace exchange_simulator.Server;


internal sealed class LocalMarketAccessor
{
    private LocalMarket? _market;

    internal LocalMarket Market =>
        Volatile.Read(ref _market) ??
        throw new InvalidOperationException("The local market has not been initialized");

    internal void Publish(LocalMarket market)
    {
        ArgumentNullException.ThrowIfNull(market);

        if (Interlocked.CompareExchange(ref _market, market, null) is not null)
            throw new InvalidOperationException("The local market has already been initialized");
    }
}