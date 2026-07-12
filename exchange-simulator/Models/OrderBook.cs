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
    
    private readonly SortedDictionary<decimal, PriceLevel> _bids = 
        new(Comparer<decimal>.Create((a, b) => b.CompareTo(a)));
    private readonly SortedDictionary<decimal, PriceLevel> _asks = new();
    
    private readonly Dictionary<Guid, OrderLocation> _ordersById = new();
    
    public Instrument Instrument { get; }

    public OrderBook(Instrument instrument)
    {
        ArgumentNullException.ThrowIfNull(instrument);
        
        Instrument = instrument;
    }

    public OrderProcessingResult ProcessOrder(Order incomingOrder)
    {
        ArgumentNullException.ThrowIfNull(incomingOrder);

        if (incomingOrder.Instrument.Id != Instrument.Id)
            throw new ArgumentException($"{incomingOrder.Instrument.Id} does not match {Instrument.Id}", nameof(incomingOrder));
        
        if (_ordersById.ContainsKey(incomingOrder.Id))
            throw new ArgumentException($"{Instrument.Id} already exists", nameof(incomingOrder));

        var oppositeSideLevels = incomingOrder.Side == OrderSide.Buy ? _asks : _bids;

        var trades = new List<Trade>();

        while (incomingOrder.RemainingSize > 0 && oppositeSideLevels.Count > 0)
        {
            var bestLevel = oppositeSideLevels.First().Value;

            if (!CanMatch(incomingOrder, bestLevel.Price)) 
                break;

            var restingNode = bestLevel.Orders.First ??
                              throw new InvalidOperationException("Price level cannot be empty");
            var restingOrder = restingNode.Value;
            
            var tradeSize = Math.Min(restingOrder.RemainingSize, incomingOrder.RemainingSize);
            
            incomingOrder.Fill(tradeSize);
            restingOrder.Fill(tradeSize);
            
            trades.Add(CreateTrade(incomingOrder, restingOrder, bestLevel.Price, tradeSize));
            
            if (restingOrder.RemainingSize == 0) 
                RemoveFilledOrder(oppositeSideLevels, bestLevel, restingNode);
        }

        var isResting = incomingOrder.Type == OrderType.Limit && incomingOrder.RemainingSize > 0;
        
        if (isResting)
            AddOrder(incomingOrder);
        
        return new OrderProcessingResult(trades, incomingOrder.RemainingSize, isResting);

    }

    private static bool CanMatch(Order order, decimal restingPrice)
    {
        if (order.Type == OrderType.Market)
            return true;
        
        var limitPrice = order.Price!.Value;
        return order.Side == OrderSide.Buy ? limitPrice >= restingPrice : limitPrice <= restingPrice;
    }

    private Trade CreateTrade(Order incomingOrder, Order restingOrder, decimal price, decimal size)
    {
        var buyOrderId = incomingOrder.Side == OrderSide.Buy ? incomingOrder.Id : restingOrder.Id;
        var sellOrderId = incomingOrder.Side == OrderSide.Sell ? incomingOrder.Id : restingOrder.Id;
        
        return new Trade(Guid.NewGuid(), Instrument.Id, buyOrderId, sellOrderId, price, size, DateTimeOffset.UtcNow);
    }

    private void RemoveFilledOrder(SortedDictionary<decimal, PriceLevel> levels,
        PriceLevel level, LinkedListNode<Order> node)
    {
        var order = node.Value;
        
        level.Orders.Remove(node);
        _ordersById.Remove(order.Id);

        if (level.Orders.Count == 0)
            levels.Remove(level.Price);
    }
    
    private void AddOrder(Order order)
    {
        if (order.Type != OrderType.Limit || !order.Price.HasValue || order.RemainingSize <= 0)
            throw new InvalidOperationException("Only an unfilled limit order can be processed.");
        
        if (_ordersById.ContainsKey(order.Id))
            throw new InvalidOperationException($"Order {order.Id} already exists");
        
        var sameSideLevels = order.Side ==  OrderSide.Buy ? _bids : _asks;
        
        var price = order.Price.Value;
        if (!sameSideLevels.TryGetValue(price, out var level))
        {
            level = new PriceLevel(price);
            sameSideLevels.Add(price, level);
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




















