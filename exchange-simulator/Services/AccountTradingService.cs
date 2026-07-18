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