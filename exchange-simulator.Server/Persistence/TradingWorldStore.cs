using exchange_simulator.Enums;
using exchange_simulator.Models.AccountTrading;
using exchange_simulator.Models.TradingCore;
using exchange_simulator.Server.Persistence.Entities;
using exchange_simulator.Services;

using Microsoft.EntityFrameworkCore;

namespace exchange_simulator.Server.Persistence;


internal sealed class TradingWorldStore(
    ExchangeDbContext dbContext,
    LocalMarketFactory marketFactory,
    ILogger<TradingWorldStore> logger)
{
    private readonly ExchangeDbContext _dbContext = dbContext;
    private readonly LocalMarketFactory _marketFactory = marketFactory;
    private readonly ILogger<TradingWorldStore> _logger = logger;

    internal async Task<LocalMarket> LoadOrCreateAsync(
        CancellationToken cancellationToken)
    {
        await _dbContext.Database.MigrateAsync(cancellationToken);

        await using var transaction =
            await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        var worlds = await _dbContext.TradingWorlds
            .OrderBy(world => world.Id)
            .Take(2)
            .ToArrayAsync(cancellationToken);

        LocalMarket market;
        var cancelledOrderCount = 0;
        var created = worlds.Length == 0;
        Guid worldId;

        if (created)
        {
            market = _marketFactory.CreateNew();
            var newWorld = AddNewWorld(market);
            worldId = newWorld.WorldId;

            await _dbContext.SaveChangesAsync(cancellationToken);

            foreach (var operation in newWorld.InitialOperations)
            {
                _dbContext.AccountOperations.Add(operation);
                await _dbContext.SaveChangesAsync(cancellationToken);
            }
        }
        else
        {
            if (worlds.Length != 1)
            {
                throw new InvalidOperationException(
                    "The database must contain exactly one trading world");
            }

            var restoredWorld = await RestoreWorldAsync(worlds[0], cancellationToken);
            market = restoredWorld.Market;
            worldId = worlds[0].Id;
            var restartChanges = ApplyRestartPolicy(restoredWorld);
            cancelledOrderCount = restartChanges.CancelledOrderCount;

            foreach (var historyEntry in restartChanges.HistoryEntries)
            {
                _dbContext.OrderHistory.Add(historyEntry);
                await _dbContext.SaveChangesAsync(cancellationToken);
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        if (created)
            _logger.LogInformation("Created and persisted a new trading world {WorldId}",
                worldId);
        else
            _logger.LogInformation("Restored trading world {WorldId}; cancelled {CancelledOrderCount} active order remainders",
                worldId, cancelledOrderCount);
    
        return market;
    }

    private NewWorldRows AddNewWorld(LocalMarket market)
    {
        var worldId = Guid.NewGuid();
        var instrument = market.Instrument;
        var initialOperations = new List<AccountOperationEntity>();

        _dbContext.TradingWorlds.Add(new TradingWorldEntity
        {
            Id = worldId,
            InitialCashPerAccount = _marketFactory.InitialCashPerAccount,
            InitialInstrumentsPerAccount = _marketFactory.InitialInstrumentsPerAccount
        });
        _dbContext.Instruments.Add(new InstrumentEntity
        {
            Id = instrument.Id,
            WorldId = worldId,
            Ticker = instrument.Ticker,
            Name = instrument.Name,
            LotSize = instrument.LotSize,
            InitialPrice = instrument.InitialPrice
        });

        var accountIds = new[]
        {
            market.MarketMakerAccountId,
            market.NoiseBotAccountId,
            market.ManualAccountId
        };

        foreach (var accountId in accountIds)
        {
            if (!market.TryGetAccount(accountId, out var account))
                throw new InvalidOperationException($"New trading account {accountId} is missing");

            _dbContext.TradingAccounts.Add(new TradingAccountEntity
            {
                Id = account!.Id,
                WorldId = worldId,
                CashBalance = account.CashBalance,
                ReservedCash = account.ReservedCash
            });
            _dbContext.Positions.Add(new PositionEntity
            {
                AccountId = account.Id,
                InstrumentId = account.Position.InstrumentId,
                Quantity = account.Position.Quantity,
                ReservedQuantity = account.Position.ReservedQuantity,
                AveragePrice = account.Position.AveragePrice
            });

            var operations = market.GetAccountOperations(accountId);

            if (operations.Count != 2 ||
                operations[0].Type != AccountOperationType.InitialCashGranted ||
                operations[1].Type != AccountOperationType.InitialInstrumentsGranted)
            {
                throw new InvalidOperationException(
                    $"New account {accountId} has invalid initial operations");
            }

            initialOperations.AddRange(operations.Select(ToEntity));
        }

        _dbContext.BotAccounts.AddRange(
            new BotAccountEntity
            {
                WorldId = worldId,
                Kind = BotAccountKind.MarketMaker,
                AccountId = market.MarketMakerAccountId
            },
            new BotAccountEntity
            {
                WorldId = worldId,
                Kind = BotAccountKind.NoiseBot,
                AccountId = market.NoiseBotAccountId
            });

        return new NewWorldRows(
            worldId,
            initialOperations);
    }

    private async Task<RestoredWorld> RestoreWorldAsync(
        TradingWorldEntity world,
        CancellationToken cancellationToken)
    {
        ValidateWorld(world);

        var instruments = await _dbContext.Instruments
            .OrderBy(instrument => instrument.Id).ToArrayAsync(cancellationToken);

        if (instruments.Length != 1 || instruments[0].WorldId != world.Id)
            throw new InvalidOperationException("The trading world must contain exactly one instrument");

        var instrumentEntity = instruments[0];
        var instrument = new Instrument(
            instrumentEntity.Id,
            instrumentEntity.Ticker,
            instrumentEntity.Name,
            instrumentEntity.LotSize,
            instrumentEntity.InitialPrice);
        var accounts = await _dbContext.TradingAccounts
            .OrderBy(account => account.Id).ToArrayAsync(cancellationToken);

        if (accounts.Any(account => account.WorldId != world.Id))
            throw new InvalidOperationException("A trading account belongs to another world");

        var positions = await _dbContext.Positions
            .OrderBy(position => position.AccountId).ToArrayAsync(cancellationToken);
        var positionsByAccountId = positions.ToDictionary(
            position => position.AccountId);

        if (positions.Length != accounts.Length ||
            accounts.Any(account => !positionsByAccountId.ContainsKey(account.Id)))
            throw new InvalidOperationException("Every trading account must have exactly one position");

        var botAccounts = await _dbContext.BotAccounts
            .OrderBy(bot => bot.Kind).ToArrayAsync(cancellationToken);
        var (marketMakerAccountId, noiseBotAccountId, manualAccountId) =
            ResolveParticipantAccounts(world.Id, accounts, botAccounts);
        var orderEntities = await _dbContext.Orders
            .OrderBy(order => order.CreatedAt).ThenBy(order => order.Id).ToArrayAsync(cancellationToken);
        var tradeEntities = await _dbContext.Trades
            .OrderBy(trade => trade.SequenceNumber).ToArrayAsync(cancellationToken);
        var historyEntities = await _dbContext.OrderHistory
            .OrderBy(history => history.SequenceNumber).ToArrayAsync(cancellationToken);
        var operationEntities = await _dbContext.AccountOperations
            .OrderBy(operation => operation.SequenceNumber).ToArrayAsync(cancellationToken);
        var operationsByAccountId = operationEntities
            .GroupBy(operation => operation.AccountId)
            .ToDictionary(
                group => group.Key,
                group => group.Select(ToDomain).ToArray());
        var accountStates = new TradingAccountRestoreState[accounts.Length];

        for (var index = 0; index < accounts.Length; index++)
        {
            var account = accounts[index];
            var position = positionsByAccountId[account.Id];
            var operations = operationsByAccountId.GetValueOrDefault(account.Id) ?? [];

            ValidateInitialOperations(world, instrument, account.Id, operations);

            accountStates[index] = new TradingAccountRestoreState(
                new TradingAccountSnapshot(
                    account.Id,
                    account.CashBalance,
                    account.ReservedCash,
                    checked(account.CashBalance - account.ReservedCash),
                    new PositionSnapshot(
                        position.InstrumentId,
                        position.Quantity,
                        position.ReservedQuantity,
                        checked(position.Quantity - position.ReservedQuantity),
                        position.AveragePrice)),
                operations);
        }

        var tradingState = new AccountTradingRestoreState(
            accountStates,
            orderEntities.Select(ToDomain).ToArray(),
            tradeEntities.Select(ToDomain).ToArray(),
            historyEntities.Select(ToDomain).ToArray());
        var market = _marketFactory.Restore(
            instrument,
            tradingState,
            marketMakerAccountId,
            noiseBotAccountId,
            manualAccountId);

        return new RestoredWorld(
            market,
            accounts,
            positionsByAccountId,
            orderEntities.ToDictionary(order => order.Id));
    }

    private RestartPolicyChanges ApplyRestartPolicy(RestoredWorld restoredWorld)
    {
        var cancelledOrders = restoredWorld.Market.ApplyRestartPolicy();
        var historyEntries = new List<OrderHistoryEntryEntity>(cancelledOrders.Count);

        foreach (var cancelledOrder in cancelledOrders)
        {
            if (!restoredWorld.OrdersById.TryGetValue(cancelledOrder.Id, out var orderEntity) ||
                orderEntity.Status != OrderStatus.Active)
            {
                throw new InvalidOperationException($"Active order {cancelledOrder.Id} is missing from persistence");
            }

            ValidateCancelledOrder(cancelledOrder, orderEntity);
            orderEntity.Status = OrderStatus.Cancelled;

            var cancellationEntry = restoredWorld.Market
                .GetOrderHistory(cancelledOrder.Id).Last();

            if (cancellationEntry.EventType != OrderHistoryEventType.Cancelled ||
                cancellationEntry.FilledSize != cancelledOrder.FilledSize ||
                cancellationEntry.RemainingSize != cancelledOrder.RemainingSize ||
                cancellationEntry.TradeId is not null)
            {
                throw new InvalidOperationException(
                    $"Order {cancelledOrder.Id} has an invalid restart cancellation entry");
            }

            historyEntries.Add(ToEntity(cancellationEntry));
        }

        foreach (var accountEntity in restoredWorld.Accounts)
        {
            if (!restoredWorld.Market.TryGetAccount(
                    accountEntity.Id,
                    out var account))
            {
                throw new InvalidOperationException(
                    $"Restored account {accountEntity.Id} is missing");
            }

            var positionEntity =
                restoredWorld.PositionsByAccountId[accountEntity.Id];

            if (account!.CashBalance != accountEntity.CashBalance ||
                account.Position.InstrumentId != positionEntity.InstrumentId ||
                account.Position.Quantity != positionEntity.Quantity ||
                account.Position.AveragePrice != positionEntity.AveragePrice)
            {
                throw new InvalidOperationException(
                    $"Restart policy changed account {accountEntity.Id} totals");
            }

            accountEntity.ReservedCash = account.ReservedCash;
            positionEntity.ReservedQuantity = account.Position.ReservedQuantity;

            if (account.ReservedCash != 0 ||
                account.Position.ReservedQuantity != 0 ||
                restoredWorld.Market.GetActiveOrders(account.Id).Count != 0)
            {
                throw new InvalidOperationException(
                    $"Restart policy did not clear account {account.Id} active reserves");
            }
        }

        var orderBook = restoredWorld.Market.GetOrderBookSnapshot();

        if (orderBook.Bids.Count != 0 || orderBook.Asks.Count != 0)
        {
            throw new InvalidOperationException(
                "Restart policy did not clear the restored order book");
        }

        return new RestartPolicyChanges(
            cancelledOrders.Count,
            historyEntries);
    }

    private static void ValidateWorld(TradingWorldEntity world)
    {
        if (world.Id == Guid.Empty ||
            world.InitialCashPerAccount <= 0 ||
            world.InitialInstrumentsPerAccount <= 0)
        {
            throw new InvalidOperationException("The stored trading world configuration is invalid");
        }
    }

    private static (
        Guid MarketMakerAccountId,
        Guid NoiseBotAccountId,
        Guid ManualAccountId) ResolveParticipantAccounts(
            Guid worldId,
            IReadOnlyList<TradingAccountEntity> accounts,
            IReadOnlyList<BotAccountEntity> botAccounts)
    {
        if (botAccounts.Count != 2 ||
            botAccounts.Any(bot => bot.WorldId != worldId))
        {
            throw new InvalidOperationException(
                "The trading world must contain exactly two bot assignments");
        }

        var marketMakers = botAccounts
            .Where(bot => bot.Kind == BotAccountKind.MarketMaker)
            .ToArray();
        var noiseBots = botAccounts
            .Where(bot => bot.Kind == BotAccountKind.NoiseBot)
            .ToArray();

        if (marketMakers.Length != 1 || noiseBots.Length != 1 ||
            marketMakers[0].AccountId == noiseBots[0].AccountId)
        {
            throw new InvalidOperationException(
                "Bot account assignments are invalid");
        }

        var accountIds = accounts.Select(account => account.Id).ToHashSet();

        if (!accountIds.Contains(marketMakers[0].AccountId) ||
            !accountIds.Contains(noiseBots[0].AccountId))
        {
            throw new InvalidOperationException(
                "A bot assignment references a missing trading account");
        }

        var manualAccountIds = accountIds
            .Except([marketMakers[0].AccountId, noiseBots[0].AccountId])
            .ToArray();

        if (manualAccountIds.Length != 1)
        {
            throw new InvalidOperationException(
                "The current server requires exactly one non-bot trading account");
        }

        return (
            marketMakers[0].AccountId,
            noiseBots[0].AccountId,
            manualAccountIds[0]);
    }

    private static void ValidateInitialOperations(
        TradingWorldEntity world,
        Instrument instrument,
        Guid accountId,
        IReadOnlyList<AccountOperation> operations)
    {
        var cashGrants = operations
            .Where(operation => operation.Type == AccountOperationType.InitialCashGranted).ToArray();
        var instrumentGrants = operations
            .Where(operation => operation.Type == AccountOperationType.InitialInstrumentsGranted).ToArray();

        if (cashGrants.Length != 1 ||
            cashGrants[0].AccountId != accountId ||
            cashGrants[0].CashChange != world.InitialCashPerAccount)
        {
            throw new InvalidOperationException($"Account {accountId} has an invalid initial cash grant");
        }

        if (instrumentGrants.Length != 1 ||
            instrumentGrants[0].AccountId != accountId ||
            instrumentGrants[0].InstrumentId != instrument.Id ||
            instrumentGrants[0].InstrumentQuantityChange !=
                world.InitialInstrumentsPerAccount)
        {
            throw new InvalidOperationException($"Account {accountId} has an invalid initial instrument grant");
        }
    }

    private static void ValidateCancelledOrder(OrderSnapshot cancelledOrder, OrderEntity storedOrder)
    {
        if (cancelledOrder.OwnerId != storedOrder.OwnerAccountId ||
            cancelledOrder.InstrumentId != storedOrder.InstrumentId ||
            cancelledOrder.OrderType != storedOrder.Type ||
            cancelledOrder.OrderSide != storedOrder.Side ||
            cancelledOrder.OrderStatus != OrderStatus.Cancelled ||
            cancelledOrder.Price != storedOrder.Price ||
            cancelledOrder.Size != storedOrder.Size ||
            cancelledOrder.RemainingSize != storedOrder.RemainingSize ||
            cancelledOrder.FilledSize !=
                storedOrder.Size - storedOrder.RemainingSize ||
            cancelledOrder.CreatedAt != storedOrder.CreatedAt)
        {
            throw new InvalidOperationException(
                $"Restart cancellation changed order {storedOrder.Id} data");
        }
    }

    private static OrderSnapshot ToDomain(OrderEntity order) =>
        new(
            order.Id,
            order.OwnerAccountId,
            order.InstrumentId,
            order.Type,
            order.Side,
            order.Status,
            order.Price,
            order.Size,
            order.RemainingSize,
            checked(order.Size - order.RemainingSize),
            order.CreatedAt);

    private static Trade ToDomain(TradeEntity trade) =>
        new(
            trade.Id,
            trade.InstrumentId,
            trade.BuyOrderId,
            trade.SellOrderId,
            trade.Price,
            trade.Size,
            trade.ExecutedAt);

    private static OrderHistoryEntry ToDomain(OrderHistoryEntryEntity history) =>
        new(
            history.OrderId,
            history.EventType,
            history.FilledSize,
            history.RemainingSize,
            history.TradeId,
            history.OccurredAt);

    private static AccountOperation ToDomain(AccountOperationEntity operation) =>
        new(
            operation.Id,
            operation.AccountId,
            operation.Type,
            operation.CashChange,
            operation.InstrumentId,
            operation.InstrumentQuantityChange,
            operation.OrderId,
            operation.TradeId,
            operation.CreatedAt);

    private static AccountOperationEntity ToEntity(AccountOperation operation) =>
        new()
        {
            Id = operation.Id,
            AccountId = operation.AccountId,
            Type = operation.Type,
            CashChange = operation.CashChange,
            InstrumentId = operation.InstrumentId,
            InstrumentQuantityChange = operation.InstrumentQuantityChange,
            OrderId = operation.OrderId,
            TradeId = operation.TradeId,
            CreatedAt = operation.CreatedAt
        };

    private static OrderHistoryEntryEntity ToEntity(OrderHistoryEntry history) =>
        new()
        {
            OrderId = history.OrderId,
            EventType = history.EventType,
            FilledSize = history.FilledSize,
            RemainingSize = history.RemainingSize,
            TradeId = history.TradeId,
            OccurredAt = history.OccurredAt
        };

    private sealed record RestoredWorld(
        LocalMarket Market,
        IReadOnlyList<TradingAccountEntity> Accounts,
        IReadOnlyDictionary<Guid, PositionEntity> PositionsByAccountId,
        IReadOnlyDictionary<Guid, OrderEntity> OrdersById);

    private sealed record NewWorldRows(
        Guid WorldId,
        IReadOnlyList<AccountOperationEntity> InitialOperations);

    private sealed record RestartPolicyChanges(
        int CancelledOrderCount,
        IReadOnlyList<OrderHistoryEntryEntity> HistoryEntries);
}