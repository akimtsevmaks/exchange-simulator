namespace exchange_simulator.Bots;

public interface ITradingBot
{
    Guid AccountId { get; }

    void ExecuteStep();
    void Stop();
}