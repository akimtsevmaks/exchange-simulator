using exchange_simulator.Enums;

namespace exchange_simulator.Models.TradingCore;

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
    
    internal static TradingEngine Restore(
        Instrument instrument,
        IReadOnlyList<OrderSnapshot> orders,
        IReadOnlyList<Trade> trades)
    {
        ArgumentNullException.ThrowIfNull(instrument);
        ArgumentNullException.ThrowIfNull(orders);
        ArgumentNullException.ThrowIfNull(trades);

        var engine = new TradingEngine(instrument);

        foreach (var snapshot in orders)
        {
            if (snapshot is null)
                throw new ArgumentException("orders cannot contain null", nameof(orders));

            var order = Order.Restore(snapshot, instrument);

            if (!engine._orders.TryAdd(order.Id, order))
                throw new ArgumentException($"duplicate order {order.Id}", nameof(orders));

            if (order.Status == OrderStatus.Active)
                engine._orderBook.RestoreActiveOrder(order);
        }

        var tradeIds = new HashSet<Guid>();

        foreach (var trade in trades)
        {
            if (trade is null)
                throw new ArgumentException("trades cannot contain null", nameof(trades));
            if (trade.Id == Guid.Empty)
                throw new ArgumentException("trade ID cannot be empty", nameof(trades));
            if (!tradeIds.Add(trade.Id))
                throw new ArgumentException($"duplicate trade {trade.Id}", nameof(trades));
            if (trade.InstrumentId != instrument.Id)
                throw new ArgumentException("trade instrument does not match the restored instrument", nameof(trades));
            if (trade.BuyOrderId == Guid.Empty || trade.SellOrderId == Guid.Empty)
                throw new ArgumentException("trade order ID cannot be empty", nameof(trades));
            if (trade.BuyOrderId == trade.SellOrderId)
                throw new ArgumentException("trade must reference two different orders", nameof(trades));
            if (trade.Price <= 0)
                throw new ArgumentException("trade price must be positive", nameof(trades));
            if (trade.Size <= 0 || trade.Size % instrument.LotSize != 0)
                throw new ArgumentException("trade size is invalid", nameof(trades));
            if (!engine._orders.TryGetValue(trade.BuyOrderId, out var buyOrder) || buyOrder.Side != OrderSide.Buy)
                throw new ArgumentException("trade references an invalid buy order", nameof(trades));
            if (!engine._orders.TryGetValue(trade.SellOrderId, out var sellOrder) || sellOrder.Side != OrderSide.Sell)
                throw new ArgumentException("trade references an invalid sell order", nameof(trades));

            engine._trades.Add(trade);
        }

        return engine;
    }
    
    public OrderCommandResult PlaceOrder(PlaceOrderCommand command) =>
        PlaceOrder(command, static _ => { });

    internal OrderCommandResult PlaceOrder(PlaceOrderCommand command, 
        Action<IReadOnlyList<PlannedTrade>> validatePlannedTrades)
    { 
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(validatePlannedTrades);
        
        if (!ValidateOrderRequest(command, out var reason))
            return new OrderCommandResult(false, reason, null, []);
        
        var order = new Order(command.OwnerId, command.Type, command.Side, Instrument, command.Size, command.Price);
        
        var result = _orderBook.ProcessOrder(order, validatePlannedTrades);
        
        _orders.Add(order.Id, order);
        _trades.AddRange(result.Trades);
        
        return new OrderCommandResult(true, null, order.GetSnapshot(), result.Trades);
    }

    public OrderCommandResult CancelOrder(Guid orderId)
    {
        if (!_orders.TryGetValue(orderId, out var order)) 
            return new OrderCommandResult(false, OrderRejectionReason.OrderNotFound, null, []);
        
        if (order.Status != OrderStatus.Active)
            return new OrderCommandResult(false, OrderRejectionReason.OrderNotActive, null, []);

        if (!_orderBook.CancelOrder(orderId))
            throw new InvalidOperationException(
                $"Order {orderId} is Active in the registry but missing from the order book.");
        
        return new OrderCommandResult(true, null, order.GetSnapshot(), []);
    }

    public bool TryGetOrder(Guid orderId, out OrderSnapshot? snapshot)
    {
        if (!_orders.TryGetValue(orderId, out var order))
        {
            snapshot = null;
            return false;
        }
        
        snapshot = order.GetSnapshot();
        return true;
    }
    
    public IReadOnlyList<Trade> GetTrades() => 
        _trades.ToArray();
    
    public OrderBookSnapshot GetOrderBookSnapshot() =>
        _orderBook.GetSnapshot();
    
    public IReadOnlyList<OrderSnapshot> GetActiveOrders() =>
        _orders.Values.Where(order => order.Status == OrderStatus.Active)
            .Select(order => order.GetSnapshot()).ToArray();

    private decimal? GetLastTradePrice() =>
        _trades.Count == 0 ? null : _trades[^1].Price;
    
    public decimal GetReferencePrice() => 
        GetLastTradePrice() ?? Instrument.InitialPrice;

    internal bool ValidateOrderRequest(PlaceOrderCommand command, out OrderRejectionReason? reason)
    {
        if (command.OwnerId == Guid.Empty)
        {
            reason = OrderRejectionReason.InvalidOwnerId;
            return false;
        }

        if (!Enum.IsDefined(command.Side))
        {
            reason = OrderRejectionReason.InvalidOrderSide;
            return false;
        }
        
        if (!Enum.IsDefined(command.Type))
        {
            reason = OrderRejectionReason.InvalidOrderType;
            return false;
        }

        if (command.Size <= 0)
        {
            reason = OrderRejectionReason.InvalidSize;
            return false;
        }

        if (command.Size % Instrument.LotSize != 0)
        {
            reason = OrderRejectionReason.QuantityNotMultipleOfLotSize;
            return false;
        }

        if (command.Type == OrderType.Limit && command.Price == null)
        {
            reason = OrderRejectionReason.PriceRequiredForLimitOrder;
            return false;
        }

        if (command.Type == OrderType.Market && command.Price != null)
        {
            reason = OrderRejectionReason.PriceNotAllowedForMarketOrder;
            return false;
        }

        if (command.Price <= 0)
        {
            reason = OrderRejectionReason.InvalidPrice;
            return false;
        }
        
        reason = null;
        return true;
    }
}