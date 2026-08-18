using exchange_simulator.Server.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace exchange_simulator.Server.Persistence;


internal sealed class ExchangeDbContext(
    DbContextOptions<ExchangeDbContext> options) : DbContext(options)
{
    internal DbSet<TradingWorldEntity> TradingWorlds =>
        Set<TradingWorldEntity>();

    internal DbSet<InstrumentEntity> Instruments =>
        Set<InstrumentEntity>();

    internal DbSet<TradingAccountEntity> TradingAccounts =>
        Set<TradingAccountEntity>();

    internal DbSet<BotAccountEntity> BotAccounts =>
        Set<BotAccountEntity>();

    internal DbSet<PositionEntity> Positions =>
        Set<PositionEntity>();

    internal DbSet<OrderEntity> Orders =>
        Set<OrderEntity>();

    internal DbSet<TradeEntity> Trades =>
        Set<TradeEntity>();

    internal DbSet<OrderHistoryEntryEntity> OrderHistory =>
        Set<OrderHistoryEntryEntity>();

    internal DbSet<AccountOperationEntity> AccountOperations =>
        Set<AccountOperationEntity>();
}