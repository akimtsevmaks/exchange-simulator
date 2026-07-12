using exchange_simulator.Enums;

namespace exchange_simulator.Models;

public class Order
{
    public Guid Id { get; } = Guid.NewGuid();
    public DateTimeOffset Time { get; } = DateTimeOffset.UtcNow;
    
    public OrderType Type { get; }
    public OrderSide Side { get; }
    
    public Guid OwnerId { get; }
    public Instrument Instrument { get; }
    
    
    public decimal? Price { get; }
    public decimal Size { get; }
    public decimal RemainingSize { get; private set; }

    public Order(Guid ownerId, OrderType type, OrderSide side, Instrument instrument,
        decimal size, decimal? price = null)
    {
        if (ownerId ==  Guid.Empty) throw new ArgumentException("invalid owner ID", nameof(ownerId));
        ArgumentNullException.ThrowIfNull(instrument);
        
        if (!Enum.IsDefined(type)) throw new ArgumentOutOfRangeException(nameof(type));
        if (!Enum.IsDefined(side)) throw new ArgumentOutOfRangeException(nameof(side));
        
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(size);

        if (type == OrderType.Limit)
        {
            if (!price.HasValue) throw new ArgumentNullException(nameof(price));
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(price.Value);
        }

        if (type == OrderType.Market && price.HasValue)
            throw new ArgumentException("invalid price in market", nameof(price));
        
        Type = type;
        Side = side;
        
        OwnerId = ownerId;
        Instrument = instrument;
        
        Size = size;
        RemainingSize = Size;
        Price = price;
    }

    public void Fill(decimal size)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(size);
        if (size > RemainingSize) throw new ArgumentException("invalid size ( >remaining )", nameof(size));
        
        RemainingSize -= size;
    }
}