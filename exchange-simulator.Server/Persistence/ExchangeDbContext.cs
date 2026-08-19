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
        ConfigureTradingAccount(modelBuilder);
        ConfigureBotAccount(modelBuilder);
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
    
    private static void ConfigureTradingAccount(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<TradingAccountEntity>();

        entity.ToTable("TradingAccounts", table =>
        {
            table.HasCheckConstraint(
                "CK_TradingAccounts_CashBalance_NonNegative",
                "\"CashBalance\" >= 0");

            table.HasCheckConstraint(
                "CK_TradingAccounts_ReservedCash_Valid",
                "\"ReservedCash\" >= 0 AND \"ReservedCash\" <= \"CashBalance\"");
        });

        entity.HasKey(account => account.Id);
        entity.Property(account => account.Id)
            .ValueGeneratedNever();

        entity.Property(account => account.CashBalance)
            .HasColumnType("numeric");
        entity.Property(account => account.ReservedCash)
            .HasColumnType("numeric");

        entity.HasOne<TradingWorldEntity>()
            .WithMany()
            .HasForeignKey(account => account.WorldId)
            .OnDelete(DeleteBehavior.Restrict);
    }
    
    private static void ConfigureBotAccount(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<BotAccountEntity>();

        entity.ToTable("BotAccounts", table =>
        {
            table.HasCheckConstraint(
                "CK_BotAccounts_Kind_Valid",
                "\"Kind\" IN ('MarketMaker', 'NoiseBot')");
        });

        entity.HasKey(bot => new { bot.WorldId, bot.Kind });

        entity.Property(bot => bot.Kind)
            .HasConversion<string>()
            .HasMaxLength(32);

        entity.HasIndex(bot => bot.AccountId)
            .IsUnique();

        entity.HasOne<TradingWorldEntity>()
            .WithMany()
            .HasForeignKey(bot => bot.WorldId)
            .OnDelete(DeleteBehavior.Restrict);

        entity.HasOne<TradingAccountEntity>()
            .WithMany()
            .HasForeignKey(bot => bot.AccountId)
            .OnDelete(DeleteBehavior.Restrict);
    }
    
    
    
    
    
    
    
}