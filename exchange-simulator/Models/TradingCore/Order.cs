using exchange_simulator.Enums;
using exchange_simulator.Models;

namespace exchange_simulator.Models.TradingCore;

public class Order
{
    public Guid Id { get; }
    public DateTimeOffset Time { get; }
    
    public OrderType Type { get; }
    public OrderSide Side { get; }
    public OrderStatus Status { get; private set; }
    
    public Guid OwnerId { get; }
    public Instrument Instrument { get; }
    
    public decimal? Price { get; }
    public long Size { get; }
    public long RemainingSize { get; private set; }
    public long FilledSize => Size - RemainingSize;
    
    public Order(Guid ownerId, OrderType type, OrderSide side, Instrument instrument, long size, decimal? price = null)
        : this(Guid.NewGuid(), UtcTimestamp.Now(), ownerId, type, side, instrument, size, size, OrderStatus.Created, price) { }

    private Order(
        Guid id,
        DateTimeOffset time,
        Guid ownerId,
        OrderType type,
        OrderSide side,
        Instrument instrument,
        long size,
        long remainingSize,
        OrderStatus status,
        decimal? price)
    {
        if (id == Guid.Empty)
            throw new ArgumentException("invalid order id", nameof(id));
        if (ownerId ==  Guid.Empty)
            throw new ArgumentException("invalid owner ID", nameof(ownerId));
        ArgumentNullException.ThrowIfNull(instrument);
        
        if (!Enum.IsDefined(type)) throw new ArgumentOutOfRangeException(nameof(type));
        if (!Enum.IsDefined(side)) throw new ArgumentOutOfRangeException(nameof(side));
        if (!Enum.IsDefined(status)) throw new ArgumentOutOfRangeException(nameof(status));
        
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(size);
        ArgumentOutOfRangeException.ThrowIfNegative(remainingSize);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(remainingSize, size);

        if (type == OrderType.Limit)
        {
            if (!price.HasValue) throw new ArgumentNullException(nameof(price));
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(price.Value);
        }

        if (type == OrderType.Market && price.HasValue)
            throw new ArgumentException("invalid price in market", nameof(price));
        
        if (size % instrument.LotSize != 0)
            throw new ArgumentException("quantity must be divisible by lot", nameof(size));
        if (remainingSize % instrument.LotSize != 0)
            throw new ArgumentException("remaining size must be divisible by lot", nameof(remainingSize));
        
        if (status == OrderStatus.Filled && remainingSize != 0)
            throw new ArgumentException("filled order must not have a remaining size", nameof(remainingSize));
        if (status != OrderStatus.Filled && remainingSize == 0)
            throw new ArgumentException("unfilled order must have a remaining size", nameof(remainingSize));
        if (status == OrderStatus.Active && type != OrderType.Limit)
            throw new ArgumentException("only a limit order can be active", nameof(status));
        
        Id = id;
        Time = time;
        
        Type = type;
        Side = side;
        Status = status;
        
        OwnerId = ownerId;
        Instrument = instrument;
        
        Size = size;
        RemainingSize = remainingSize;
        Price = price;
    }

    internal static Order Restore(OrderSnapshot snapshot, Instrument instrument)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(instrument);

        if (snapshot.InstrumentId != instrument.Id)
            throw new ArgumentException("order instrument does not match the restored instrument", nameof(snapshot));
        if (snapshot.OrderStatus == OrderStatus.Created)
            throw new ArgumentException("a created order is not a persisted order", nameof(snapshot));
        
        var order = new Order(
            snapshot.Id,
            snapshot.CreatedAt,
            snapshot.OwnerId,
            snapshot.OrderType,
            snapshot.OrderSide,
            instrument,
            snapshot.Size,
            snapshot.RemainingSize,
            snapshot.OrderStatus,
            snapshot.Price);
        
        if (snapshot.FilledSize != order.FilledSize)
            throw new ArgumentException("filled size does not match order size and remainder", nameof(snapshot));

        return order;
    }

    internal void Fill(long size)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(size);
        if (Status != OrderStatus.Created && Status != OrderStatus.Active)
            throw new InvalidOperationException($"Order in status {Status} cannot be filled.");
        if (size > RemainingSize) throw new ArgumentException("invalid size ( >remaining )", nameof(size));
        
        RemainingSize -= size;

        if (RemainingSize == 0)
            Status = OrderStatus.Filled;

    }

    internal void Activate()
    {
        if (Status != OrderStatus.Created)
            throw new InvalidOperationException("invalid order status");
        
        Status = OrderStatus.Active;
    }

    internal void Cancel()
    {
        if (Status != OrderStatus.Created && Status != OrderStatus.Active)
            throw new InvalidOperationException("invalid order status");
        
        Status = OrderStatus.Cancelled;
    }
    
    public OrderSnapshot GetSnapshot()
    {
        return new OrderSnapshot(Id, OwnerId, Instrument.Id, 
            Type, Side, Status, Price, Size, RemainingSize, FilledSize, Time);
    }
}