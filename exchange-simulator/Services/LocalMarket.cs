using exchange_simulator.Bots;
using exchange_simulator.Enums;
using exchange_simulator.Models.AccountTrading;
using exchange_simulator.Models.TradingCore;

namespace exchange_simulator.Services;

public sealed class LocalMarket
{
    private readonly Lock _syncRoot = new();
    private readonly AccountTradingService _accountTradingService;
    private readonly IReadOnlyList<ITradingBot> _bots;
    private readonly Dictionary<Guid, BotExecutionFailure> _botFailures = [];
    private readonly HashSet<Guid> _stoppedBotAccountIds = [];
    private CancellationTokenSource? _runCancellation;
    private Task? _runTask;
    private LocalMarketStatus _status;
    
    public Instrument Instrument => _accountTradingService.Instrument;
    
    public Guid MarketMakerAccountId { get; }
    public Guid NoiseBotAccountId { get; }
    public Guid ManualAccountId { get; }
    private TimeSpan StepInterval { get; }
    public LocalMarketStatus Status => Execute(() => _status);

    public LocalMarket(
        Instrument instrument,
        decimal initialCashPerAccount,
        long initialInstrumentsPerAccount,
        TimeSpan stepInterval,
        MarketMakerBotOptions marketMakerOptions,
        NoiseBotOptions noiseBotOptions)
    {
        ArgumentNullException.ThrowIfNull(instrument);
        ArgumentNullException.ThrowIfNull(marketMakerOptions);
        ArgumentNullException.ThrowIfNull(noiseBotOptions);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(initialCashPerAccount);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(initialInstrumentsPerAccount);
        
        if (stepInterval <= TimeSpan.Zero || 
            stepInterval.TotalMilliseconds > uint.MaxValue - 1)
            throw new ArgumentOutOfRangeException(nameof(stepInterval),
                "Step interval is outside the supported range");
        
        _accountTradingService = new AccountTradingService(instrument);

        MarketMakerAccountId = RegisterParticipant(initialCashPerAccount, initialInstrumentsPerAccount);
        NoiseBotAccountId = RegisterParticipant(initialCashPerAccount, initialInstrumentsPerAccount);
        ManualAccountId = RegisterParticipant(initialCashPerAccount, initialInstrumentsPerAccount);
        
        StepInterval = stepInterval;
        _bots =
        [
            new MarketMakerBot(
                this,
                marketMakerOptions.QuoteOffset,
                marketMakerOptions.OrderSize),
            new NoiseBot(
                this,
                noiseBotOptions.RandomSeed,
                noiseBotOptions.PriceOffset,
                noiseBotOptions.MaxOrderLots,
                noiseBotOptions.MaxActiveOrders)
        ];
    }

    public void Step()
    {
        lock (_syncRoot)
        {
            if (_status == LocalMarketStatus.Stopped)
                throw new InvalidOperationException(
                    "A stopped local market cannot execute new steps");

            foreach (var bot in _bots)
            {
                if (!_stoppedBotAccountIds.Contains(bot.AccountId))
                    ExecuteBotStep(bot);
            }
        }
    }
    
    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        lock (_syncRoot)
        {
            if (_status != LocalMarketStatus.Created)
            {
                throw new InvalidOperationException(
                    "A local market can be started only once");
            }

            _runCancellation =
                CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _status = LocalMarketStatus.Running;
            _runTask = RunLoopAsync(_runCancellation.Token);
        }

        return Task.CompletedTask;
    }
    
    public async Task StopAsync()
    {
        CancellationTokenSource? runCancellation;
        Task? runTask;

        lock (_syncRoot)
        {
            if (_status == LocalMarketStatus.Created)
            {
                StopBots();
                _status = LocalMarketStatus.Stopped;
                return;
            }

            runCancellation = _runCancellation;
            runTask = _runTask;
        }

        runCancellation?.Cancel();

        if (runTask is not null)
            await runTask;
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
    
    public IReadOnlyList<BotExecutionFailure> GetBotFailures() =>
        Execute(() => _bots
            .Where(bot => _botFailures.ContainsKey(bot.AccountId))
            .Select(bot => _botFailures[bot.AccountId])
            .ToArray());
    
    public LocalMarketSnapshot GetSnapshot()
    {
        lock (_syncRoot)
        {
            if (!_accountTradingService.TryGetAccount(ManualAccountId, out var manualAccount))
            {
                throw new InvalidOperationException(
                    "The manual participant account is missing");
            }

            return new LocalMarketSnapshot(
                _accountTradingService.GetOrderBookSnapshot(),
                _accountTradingService.GetReferencePrice(),
                _accountTradingService.GetTrades(),
                manualAccount!,
                _accountTradingService.GetActiveOrders(ManualAccountId));
        }
    }
    
    public bool TryStopBot(Guid botAccountId)
    {
        lock (_syncRoot)
        {
            var bot = _bots.FirstOrDefault(bot => bot.AccountId == botAccountId);

            return bot is not null && StopBot(bot);
        }
    }
    
    private async Task RunLoopAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var timer = new PeriodicTimer(StepInterval);

            while (await timer.WaitForNextTickAsync(cancellationToken))
                Step();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        finally
        {
            lock (_syncRoot)
            {
                StopBots();
                _status = LocalMarketStatus.Stopped;
            }
        }
    }
    
    private void ExecuteBotStep(ITradingBot bot)
    {
        try
        {
            bot.ExecuteStep();
            ClearBotFailure(bot, "Step");
        }
        catch (Exception exception)
        {
            RecordBotFailure(bot, "Step", exception);
        }
    }

    private void StopBots()
    {
        foreach (var bot in _bots)
            StopBot(bot);
    }
    
    private bool StopBot(ITradingBot bot)
    {
        try
        {
            bot.Stop();
            _stoppedBotAccountIds.Add(bot.AccountId);
            ClearBotFailure(bot, "Stop");
            return true;
        }
        catch (Exception exception)
        {
            RecordBotFailure(bot, "Stop", exception);
            return false;
        }
    }
    
    private void ClearBotFailure(ITradingBot bot, string operation)
    {
        if (_botFailures.TryGetValue(bot.AccountId, out var failure) &&
            failure.Operation == operation)
        {
            _botFailures.Remove(bot.AccountId);
        }
    }

    private void RecordBotFailure(
        ITradingBot bot,
        string operation,
        Exception exception)
    {
        _botFailures[bot.AccountId] = new BotExecutionFailure(
            bot.AccountId,
            operation,
            exception.GetType().Name,
            exception.Message,
            DateTimeOffset.UtcNow);
    }

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