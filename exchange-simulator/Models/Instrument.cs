namespace exchange_simulator.Models;

public sealed class Instrument
{
    public Guid Id { get; }
    public string Ticker { get; }
    public string Name { get; } 

    
    public long LotSize { get; }
    public decimal InitPrice { get; }

    public Instrument(Guid id, string ticker, string name, long lotSize, decimal initPrice)
    {
        if (id == Guid.Empty) throw new ArgumentException("invalid id", nameof(id));
        ArgumentException.ThrowIfNullOrEmpty(ticker);
        ArgumentException.ThrowIfNullOrEmpty(name);
        
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(lotSize);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(initPrice);
        
        Id = id;
        Ticker = ticker;
        Name = name;
        
        LotSize = lotSize;
        InitPrice = initPrice;
    }
}