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
        ConfigurePosition(modelBuilder);
        ConfigureOrder(modelBuilder);
        ConfigureTrade(modelBuilder);
        ConfigureOrderHistory(modelBuilder);
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
    
    private static void ConfigurePosition(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<PositionEntity>();

        entity.ToTable("Positions", table =>
        {
            table.HasCheckConstraint(
                "CK_Positions_Quantity_NonNegative",
                "\"Quantity\" >= 0");

            table.HasCheckConstraint(
                "CK_Positions_ReservedQuantity_Valid",
                "\"ReservedQuantity\" >= 0 AND \"ReservedQuantity\" <= \"Quantity\"");

            table.HasCheckConstraint(
                "CK_Positions_AveragePrice_Valid",
                "(\"Quantity\" = 0 AND \"AveragePrice\" = 0) OR " +
                "(\"Quantity\" > 0 AND \"AveragePrice\" > 0)");
        });

        entity.HasKey(position => new
        {
            position.AccountId,
            position.InstrumentId
        });

        entity.Property(position => position.AveragePrice)
            .HasColumnType("numeric");

        entity.HasOne<TradingAccountEntity>()
            .WithMany()
            .HasForeignKey(position => position.AccountId)
            .OnDelete(DeleteBehavior.Restrict);

        entity.HasOne<InstrumentEntity>()
            .WithMany()
            .HasForeignKey(position => position.InstrumentId)
            .OnDelete(DeleteBehavior.Restrict);
    }
    
    private static void ConfigureOrder(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<OrderEntity>();

        entity.ToTable("Orders", table =>
        {
            table.HasCheckConstraint(
                "CK_Orders_Type_Valid",
                "\"Type\" IN ('Market', 'Limit')");

            table.HasCheckConstraint(
                "CK_Orders_Side_Valid",
                "\"Side\" IN ('Buy', 'Sell')");

            table.HasCheckConstraint(
                "CK_Orders_Status_Valid",
                "\"Status\" IN ('Active', 'Filled', 'Cancelled')");

            table.HasCheckConstraint(
                "CK_Orders_Size_Valid",
                "\"Size\" > 0 AND \"RemainingSize\" >= 0 AND \"RemainingSize\" <= \"Size\"");

            table.HasCheckConstraint(
                "CK_Orders_Price_Valid",
                "(\"Type\" = 'Limit' AND \"Price\" IS NOT NULL AND \"Price\" > 0) OR " +
                "(\"Type\" = 'Market' AND \"Price\" IS NULL)");

            table.HasCheckConstraint(
                "CK_Orders_Status_RemainingSize",
                "(\"Status\" = 'Filled' AND \"RemainingSize\" = 0) OR " +
                "(\"Status\" IN ('Active', 'Cancelled') AND \"RemainingSize\" > 0)");

            table.HasCheckConstraint(
                "CK_Orders_Active_IsLimit",
                "\"Status\" <> 'Active' OR \"Type\" = 'Limit'");
        });

        entity.HasKey(order => order.Id);

        entity.Property(order => order.Id)
            .ValueGeneratedNever();

        entity.Property(order => order.Type)
            .HasConversion<string>()
            .HasMaxLength(16);

        entity.Property(order => order.Side)
            .HasConversion<string>()
            .HasMaxLength(16);

        entity.Property(order => order.Status)
            .HasConversion<string>()
            .HasMaxLength(16);

        entity.Property(order => order.Price)
            .HasColumnType("numeric");

        entity.Property(order => order.CreatedAt)
            .HasColumnType("timestamp with time zone");

        entity.HasIndex(order => new
        {
            order.OwnerAccountId,
            order.CreatedAt
        });

        entity.HasIndex(order => order.Status)
            .HasFilter("\"Status\" = 'Active'");

        entity.HasOne<TradingAccountEntity>()
            .WithMany()
            .HasForeignKey(order => order.OwnerAccountId)
            .OnDelete(DeleteBehavior.Restrict);

        entity.HasOne<InstrumentEntity>()
            .WithMany()
            .HasForeignKey(order => order.InstrumentId)
            .OnDelete(DeleteBehavior.Restrict);
    }
    
    private static void ConfigureTrade(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<TradeEntity>();

        entity.ToTable("Trades", table =>
        {
            table.HasCheckConstraint(
                "CK_Trades_SequenceNumber_Positive",
                "\"SequenceNumber\" > 0");

            table.HasCheckConstraint(
                "CK_Trades_Price_Positive",
                "\"Price\" > 0");

            table.HasCheckConstraint(
                "CK_Trades_Size_Positive",
                "\"Size\" > 0");

            table.HasCheckConstraint(
                "CK_Trades_DifferentOrders",
                "\"BuyOrderId\" <> \"SellOrderId\"");
        });

        entity.HasKey(trade => trade.Id);

        entity.Property(trade => trade.Id)
            .ValueGeneratedNever();

        entity.Property(trade => trade.SequenceNumber)
            .UseIdentityByDefaultColumn();

        entity.Property(trade => trade.Price)
            .HasColumnType("numeric");

        entity.Property(trade => trade.ExecutedAt)
            .HasColumnType("timestamp with time zone");

        entity.HasIndex(trade => trade.SequenceNumber)
            .IsUnique();

        entity.HasOne<InstrumentEntity>()
            .WithMany()
            .HasForeignKey(trade => trade.InstrumentId)
            .OnDelete(DeleteBehavior.Restrict);

        entity.HasOne<OrderEntity>()
            .WithMany()
            .HasForeignKey(trade => trade.BuyOrderId)
            .OnDelete(DeleteBehavior.Restrict);

        entity.HasOne<OrderEntity>()
            .WithMany()
            .HasForeignKey(trade => trade.SellOrderId)
            .OnDelete(DeleteBehavior.Restrict);
    }
    
    private static void ConfigureOrderHistory(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<OrderHistoryEntryEntity>();

        entity.ToTable("OrderHistoryEntries", table =>
        {
            table.HasCheckConstraint(
                "CK_OrderHistoryEntries_SequenceNumber_Positive",
                "\"SequenceNumber\" > 0");

            table.HasCheckConstraint(
                "CK_OrderHistoryEntries_EventType_Valid",
                "\"EventType\" IN " +
                "('Accepted', 'Activated', 'PartiallyFilled', 'Filled', 'Cancelled')");

            table.HasCheckConstraint(
                "CK_OrderHistoryEntries_Sizes_NonNegative",
                "\"FilledSize\" >= 0 AND \"RemainingSize\" >= 0");

            table.HasCheckConstraint(
                "CK_OrderHistoryEntries_Trade_Valid",
                "(\"EventType\" IN ('PartiallyFilled', 'Filled') AND \"TradeId\" IS NOT NULL)" +
                " OR " +
                "(\"EventType\" IN ('Accepted', 'Activated', 'Cancelled') AND \"TradeId\" IS NULL)");
        });

        entity.HasKey(history => history.SequenceNumber);

        entity.Property(history => history.SequenceNumber)
            .UseIdentityByDefaultColumn();

        entity.Property(history => history.EventType)
            .HasConversion<string>()
            .HasMaxLength(32);

        entity.Property(history => history.OccurredAt)
            .HasColumnType("timestamp with time zone");

        entity.HasIndex(history => new
        {
            history.OrderId,
            history.SequenceNumber
        });

        entity.HasOne<OrderEntity>()
            .WithMany()
            .HasForeignKey(history => history.OrderId)
            .OnDelete(DeleteBehavior.Restrict);

        entity.HasOne<TradeEntity>()
            .WithMany()
            .HasForeignKey(history => history.TradeId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}