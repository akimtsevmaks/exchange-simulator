namespace exchange_simulator.Services;

public sealed class MarketFaultedException : InvalidOperationException
{
    public MarketFaultedException() 
        : base("The local market is faulted and can be recovered only by restarting the server") { }
}