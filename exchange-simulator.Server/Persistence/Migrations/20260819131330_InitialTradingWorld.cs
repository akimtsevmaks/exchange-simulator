using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace exchange_simulator.Server.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialTradingWorld : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TradingWorlds",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    InitialCashPerAccount = table.Column<decimal>(type: "numeric", nullable: false),
                    InitialInstrumentsPerAccount = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TradingWorlds", x => x.Id);
                    table.CheckConstraint("CK_TradingWorlds_InitialCashPerAccount_Positive", "\"InitialCashPerAccount\" > 0");
                    table.CheckConstraint("CK_TradingWorlds_InitialInstrumentsPerAccount_Positive", "\"InitialInstrumentsPerAccount\" > 0");
                });

            migrationBuilder.CreateTable(
                name: "Instruments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WorldId = table.Column<Guid>(type: "uuid", nullable: false),
                    Ticker = table.Column<string>(type: "character varying(4)", maxLength: 4, nullable: false),
                    Name = table.Column<string>(type: "character varying(99)", maxLength: 99, nullable: false),
                    LotSize = table.Column<long>(type: "bigint", nullable: false),
                    InitialPrice = table.Column<decimal>(type: "numeric", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Instruments", x => x.Id);
                    table.CheckConstraint("CK_Instruments_InitialPrice_Positive", "\"InitialPrice\" > 0");
                    table.CheckConstraint("CK_Instruments_LotSize_Positive", "\"LotSize\" > 0");
                    table.CheckConstraint("CK_Instruments_Name_Length", "char_length(\"Name\") BETWEEN 1 AND 99");
                    table.CheckConstraint("CK_Instruments_Ticker_Length", "char_length(\"Ticker\") = 4");
                    table.ForeignKey(
                        name: "FK_Instruments_TradingWorlds_WorldId",
                        column: x => x.WorldId,
                        principalTable: "TradingWorlds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TradingAccounts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WorldId = table.Column<Guid>(type: "uuid", nullable: false),
                    CashBalance = table.Column<decimal>(type: "numeric", nullable: false),
                    ReservedCash = table.Column<decimal>(type: "numeric", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TradingAccounts", x => x.Id);
                    table.CheckConstraint("CK_TradingAccounts_CashBalance_NonNegative", "\"CashBalance\" >= 0");
                    table.CheckConstraint("CK_TradingAccounts_ReservedCash_Valid", "\"ReservedCash\" >= 0 AND \"ReservedCash\" <= \"CashBalance\"");
                    table.ForeignKey(
                        name: "FK_TradingAccounts_TradingWorlds_WorldId",
                        column: x => x.WorldId,
                        principalTable: "TradingWorlds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "BotAccounts",
                columns: table => new
                {
                    WorldId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    AccountId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BotAccounts", x => new { x.WorldId, x.Kind });
                    table.CheckConstraint("CK_BotAccounts_Kind_Valid", "\"Kind\" IN ('MarketMaker', 'NoiseBot')");
                    table.ForeignKey(
                        name: "FK_BotAccounts_TradingAccounts_AccountId",
                        column: x => x.AccountId,
                        principalTable: "TradingAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BotAccounts_TradingWorlds_WorldId",
                        column: x => x.WorldId,
                        principalTable: "TradingWorlds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Orders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerAccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    InstrumentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Side = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Price = table.Column<decimal>(type: "numeric", nullable: true),
                    Size = table.Column<long>(type: "bigint", nullable: false),
                    RemainingSize = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Orders", x => x.Id);
                    table.CheckConstraint("CK_Orders_Active_IsLimit", "\"Status\" <> 'Active' OR \"Type\" = 'Limit'");
                    table.CheckConstraint("CK_Orders_Price_Valid", "(\"Type\" = 'Limit' AND \"Price\" IS NOT NULL AND \"Price\" > 0) OR (\"Type\" = 'Market' AND \"Price\" IS NULL)");
                    table.CheckConstraint("CK_Orders_Side_Valid", "\"Side\" IN ('Buy', 'Sell')");
                    table.CheckConstraint("CK_Orders_Size_Valid", "\"Size\" > 0 AND \"RemainingSize\" >= 0 AND \"RemainingSize\" <= \"Size\"");
                    table.CheckConstraint("CK_Orders_Status_RemainingSize", "(\"Status\" = 'Filled' AND \"RemainingSize\" = 0) OR (\"Status\" IN ('Active', 'Cancelled') AND \"RemainingSize\" > 0)");
                    table.CheckConstraint("CK_Orders_Status_Valid", "\"Status\" IN ('Active', 'Filled', 'Cancelled')");
                    table.CheckConstraint("CK_Orders_Type_Valid", "\"Type\" IN ('Market', 'Limit')");
                    table.ForeignKey(
                        name: "FK_Orders_Instruments_InstrumentId",
                        column: x => x.InstrumentId,
                        principalTable: "Instruments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Orders_TradingAccounts_OwnerAccountId",
                        column: x => x.OwnerAccountId,
                        principalTable: "TradingAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Positions",
                columns: table => new
                {
                    AccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    InstrumentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Quantity = table.Column<long>(type: "bigint", nullable: false),
                    ReservedQuantity = table.Column<long>(type: "bigint", nullable: false),
                    AveragePrice = table.Column<decimal>(type: "numeric", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Positions", x => new { x.AccountId, x.InstrumentId });
                    table.CheckConstraint("CK_Positions_AveragePrice_Valid", "(\"Quantity\" = 0 AND \"AveragePrice\" = 0) OR (\"Quantity\" > 0 AND \"AveragePrice\" > 0)");
                    table.CheckConstraint("CK_Positions_Quantity_NonNegative", "\"Quantity\" >= 0");
                    table.CheckConstraint("CK_Positions_ReservedQuantity_Valid", "\"ReservedQuantity\" >= 0 AND \"ReservedQuantity\" <= \"Quantity\"");
                    table.ForeignKey(
                        name: "FK_Positions_Instruments_InstrumentId",
                        column: x => x.InstrumentId,
                        principalTable: "Instruments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Positions_TradingAccounts_AccountId",
                        column: x => x.AccountId,
                        principalTable: "TradingAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Trades",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    InstrumentId = table.Column<Guid>(type: "uuid", nullable: false),
                    BuyOrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    SellOrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    SequenceNumber = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Price = table.Column<decimal>(type: "numeric", nullable: false),
                    Size = table.Column<long>(type: "bigint", nullable: false),
                    ExecutedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Trades", x => x.Id);
                    table.CheckConstraint("CK_Trades_DifferentOrders", "\"BuyOrderId\" <> \"SellOrderId\"");
                    table.CheckConstraint("CK_Trades_Price_Positive", "\"Price\" > 0");
                    table.CheckConstraint("CK_Trades_SequenceNumber_Positive", "\"SequenceNumber\" > 0");
                    table.CheckConstraint("CK_Trades_Size_Positive", "\"Size\" > 0");
                    table.ForeignKey(
                        name: "FK_Trades_Instruments_InstrumentId",
                        column: x => x.InstrumentId,
                        principalTable: "Instruments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Trades_Orders_BuyOrderId",
                        column: x => x.BuyOrderId,
                        principalTable: "Orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Trades_Orders_SellOrderId",
                        column: x => x.SellOrderId,
                        principalTable: "Orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AccountOperations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    SequenceNumber = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    CashChange = table.Column<decimal>(type: "numeric", nullable: false),
                    InstrumentId = table.Column<Guid>(type: "uuid", nullable: true),
                    InstrumentQuantityChange = table.Column<long>(type: "bigint", nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: true),
                    TradeId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccountOperations", x => x.Id);
                    table.CheckConstraint("CK_AccountOperations_Payload_Valid", "(\"Type\" = 'InitialCashGranted' AND \"CashChange\" > 0 AND \"InstrumentId\" IS NULL AND \"InstrumentQuantityChange\" = 0 AND \"OrderId\" IS NULL AND \"TradeId\" IS NULL) OR (\"Type\" = 'InitialInstrumentsGranted' AND \"CashChange\" = 0 AND \"InstrumentId\" IS NOT NULL AND \"InstrumentQuantityChange\" > 0 AND \"OrderId\" IS NULL AND \"TradeId\" IS NULL) OR (\"Type\" = 'TradeBuy' AND \"CashChange\" < 0 AND \"InstrumentId\" IS NOT NULL AND \"InstrumentQuantityChange\" > 0 AND \"OrderId\" IS NOT NULL AND \"TradeId\" IS NOT NULL) OR (\"Type\" = 'TradeSell' AND \"CashChange\" > 0 AND \"InstrumentId\" IS NOT NULL AND \"InstrumentQuantityChange\" < 0 AND \"OrderId\" IS NOT NULL AND \"TradeId\" IS NOT NULL)");
                    table.CheckConstraint("CK_AccountOperations_SequenceNumber_Positive", "\"SequenceNumber\" > 0");
                    table.CheckConstraint("CK_AccountOperations_Type_Valid", "\"Type\" IN ('InitialCashGranted', 'InitialInstrumentsGranted', 'TradeBuy', 'TradeSell')");
                    table.ForeignKey(
                        name: "FK_AccountOperations_Instruments_InstrumentId",
                        column: x => x.InstrumentId,
                        principalTable: "Instruments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AccountOperations_Orders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "Orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AccountOperations_Trades_TradeId",
                        column: x => x.TradeId,
                        principalTable: "Trades",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AccountOperations_TradingAccounts_AccountId",
                        column: x => x.AccountId,
                        principalTable: "TradingAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "OrderHistoryEntries",
                columns: table => new
                {
                    SequenceNumber = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    EventType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    FilledSize = table.Column<long>(type: "bigint", nullable: false),
                    RemainingSize = table.Column<long>(type: "bigint", nullable: false),
                    TradeId = table.Column<Guid>(type: "uuid", nullable: true),
                    OccurredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderHistoryEntries", x => x.SequenceNumber);
                    table.CheckConstraint("CK_OrderHistoryEntries_EventType_Valid", "\"EventType\" IN ('Accepted', 'Activated', 'PartiallyFilled', 'Filled', 'Cancelled')");
                    table.CheckConstraint("CK_OrderHistoryEntries_SequenceNumber_Positive", "\"SequenceNumber\" > 0");
                    table.CheckConstraint("CK_OrderHistoryEntries_Sizes_NonNegative", "\"FilledSize\" >= 0 AND \"RemainingSize\" >= 0");
                    table.CheckConstraint("CK_OrderHistoryEntries_Trade_Valid", "(\"EventType\" IN ('PartiallyFilled', 'Filled') AND \"TradeId\" IS NOT NULL) OR (\"EventType\" IN ('Accepted', 'Activated', 'Cancelled') AND \"TradeId\" IS NULL)");
                    table.ForeignKey(
                        name: "FK_OrderHistoryEntries_Orders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "Orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OrderHistoryEntries_Trades_TradeId",
                        column: x => x.TradeId,
                        principalTable: "Trades",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AccountOperations_AccountId_SequenceNumber",
                table: "AccountOperations",
                columns: new[] { "AccountId", "SequenceNumber" });

            migrationBuilder.CreateIndex(
                name: "IX_AccountOperations_AccountId_Type",
                table: "AccountOperations",
                columns: new[] { "AccountId", "Type" },
                unique: true,
                filter: "\"Type\" IN ('InitialCashGranted', 'InitialInstrumentsGranted')");

            migrationBuilder.CreateIndex(
                name: "IX_AccountOperations_InstrumentId",
                table: "AccountOperations",
                column: "InstrumentId");

            migrationBuilder.CreateIndex(
                name: "IX_AccountOperations_OrderId",
                table: "AccountOperations",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_AccountOperations_SequenceNumber",
                table: "AccountOperations",
                column: "SequenceNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AccountOperations_TradeId",
                table: "AccountOperations",
                column: "TradeId");

            migrationBuilder.CreateIndex(
                name: "IX_BotAccounts_AccountId",
                table: "BotAccounts",
                column: "AccountId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Instruments_WorldId",
                table: "Instruments",
                column: "WorldId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OrderHistoryEntries_OrderId_SequenceNumber",
                table: "OrderHistoryEntries",
                columns: new[] { "OrderId", "SequenceNumber" });

            migrationBuilder.CreateIndex(
                name: "IX_OrderHistoryEntries_TradeId",
                table: "OrderHistoryEntries",
                column: "TradeId");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_InstrumentId",
                table: "Orders",
                column: "InstrumentId");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_OwnerAccountId_CreatedAt",
                table: "Orders",
                columns: new[] { "OwnerAccountId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Orders_Status",
                table: "Orders",
                column: "Status",
                filter: "\"Status\" = 'Active'");

            migrationBuilder.CreateIndex(
                name: "IX_Positions_InstrumentId",
                table: "Positions",
                column: "InstrumentId");

            migrationBuilder.CreateIndex(
                name: "IX_Trades_BuyOrderId",
                table: "Trades",
                column: "BuyOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_Trades_InstrumentId",
                table: "Trades",
                column: "InstrumentId");

            migrationBuilder.CreateIndex(
                name: "IX_Trades_SellOrderId",
                table: "Trades",
                column: "SellOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_Trades_SequenceNumber",
                table: "Trades",
                column: "SequenceNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TradingAccounts_WorldId",
                table: "TradingAccounts",
                column: "WorldId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AccountOperations");

            migrationBuilder.DropTable(
                name: "BotAccounts");

            migrationBuilder.DropTable(
                name: "OrderHistoryEntries");

            migrationBuilder.DropTable(
                name: "Positions");

            migrationBuilder.DropTable(
                name: "Trades");

            migrationBuilder.DropTable(
                name: "Orders");

            migrationBuilder.DropTable(
                name: "Instruments");

            migrationBuilder.DropTable(
                name: "TradingAccounts");

            migrationBuilder.DropTable(
                name: "TradingWorlds");
        }
    }
}
