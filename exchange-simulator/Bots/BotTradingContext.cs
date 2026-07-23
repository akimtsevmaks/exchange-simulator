using exchange_simulator.Enums;
using exchange_simulator.Models.AccountTrading;
using exchange_simulator.Models.TradingCore;
using exchange_simulator.Services;

namespace exchange_simulator.Bots;

public sealed class BotTradingContext
{
    private readonly LocalMarket _market;
    
    public Guid AccountId { get; }
    public Instrument Instrument => _market.Instrument;

    internal BotTradingContext(LocalMarket market, Guid accountId)
    {
        ArgumentNullException.ThrowIfNull(market);

        if (accountId != market.MarketMakerAccountId &&
            accountId != market.NoiseBotAccountId)
        {
            throw new ArgumentException("The account is not assigned to a trading bot", nameof(accountId));
        }
        
        _market = market;
        AccountId = accountId;
    }
    
    public OrderBookSnapshot GetOrderBookSnapshot() =>
        _market.GetOrderBookSnapshot();
    
    public decimal GetReferencePrice() =>
        _market.GetReferencePrice();

    public MarketBuyQuote GetMarketBuyQuote(long requestedSize) =>
        _market.GetMarketBuyQuote(requestedSize);
    
    public TradingAccountSnapshot GetAccountSnapshot()
    {
        if (!_market.TryGetAccount(AccountId, out var snapshot))
            throw new InvalidOperationException($"Bot account {AccountId} is missing");
        
        return snapshot!;
    }
    
    public IReadOnlyList<OrderSnapshot> GetActiveOrders() =>
        _market.GetActiveOrders(AccountId);
    
    public OrderCommandResult PlaceOrder(OrderSide side, OrderType type, long size, decimal? price = null) =>
        _market.PlaceOrder(new PlaceOrderCommand(AccountId, side, type, size, price));

    public OrderCommandResult CancelOrder(Guid orderId)
    {
        if (!_market.TryGetOrder(AccountId, orderId, out _))
            return new OrderCommandResult(false, OrderRejectionReason.OrderNotFound, null, []);

        return _market.CancelOrder(orderId);
    }

    public void CancelAllActiveOrders()
    {
        foreach (var order in _market.GetActiveOrders(AccountId))
            CancelOrder(order.Id);
    }

}