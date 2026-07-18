using exchange_simulator.Models;

namespace exchange_simulator.Services;

public sealed class AccountTradingService
{
    private readonly TradingEngine _tradingEngine;
    private readonly Dictionary<Guid, TradingAccount> _accounts = [];
    
    public Instrument Instrument => _tradingEngine.Instrument;

    public AccountTradingService(TradingEngine tradingEngine)
    {
        ArgumentNullException.ThrowIfNull(tradingEngine);
        _tradingEngine = tradingEngine;
    }

    public TradingAccountSnapshot RegisterAccount(Guid accountId)
    {
        var account = new TradingAccount(accountId, Instrument);

        if (!_accounts.TryAdd(accountId, account))
            throw new InvalidOperationException($"Account {accountId} is already registered");

        return account.GetSnapshot();
    }
    
    public AccountOperation GrantInitialCash(Guid accountId, decimal amount) =>
        GetAccount(accountId).GrantInitialCash(amount);
    
    public AccountOperation GrantInitialInstrument(Guid accountId, long quantity) =>
        GetAccount(accountId).GrantInitialInstruments(quantity);

    public MarketBuyQuote GetMarketBuyQuote(long requestedSize)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(requestedSize);
        
        if (requestedSize % Instrument.LotSize != 0)
            throw new ArgumentException("Quantity must be a multiple of lot size");

        var remainingSize = requestedSize;
        var executableSize = 0L;
        var cost = 0m;

        foreach (var level in _tradingEngine.GetOrderBookSnapshot().Asks)
        {
            if (remainingSize == 0)
                break;
            
            var sizeAtLevel = Math.Min(remainingSize, level.Size);

            executableSize += sizeAtLevel;
            remainingSize -= sizeAtLevel;
            cost = checked(cost + level.Price * sizeAtLevel);
        }
        
        return new MarketBuyQuote(requestedSize, executableSize, remainingSize, cost);
    }
    
    public bool TryGetAccount(Guid accountId, out TradingAccountSnapshot? snapshot)
    {
        if (!_accounts.TryGetValue(accountId, out var account))
        {
            snapshot = null;
            return false;
        }
        
        snapshot = account.GetSnapshot();
        return true;
    }

    public IReadOnlyList<AccountOperation> GetAccountOperations(Guid accountId) =>
        GetAccount(accountId).GetOperations();

    private TradingAccount GetAccount(Guid accountId)
    {
        if (!_accounts.TryGetValue(accountId, out var account))
            throw new KeyNotFoundException($"Account {accountId} is not registered");
        
        return account;
    }
}