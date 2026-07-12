using exchange_simulator.Enums;

namespace exchange_simulator.Models;

public class OrderBook
{
    private sealed class PriceLevel(decimal price)
    {
        public decimal Price { get; } = price;
        public LinkedList<Order> Orders { get; } = [];
    }
    public Instrument Instrument { get; }
    
    private readonly SortedDictionary<decimal, PriceLevel> _bids = 
        new(Comparer<decimal>.Create((a, b) => b.CompareTo(a)));

    private readonly SortedDictionary<decimal, PriceLevel> _asks = new();

    public OrderBook(Instrument instrument)
    {
        ArgumentNullException.ThrowIfNull(instrument);
        
        Instrument = instrument;
    }
}




















