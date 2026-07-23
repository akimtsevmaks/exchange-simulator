using exchange_simulator.Models.AccountTrading;
using exchange_simulator.Models.TradingCore;

namespace exchange_simulator.Services;

public sealed class LocalMarket
{
    private readonly object _syncRoot = new();
    private readonly AccountTradingService _accountTradingService;
    
    public Instrument Instrument => _accountTradingService.Instrument;
    
    public Guid MarketMakerAccountId { get; }
    public Guid NoiseBotAccountId { get; }
    public Guid ManualAccountId { get; }

    public LocalMarket(Instrument instrument, decimal initialCashPerAccount, long initialInstrumentPeerAccount)
    {
        ArgumentNullException.ThrowIfNull(instrument);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(initialCashPerAccount);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(initialInstrumentPeerAccount);
        
        _accountTradingService = new AccountTradingService(instrument);

        MarketMakerAccountId = RegisterParticipant(initialCashPerAccount, initialInstrumentPeerAccount);
        NoiseBotAccountId = RegisterParticipant(initialCashPerAccount, initialInstrumentPeerAccount);
        ManualAccountId = RegisterParticipant(initialCashPerAccount, initialInstrumentPeerAccount);
    }
    
    public TradingAccountSnapshot RegisterAccount(Guid accountId) =>
        Execute(() => _accountTradingService.RegisterAccount(accountId));
    
    public AccountOperation GrantInitialCash(Guid accountId, decimal amount) =>
        Execute(() => _accountTradingService.GrantInitialCash(accountId, amount));

    public AccountOperation GrantInitialInstruments(Guid accountId, long quantity) =>
        Execute(() => _accountTradingService.GrantInitialInstruments(accountId, quantity));
    
    public OrderCommandResult PlaceOrder(PlaceOrderCommand command) =>
        Execute(() => _accountTradingService.PlaceOrder(command));

    public OrderCommandResult CancelOrder(Guid orderId) =>
        Execute(() => _accountTradingService.CancelOrder(orderId));
    
    public OrderBookSnapshot GetOrderBookSnapshot() =>
        Execute(() => _accountTradingService.GetOrderBookSnapshot());

    public decimal GetReferencePrice() =>
        Execute(() => _accountTradingService.GetReferencePrice());

    public IReadOnlyList<Trade> GetTrades() =>
        Execute(() => _accountTradingService.GetTrades());
    
    public MarketBuyQuote GetMarketBuyQuote(long requestedSize) =>
        Execute(() => _accountTradingService.GetMarketBuyQuote(requestedSize));

    public bool TryGetOrder(Guid accountId, Guid orderId, out OrderSnapshot? snapshot)
    {
        lock (_syncRoot)
        {
            return _accountTradingService.TryGetOrder(accountId, orderId, out snapshot);
        }
    }
    
    public bool TryGetAccount(Guid accountId, out TradingAccountSnapshot? snapshot)
    {
        lock (_syncRoot)
        {
            return _accountTradingService.TryGetAccount(accountId, out snapshot);
        }
    }
    
    public IReadOnlyList<AccountOperation> GetAccountOperations(Guid accountId) =>
        Execute(() => _accountTradingService.GetAccountOperations(accountId));

    public IReadOnlyList<OrderSnapshot> GetActiveOrders(Guid accountId) =>
        Execute(() => _accountTradingService.GetActiveOrders(accountId));

    public IReadOnlyList<OrderHistoryEntry> GetAccountOrderHistory(Guid accountId) =>
        Execute(() => _accountTradingService.GetAccountOrderHistory(accountId));

    public IReadOnlyList<Trade> GetAccountTrades(Guid accountId) =>
        Execute(() => _accountTradingService.GetAccountTrades(accountId));

    public IReadOnlyList<OrderHistoryEntry> GetOrderHistory(Guid orderId) =>
        Execute(() => _accountTradingService.GetOrderHistory(orderId));


    private Guid RegisterParticipant(decimal initialCash, long initialInstrument)
    {
        var accountId = Guid.NewGuid();
        
        _accountTradingService.RegisterAccount(accountId);
        _accountTradingService.GrantInitialCash(accountId, initialCash);
        _accountTradingService.GrantInitialInstruments(accountId, initialInstrument);
        
        return accountId;
    }

    private T Execute<T>(Func<T> func)
    {
        lock (_syncRoot)
        {
            return func();
        }
    }
}