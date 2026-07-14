namespace exchange_simulator.Models;

public sealed class Instrument
{
    public Guid Id { get; }
    public string Ticker { get; }
    public string Name { get; } 

    
    public long LotSize { get; }
    public decimal InitialPrice { get; }

    public Instrument(Guid id, string ticker, string name, long lotSize, decimal initialPrice)
    {
        if (id == Guid.Empty) throw new ArgumentException("invalid id", nameof(id));
        ArgumentException.ThrowIfNullOrEmpty(ticker);
        ArgumentException.ThrowIfNullOrEmpty(name);
        
        if (ticker.Length != 4)
            throw new ArgumentException("Ticker must contain exactly 4 characters.", nameof(ticker));
        if (name.Length > 99)
            throw new ArgumentException("Name cannot exceed 99 characters.", nameof(name));
        
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(lotSize);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(initialPrice);
        
        Id = id;
        Ticker = ticker;
        Name = name;
        
        LotSize = lotSize;
        InitialPrice = initialPrice;
    }
}