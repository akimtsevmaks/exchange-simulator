namespace exchange_simulator.Models.AccountTrading;

public sealed class Position
{
    public Guid InstrumentId { get; }
    
    public long Quantity { get; private set; }
    public long ReservedQuantity { get; private set; }
    public long AvailableQuantity => Quantity - ReservedQuantity;
    
    public decimal AveragePrice { get; private set; }
    
    internal Position(Guid instrumentId) : this(instrumentId, 0, 0, 0) { }

    private Position(Guid instrumentId, long quantity, long reservedQuantity, decimal averagePrice)
    {
        if (instrumentId == Guid.Empty)
            throw new ArgumentException("invalid instrument ID", nameof(instrumentId));
        
        ArgumentOutOfRangeException.ThrowIfNegative(quantity);
        ArgumentOutOfRangeException.ThrowIfNegative(reservedQuantity);
        
        ArgumentOutOfRangeException.ThrowIfGreaterThan(reservedQuantity, quantity);
        
        if ((quantity == 0 && averagePrice != 0) || (quantity > 0 && averagePrice <= 0))
            throw new ArgumentOutOfRangeException(nameof(averagePrice));
        
        InstrumentId = instrumentId;
        Quantity = quantity;
        ReservedQuantity = reservedQuantity;
        AveragePrice = averagePrice;
    }

    internal static Position Restore(PositionSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var position = new Position(
            snapshot.InstrumentId,
            snapshot.Quantity,
            snapshot.ReservedQuantity,
            snapshot.AveragePrice);
        
        if (snapshot.AvailableQuantity != position.AvailableQuantity)
            throw new ArgumentException("available quantity doesn't match quantity", nameof(snapshot));

        return position;
    }

    internal void GrantInitialQuantity(long quantity, decimal price)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(quantity);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(price);

        AddQuantity(quantity, checked(price * quantity));
    }

    internal bool TryReserve(long quantity)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(quantity);

        if (quantity > AvailableQuantity)
            return false;
        
        ReservedQuantity += quantity;
        return true;
    }

    internal void Release(long quantity)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(quantity);
        
        if (quantity > ReservedQuantity)
            throw new InvalidOperationException("Can't release more than reserved quantity");
        
        ReservedQuantity -= quantity;
    }

    internal void Buy(long quantity, decimal cost)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(quantity);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(cost);

        AddQuantity(quantity, cost);
    }

    internal void Sell(long quantity)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(quantity);
        
        if (quantity > ReservedQuantity)
            throw new InvalidOperationException("Can't sell more than reserved quantity");
        
        Quantity -= quantity;
        ReservedQuantity -= quantity;
        
        if (Quantity == 0)
            AveragePrice = 0;
    }

    private void AddQuantity(long quantity, decimal cost)
    {
        var newQuantity = checked(Quantity + quantity);
        var currentCost = checked(AveragePrice * Quantity);
        var newCost = checked(currentCost + cost);
        
        Quantity = newQuantity;
        AveragePrice = newCost / newQuantity;
    }
    
    public PositionSnapshot GetSnapshot() =>
        new(InstrumentId, Quantity, ReservedQuantity, AvailableQuantity, AveragePrice);
}