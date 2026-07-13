using exchange_simulator.Models;

namespace exchange_simulator;

class Program
{
    static void Main(string[] args)
    {
        var instrument = new Instrument( Guid.NewGuid(), "TEST", "Test Instrument", lotSize: 1, initPrice: 100m);
    }
}