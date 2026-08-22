using System.Data;
using exchange_simulator.Services;
using Npgsql;
using NpgsqlTypes;

namespace exchange_simulator.Server;


internal sealed class LocalMarketHostedService(
    IServiceProvider services,
    IConfiguration configuration,
    ILogger<LocalMarketHostedService> logger) : IHostedService
{
    private const int AdvisoryLockNamespace = 0x45584348;
    private const int AdvisoryLockResource = 1;
    private const int LockCommandTimeoutSeconds = 5;
    private static readonly TimeSpan HeartbeatInterval = TimeSpan.FromSeconds(5);

    private readonly IServiceProvider _services = services;
    private readonly IConfiguration _configuration = configuration;
    private readonly ILogger<LocalMarketHostedService> _logger = logger;
    private readonly Lock _lifecycleSync = new();

    private NpgsqlConnection? _lockConnection;
    private LocalMarket? _market;
    private CancellationTokenSource? _monitorCancellation;
    private Task? _monitorTask;
    private bool _isStopping;

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

            var market = _services.GetRequiredService<LocalMarket>();
            _market = market;
            await market.StartAsync(cancellationToken);

            _monitorCancellation = new CancellationTokenSource();
            _monitorTask = MonitorLockConnectionAsync(
                connection,
                _monitorCancellation.Token);

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

                if (_market is not null)
                    await _market.StopAsync();
            }
            finally
            {
                _monitorCancellation?.Dispose();
                await connection.DisposeAsync();
                _lockConnection = null;
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
        CancellationToken cancellationToken)
    {
        try
        {
            using var timer = new PeriodicTimer(HeartbeatInterval);

            while (await timer.WaitForNextTickAsync(cancellationToken))
            {
                await using var command = connection.CreateCommand();
                command.CommandText = "SELECT 1";
                command.CommandTimeout = LockCommandTimeoutSeconds;
                await command.ExecuteScalarAsync(cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            lock (_lifecycleSync)
            {
                if (_isStopping)
                    return;

                _market?.MarkFaulted();
            }

            _logger.LogCritical(
                exception,
                "The PostgreSQL connection holding the single-instance lock was lost; " +
                "the local market is now faulted until server restart");
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

                var released =
                    await command.ExecuteScalarAsync(cancellationToken) is true;

                if (!released)
                {
                    _logger.LogWarning(
                        "The PostgreSQL advisory lock was not owned during shutdown");
                }
            }
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
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