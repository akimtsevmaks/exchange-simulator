using exchange_simulator.Enums;

namespace exchange_simulator.Models;

public class OrderBook
{
    private sealed class PriceLevel(decimal price)
    {
        public decimal Price { get; } = price;
        public LinkedList<Order> Orders { get; } = [];
    }
    private sealed record OrderLocation(PriceLevel Level, LinkedListNode<Order> Node, OrderSide Side);
    
    public Instrument Instrument { get; }
    
    private readonly SortedDictionary<decimal, PriceLevel> _bids = 
        new(Comparer<decimal>.Create((a, b) => b.CompareTo(a)));

    private readonly SortedDictionary<decimal, PriceLevel> _asks = new();
    private readonly Dictionary<Guid, OrderLocation> _ordersById = new();

    public OrderBook(Instrument instrument)
    {
        ArgumentNullException.ThrowIfNull(instrument);
        
        Instrument = instrument;
    }
    
    private void AddOrder(Order order)
    {
        var sameSideLevels = order.Side ==  OrderSide.Buy ? _bids : _asks;

        if (!sameSideLevels.TryGetValue(order.Price!.Value, out var level))
        {
            level = new PriceLevel(order.Price!.Value);
            sameSideLevels.Add(order.Price!.Value, level);
        }
        
        var node = level.Orders.AddLast(order);
        
        _ordersById.Add(order.Id, new OrderLocation(level, node, order.Side));
    }

    public bool CancelOrder(Guid orderId)
    {
        if (!_ordersById.Remove(orderId, out var location))
            return false;

        location.Level.Orders.Remove(location.Node);

        if (location.Level.Orders.Count != 0)
            return true;
        
        var sameSideLevels = location.Side ==  OrderSide.Buy ? _bids : _asks;
        sameSideLevels.Remove(location.Level.Price);
        
        return true;
    }
}




















