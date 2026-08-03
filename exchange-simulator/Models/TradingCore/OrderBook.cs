using exchange_simulator.Enums;

namespace exchange_simulator.Models.TradingCore;

public class OrderBook
{
    private sealed class PriceLevel(decimal price)
    {
        public decimal Price { get; } = price;
        public LinkedList<Order> Orders { get; } = [];
    }
    private sealed record OrderLocation(PriceLevel Level, LinkedListNode<Order> Node, OrderSide Side);
    private sealed record PlannedMatch(PriceLevel Level, LinkedListNode<Order> RestingNode, long Size);
    
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

    public OrderBookSnapshot GetSnapshot()
    {
        var bids = BuildLevels(_bids);
        var asks = BuildLevels(_asks);
        
        return new OrderBookSnapshot(Instrument.Id, bids, asks);
    }

    private static IReadOnlyList<OrderBookLevel> BuildLevels(SortedDictionary<decimal, PriceLevel> levels)
    {
        var result = new List<OrderBookLevel>();

        foreach (var level in levels.Values)
        {
            var totalSize = level.Orders.Sum(order => order.RemainingSize);
            result.Add(new OrderBookLevel(level.Price, totalSize));
        }
        
        return result.AsReadOnly();
    }
    
    public OrderProcessingResult ProcessOrder(Order incomingOrder) =>
        ProcessOrder(incomingOrder, static _ => { });

    internal OrderProcessingResult ProcessOrder(Order incomingOrder,
        Action<IReadOnlyList<PlannedTrade>> validatePlannedTrades)
    {
        ArgumentNullException.ThrowIfNull(incomingOrder);
        ArgumentNullException.ThrowIfNull(validatePlannedTrades);

        if (incomingOrder.Instrument.Id != Instrument.Id)
            throw new ArgumentException(
                $"Instrument {incomingOrder.Instrument.Id} does not match {Instrument.Id}", nameof(incomingOrder));

        if (incomingOrder.Status != OrderStatus.Created)
            throw new ArgumentException($"{incomingOrder.Id} hasn't Created status", nameof(incomingOrder));
        
        if (_ordersById.ContainsKey(incomingOrder.Id))
            throw new ArgumentException($"{incomingOrder.Id} already exists", nameof(incomingOrder));

        var oppositeSideLevels = incomingOrder.Side == OrderSide.Buy ? _asks : _bids;
        var matchPlan = BuildMatchPlan(incomingOrder, oppositeSideLevels);
        var plannedTrades = matchPlan.Select(match => CreatePlannedTrade(incomingOrder, match)).ToList();

        validatePlannedTrades(plannedTrades);
        
        var trades = new List<Trade>();

        foreach (var match in matchPlan)
        {
            var restingOrder = match.RestingNode.Value;
            
            incomingOrder.Fill(match.Size);
            restingOrder.Fill(match.Size);
            
            trades.Add(CreateTrade(incomingOrder, restingOrder, match.Level.Price, match.Size));

            if (restingOrder.RemainingSize == 0)
                RemoveFilledOrder(oppositeSideLevels, match.Level, match.RestingNode);
        }

        var isResting = incomingOrder.Type == OrderType.Limit && incomingOrder.RemainingSize > 0;
        
        if (isResting)
            AddOrder(incomingOrder);
        
        if (incomingOrder.Type == OrderType.Market && incomingOrder.RemainingSize > 0)
            incomingOrder.Cancel();
        
        return new OrderProcessingResult(trades, incomingOrder.RemainingSize, isResting);
    }
    
    private static IReadOnlyList<PlannedMatch> BuildMatchPlan(Order incomingOrder,
        SortedDictionary<decimal, PriceLevel> oppositeSideLevels)
    {
        var matches = new List<PlannedMatch>();
        var remainingSize = incomingOrder.RemainingSize;

        foreach (var level in oppositeSideLevels.Values)
        {
            if (remainingSize == 0 || !CanMatch(incomingOrder, level.Price))
                break;

            var node = level.Orders.First ?? throw new InvalidOperationException("Price level cannot be empty");

            while (node is not null && remainingSize > 0)
            {
                var matchSize = Math.Min(node.Value.RemainingSize, remainingSize);
                matches.Add(new PlannedMatch(level, node, matchSize));
                remainingSize -= matchSize;
                node = node.Next;
            }
        }

        return matches;
    }
    
    private static PlannedTrade CreatePlannedTrade(Order incomingOrder, PlannedMatch match)
    {
        var restingOrder = match.RestingNode.Value;
        var buyerAccountId = incomingOrder.Side == OrderSide.Buy
            ? incomingOrder.OwnerId : restingOrder.OwnerId;
        var sellerAccountId = incomingOrder.Side == OrderSide.Sell
            ? incomingOrder.OwnerId : restingOrder.OwnerId;

        return new PlannedTrade(
            buyerAccountId,
            sellerAccountId,
            match.Level.Price,
            match.Size);
    }

    private static bool CanMatch(Order order, decimal restingPrice)
    {
        if (order.Type == OrderType.Market)
            return true;
        
        var limitPrice = order.Price!.Value;
        return order.Side == OrderSide.Buy ? limitPrice >= restingPrice : limitPrice <= restingPrice;
    }

    private Trade CreateTrade(Order incomingOrder, Order restingOrder, decimal price, long size)
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
        order.Activate();
    }

    public bool CancelOrder(Guid orderId)
    {
        if (!_ordersById.Remove(orderId, out var location))
            return false;

        location.Level.Orders.Remove(location.Node);
        location.Node.Value.Cancel();

        if (location.Level.Orders.Count != 0)
            return true;
        
        var sameSideLevels = location.Side ==  OrderSide.Buy ? _bids : _asks;
        sameSideLevels.Remove(location.Level.Price);
        
        return true;
    }
}




















