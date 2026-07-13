namespace exchange_simulator.Models;

public class TradingEngine
{
    public Instrument Instrument { get; }
    private readonly OrderBook _orderBook;
    private readonly Dictionary<Guid, Order> _orders = [];
    private readonly List<Trade> _trades = [];

    public TradingEngine(Instrument instrument)
    {
        ArgumentNullException.ThrowIfNull(instrument);
        
        Instrument = instrument;
        _orderBook = new OrderBook(Instrument);
    }
}