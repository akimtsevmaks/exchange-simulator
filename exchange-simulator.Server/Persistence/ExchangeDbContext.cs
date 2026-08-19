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


    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        ConfigureTradingWorld(modelBuilder);
        ConfigureInstrument(modelBuilder);
    }

    private static void ConfigureTradingWorld(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<TradingWorldEntity>();
        
        entity.ToTable("TradingWorlds", table =>
        {
            table.HasCheckConstraint(
                "CK_TradingWorlds_InitialCashPerAccount_Positive",
                "\"InitialCashPerAccount\" > 0");

            table.HasCheckConstraint(
                "CK_TradingWorlds_InitialInstrumentsPerAccount_Positive",
                "\"InitialInstrumentsPerAccount\" > 0");
        });

        entity.HasKey(world => world.Id);

        entity.Property(world => world.Id)
            .ValueGeneratedNever();

        entity.Property(world => world.InitialCashPerAccount)
            .HasColumnType("numeric");
    }
    
    private static void ConfigureInstrument(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<InstrumentEntity>();

        entity.ToTable("Instruments", table =>
        {
            table.HasCheckConstraint(
                "CK_Instruments_Ticker_Length",
                "char_length(\"Ticker\") = 4");

            table.HasCheckConstraint(
                "CK_Instruments_Name_Length",
                "char_length(\"Name\") BETWEEN 1 AND 99");

            table.HasCheckConstraint(
                "CK_Instruments_LotSize_Positive",
                "\"LotSize\" > 0");

            table.HasCheckConstraint(
                "CK_Instruments_InitialPrice_Positive",
                "\"InitialPrice\" > 0");
        });

        entity.HasKey(instrument => instrument.Id);

        entity.Property(instrument => instrument.Id)
            .ValueGeneratedNever();

        entity.Property(instrument => instrument.Ticker)
            .HasMaxLength(4)
            .IsRequired();

        entity.Property(instrument => instrument.Name)
            .HasMaxLength(99)
            .IsRequired();

        entity.Property(instrument => instrument.InitialPrice)
            .HasColumnType("numeric");

        entity.HasIndex(instrument => instrument.WorldId)
            .IsUnique();

        entity.HasOne<TradingWorldEntity>()
            .WithMany()
            .HasForeignKey(instrument => instrument.WorldId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}