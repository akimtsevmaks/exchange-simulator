namespace exchange_simulator.Models;

public class TradingAccount
{
    public Guid Id { get; }
    
    public decimal CashBalance { get; private set; }
    public decimal ReservedCash { get; private set; }
    public decimal AvailableCash =>  CashBalance - ReservedCash;
    
    public Position Position { get; }

    public TradingAccount(Guid id, Instrument instrument)
    {
        if (id == Guid.Empty)
            throw new ArgumentException("invalid account ID", nameof(id));
        ArgumentNullException.ThrowIfNull(instrument);
        
        Id = id;
        Position = new Position(instrument.Id);
    }
    
    public TradingAccountSnapshot GetSnapshot() =>
        new(Id, CashBalance,  ReservedCash, AvailableCash, Position.GetSnapshot());
}