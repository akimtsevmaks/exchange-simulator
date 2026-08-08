using exchange_simulator.Services;

namespace exchange_simulator.Server;

internal sealed class TestParticipant
{
    public Guid AccountId { get; }

    public TestParticipant(LocalMarket market)
    {
        ArgumentNullException.ThrowIfNull(market);

        AccountId = market.ManualAccountId;
    }
}