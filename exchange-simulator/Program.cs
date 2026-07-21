using exchange_simulator.Models;
using exchange_simulator.Models.TradingCore;

namespace exchange_simulator;

class Program
{
    static void Main(string[] args)
    {
        var instrument = new Instrument( Guid.NewGuid(), "TEST", "Test Instrument", lotSize: 1, initialPrice: 100m);
    }
}