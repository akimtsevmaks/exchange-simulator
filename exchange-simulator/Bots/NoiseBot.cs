using exchange_simulator.Enums;
using exchange_simulator.Models.AccountTrading;
using exchange_simulator.Models.TradingCore;
using exchange_simulator.Services;

namespace exchange_simulator.Bots;

public sealed class NoiseBot : ITradingBot
{
    private enum NoiseAction
    {
        None,
        Cancel,
        Limit,
        Market
    }
    
    private readonly BotTradingContext _context;
    private readonly Random _random;
    private bool _isStopped;

    public Guid AccountId => _context.AccountId;
    public decimal PriceOffset { get; }
    public int MaxOrderLots { get; }
    public int MaxActiveOrders { get; }
    
    public NoiseBot(LocalMarket market, int randomSeed, decimal priceOffset, int maxOrderLots, int maxActiveOrders)
    {
        ArgumentNullException.ThrowIfNull(market);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(priceOffset);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxOrderLots);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxActiveOrders);

        if (priceOffset >= market.Instrument.InitialPrice)
            throw new ArgumentOutOfRangeException(nameof(priceOffset),
                "Price offset must be less than the initial instrument price");

        if ((long)maxOrderLots > long.MaxValue / market.Instrument.LotSize)
            throw new ArgumentOutOfRangeException(nameof(maxOrderLots),
                "Maximum order size exceeds the supported quantity range");

        _context = new BotTradingContext(market, market.NoiseBotAccountId);
        _random = new Random(randomSeed);
        PriceOffset = priceOffset;
        MaxOrderLots = maxOrderLots;
        MaxActiveOrders = maxActiveOrders;
    }
    
    public void ExecuteStep()
    {
        if (_isStopped)
            return;

        var activeOrders = _context.GetActiveOrders();

        if (activeOrders.Count >= MaxActiveOrders)
        {
            CancelRandomOrder(activeOrders);
            return;
        }

        switch (GetNextAction())
        {
            case NoiseAction.None:
                return;
            case NoiseAction.Cancel:
                CancelRandomOrder(activeOrders);
                return;
            case NoiseAction.Limit:
                TryPlaceLimitOrder();
                return;
            case NoiseAction.Market:
                TryPlaceMarketOrder();
                return;
            default:
                throw new InvalidOperationException("Unknown noise bot action.");
        }
    }
    
    public void Stop()
    {
        if (_isStopped)
            return;

        _isStopped = true;
        _context.CancelAllActiveOrders();
    }
    
    private NoiseAction GetNextAction()
    {
        var value = _random.Next(100);

        if (value < 20)
            return NoiseAction.None;
        if (value < 40)
            return NoiseAction.Cancel;
        if (value < 80)
            return NoiseAction.Limit;

        return NoiseAction.Market;
    }
    
    private void CancelRandomOrder(IReadOnlyList<OrderSnapshot> activeOrders)
    {
        if (activeOrders.Count == 0)
            return;

        var order = activeOrders[_random.Next(activeOrders.Count)];
        _context.CancelOrder(order.Id);
    }
    
    private void TryPlaceLimitOrder()
    {
        var side = GetRandomSide();
        var size = GetRandomOrderSize();
        var price = GetRandomLimitPrice();

        if (!price.HasValue)
            return;

        var account = _context.GetAccountSnapshot();

        if (!HasRequiredResources(account, side, size, price.Value))
            return;

        _context.PlaceOrder(side, OrderType.Limit, size, price.Value);
    }
    
    private void TryPlaceMarketOrder()
    {
        var side = GetRandomSide();
        var size = GetRandomOrderSize();
        var account = _context.GetAccountSnapshot();

        if (side == OrderSide.Sell)
        {
            if (account.Position.AvailableQuantity < size ||
                _context.GetOrderBookSnapshot().Bids.Count == 0)
            {
                return;
            }
        }
        else
        {
            MarketBuyQuote quote;

            try
            {
                quote = _context.GetMarketBuyQuote(size);
            }
            catch (OverflowException)
            {
                return;
            }

            if (quote.ExecutableSize == 0 || quote.Cost > account.AvailableCash)
                return;
        }

        _context.PlaceOrder(side, OrderType.Market, size);
    }
    
    private OrderSide GetRandomSide() =>
        _random.Next(2) == 0 ? OrderSide.Buy : OrderSide.Sell;

    private long GetRandomOrderSize()
    {
        var lots = _random.NextInt64(1, (long)MaxOrderLots + 1);
        return checked(lots * _context.Instrument.LotSize);
    }

    private decimal? GetRandomLimitPrice()
    {
        var referencePrice = _context.GetReferencePrice();
        var direction = _random.Next(-1, 2);

        return direction switch
        {
            -1 when referencePrice > PriceOffset => referencePrice - PriceOffset,
            0 => referencePrice,
            1 when referencePrice <= decimal.MaxValue - PriceOffset =>
                referencePrice + PriceOffset,
            _ => null
        };
    }

    private static bool HasRequiredResources(TradingAccountSnapshot account, OrderSide side, long size, decimal price)
    {
        if (side == OrderSide.Sell)
            return account.Position.AvailableQuantity >= size;

        try
        {
            return checked(price * size) <= account.AvailableCash;
        }
        catch (OverflowException)
        {
            return false;
        }
    }

}