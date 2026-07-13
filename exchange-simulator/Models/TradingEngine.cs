using exchange_simulator.Enums;

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

    public OrderCommandResult PlaceOrder(PlaceOrderCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        
        if (!ValidateOrderRequest(command, out var reason))
            return new OrderCommandResult(false, reason, null, []);
        
        var order = new Order(command.OwnerId, command.Type, command.Side, Instrument, command.Size, command.Price);
        
        var result = _orderBook.ProcessOrder(order);
        
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

    private bool ValidateOrderRequest(PlaceOrderCommand command, out OrderRejectionReason? reason)
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