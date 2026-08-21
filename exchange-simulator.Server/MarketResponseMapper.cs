using System.Globalization;
using exchange_simulator.Contracts;
using exchange_simulator.Enums;
using exchange_simulator.Models.TradingCore;
using exchange_simulator.Services;
using ContractMarketStatus = exchange_simulator.Contracts.MarketStatus;

namespace exchange_simulator.Server;


internal static class MarketResponseMapper
{
    public static InstrumentResponse ToInstrumentResponse(Instrument instrument) =>
        new(
            instrument.Id,
            instrument.Ticker,
            instrument.Name,
            instrument.LotSize,
            instrument.InitialPrice);

    public static MarketStateResponse ToMarketStateResponse(LocalMarket market) =>
        new(
            ToMarketStatus(market.Status),
            market.GetReferencePrice());

    public static OrderBookResponse ToOrderBookResponse(OrderBookSnapshot snapshot) =>
        new(
            snapshot.InstrumentId,
            snapshot.Bids.Select(ToOrderBookLevelResponse).ToArray(),
            snapshot.Asks.Select(ToOrderBookLevelResponse).ToArray());

    public static TradeResponse ToTradeResponse(Trade trade) =>
        new(
            trade.Id,
            trade.InstrumentId,
            trade.Price,
            trade.Size,
            trade.ExecutedAt);

    private static OrderBookLevelResponse ToOrderBookLevelResponse(OrderBookLevel level) =>
        new(
            level.Price,
            level.Size.ToString("D", CultureInfo.InvariantCulture));

    private static ContractMarketStatus ToMarketStatus(LocalMarketStatus status) =>
        status switch
        {
            LocalMarketStatus.Created => ContractMarketStatus.Created,
            LocalMarketStatus.Running => ContractMarketStatus.Running,
            LocalMarketStatus.Stopped => ContractMarketStatus.Stopped,
            LocalMarketStatus.Faulted => ContractMarketStatus.Faulted,
            _ => throw new InvalidOperationException($"Unknown {nameof(LocalMarketStatus)}: {status}")
        };
}