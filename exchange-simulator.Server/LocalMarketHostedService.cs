using System.Data;
using exchange_simulator.Server.Persistence;
using exchange_simulator.Services;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using NpgsqlTypes;

namespace exchange_simulator.Server;


internal sealed class LocalMarketHostedService(
    LocalMarketFactory marketFactory,
    LocalMarketAccessor marketAccessor,
    IConfiguration configuration,
    ILogger<LocalMarketHostedService> logger,
    ILogger<TradingWorldStore> tradingWorldStoreLogger) : IHostedService
{
    private const int AdvisoryLockNamespace = 0x45584348;
    private const int AdvisoryLockResource = 1;
    private const int LockCommandTimeoutSeconds = 5;
    private static readonly TimeSpan HeartbeatInterval = TimeSpan.FromSeconds(5);

    private readonly LocalMarketFactory _marketFactory = marketFactory;
    private readonly LocalMarketAccessor _marketAccessor = marketAccessor;
    private readonly IConfiguration _configuration = configuration;
    private readonly ILogger<LocalMarketHostedService> _logger = logger;
    private readonly ILogger<TradingWorldStore> _tradingWorldStoreLogger = tradingWorldStoreLogger;
    private readonly Lock _lifecycleSync = new();
    private readonly SemaphoreSlim _lockCommandSync = new(1, 1);

    private NpgsqlConnection? _lockConnection;
    private LocalMarket? _market;
    private CancellationTokenSource? _monitorCancellation;
    private Task? _monitorTask;
    private bool _isStopping;
    private bool _lockLost;
    private bool _startupCompleted;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var connectionString =
            _configuration.GetConnectionString("ExchangeDatabase") ??
            throw new InvalidOperationException(
                "Connection string 'ExchangeDatabase' is not configured");
        var lockConnectionString = new NpgsqlConnectionStringBuilder(connectionString)
        {
            Pooling = false,
            ApplicationName = "exchange-simulator-single-instance-lock"
        }.ConnectionString;
        var connection = new NpgsqlConnection(lockConnectionString);
        CancellationTokenSource? startupCancellation = null;

        try
        {
            await connection.OpenAsync(cancellationToken);

            if (!await TryAcquireLockAsync(connection, cancellationToken))
            {
                throw new InvalidOperationException(
                    "Another exchange server already owns the PostgreSQL " +
                    "single-instance lock for this database");
            }

            _lockConnection = connection;
            startupCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            var dbContextOptions =
                new DbContextOptionsBuilder<ExchangeDbContext>()
                    .UseNpgsql(connection, contextOwnsConnection: false)
                    .Options;
            LocalMarket market;

            await using (var dbContext = new ExchangeDbContext(dbContextOptions))
            {
                var store = new TradingWorldStore(
                    dbContext,
                    _marketFactory,
                    _tradingWorldStoreLogger);
                market = await store.LoadOrCreateAsync(
                    startupCancellation.Token);
            }

            _monitorCancellation = new CancellationTokenSource();
            _monitorTask = MonitorLockConnectionAsync(
                connection,
                startupCancellation,
                _monitorCancellation.Token);
            await CheckLockConnectionAsync(
                connection,
                startupCancellation.Token);
            startupCancellation.Token.ThrowIfCancellationRequested();
            _market = market;
            await market.StartAsync(startupCancellation.Token);
            startupCancellation.Token.ThrowIfCancellationRequested();

            lock (_lifecycleSync)
            {
                startupCancellation.Token.ThrowIfCancellationRequested();

                if (_lockLost)
                {
                    throw new InvalidOperationException(
                        "The PostgreSQL single-instance lock was lost during startup");
                }

                _marketAccessor.Publish(market);
                _startupCompleted = true;
            }

            startupCancellation.Dispose();
            startupCancellation = null;

            _logger.LogInformation(
                "Acquired the PostgreSQL single-instance lock and started the local market");
        }
        catch
        {
            lock (_lifecycleSync)
            {
                _isStopping = true;
            }

            _monitorCancellation?.Cancel();

            try
            {
                if (_monitorTask is not null)
                    await _monitorTask;
            }
            finally
            {
                try
                {
                    if (_market is not null)
                        await _market.StopAsync();
                }
                finally
                {
                    startupCancellation?.Dispose();
                    _monitorCancellation?.Dispose();
                    await connection.DisposeAsync();
                    _lockConnection = null;
                }
            }

            throw;
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        lock (_lifecycleSync)
        {
            _isStopping = true;
        }

        _monitorCancellation?.Cancel();

        try
        {
            if (_monitorTask is not null)
                await _monitorTask;
        }
        finally
        {
            try
            {
                if (_market is not null)
                    await _market.StopAsync();
            }
            finally
            {
                try
                {
                    await ReleaseLockAsync(cancellationToken);
                }
                finally
                {
                    _monitorCancellation?.Dispose();
                }
            }
        }
    }

    private static async Task<bool> TryAcquireLockAsync(
        NpgsqlConnection connection,
        CancellationToken cancellationToken)
    {
        await using var command = CreateLockCommand(
            connection,
            "SELECT pg_try_advisory_lock(@namespace, @resource)");

        return await command.ExecuteScalarAsync(cancellationToken) is true;
    }

    private async Task MonitorLockConnectionAsync(
        NpgsqlConnection connection,
        CancellationTokenSource startupCancellation,
        CancellationToken cancellationToken)
    {
        try
        {
            using var timer = new PeriodicTimer(HeartbeatInterval);

            while (true)
            {
                await CheckLockConnectionAsync(connection, cancellationToken);

                if (!await timer.WaitForNextTickAsync(cancellationToken))
                    break;
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            LocalMarket? market;
            bool startupInProgress;

            lock (_lifecycleSync)
            {
                if (_isStopping)
                    return;

                _lockLost = true;
                startupInProgress = !_startupCompleted;
                market = _market;
            }

            if (startupInProgress)
                startupCancellation.Cancel();

            market?.MarkFaulted();

            _logger.LogCritical(
                exception,
                "The PostgreSQL connection holding the single-instance lock was lost; " +
                "the local market is now faulted until server restart");
        }
    }

    private async Task CheckLockConnectionAsync(
        NpgsqlConnection connection,
        CancellationToken cancellationToken)
    {
        await _lockCommandSync.WaitAsync(cancellationToken);

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT 1";
            command.CommandTimeout = LockCommandTimeoutSeconds;
            await command.ExecuteScalarAsync(cancellationToken);
        }
        finally
        {
            _lockCommandSync.Release();
        }
    }

    private async Task ReleaseLockAsync(CancellationToken cancellationToken)
    {
        var connection = _lockConnection;
        _lockConnection = null;

        if (connection is null)
            return;

        try
        {
            if (connection.State == ConnectionState.Open)
            {
                await using var command = CreateLockCommand(
                    connection,
                    "SELECT pg_advisory_unlock(@namespace, @resource)");
                var released = await command.ExecuteScalarAsync(cancellationToken) is true;

                if (!released)
                {
                    _logger.LogWarning(
                        "The PostgreSQL advisory lock was not owned during shutdown");
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception,
                "Could not explicitly release the PostgreSQL advisory lock; " +
                "closing its non-pooled connection will release the session lock");
        }
        finally
        {
            await connection.DisposeAsync();
        }
    }

    private static NpgsqlCommand CreateLockCommand(
        NpgsqlConnection connection,
        string commandText)
    {
        var command = connection.CreateCommand();
        command.CommandText = commandText;
        command.CommandTimeout = LockCommandTimeoutSeconds;
        command.Parameters.AddWithValue(
            "namespace",
            NpgsqlDbType.Integer,
            AdvisoryLockNamespace);
        command.Parameters.AddWithValue(
            "resource",
            NpgsqlDbType.Integer,
            AdvisoryLockResource);
        return command;
    }
}
