using exchange_simulator.Enums;
using exchange_simulator.Models.AccountTrading;
using exchange_simulator.Models.TradingCore;
using exchange_simulator.Services;

namespace exchange_simulator.Bots;

public sealed class MarketMakerBot : ITradingBot
{
    private readonly BotTradingContext _context;
    private bool _isStopped;

    public Guid AccountId => _context.AccountId;
    public decimal QuoteOffset { get; }
    public long OrderSize { get; }

    public MarketMakerBot(LocalMarket market, decimal quoteOffset, long orderSize)
    {
        ArgumentNullException.ThrowIfNull(market);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(quoteOffset);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(orderSize);

        if (quoteOffset >= market.Instrument.InitialPrice)
        {
            throw new ArgumentOutOfRangeException(nameof(quoteOffset),
                "Quote offset must be less than the initial instrument price.");
        }

        if (orderSize % market.Instrument.LotSize != 0)
        {
            throw new ArgumentException(
                "Order size must be a multiple of the instrument lot size", nameof(orderSize));
        }

        _context = new BotTradingContext(market, market.MarketMakerAccountId);
        QuoteOffset = quoteOffset;
        OrderSize = orderSize;
    }

    public void ExecuteStep()
    {
        if (_isStopped) return;

        var referencePrice = _context.GetReferencePrice();
        var bidPrice = referencePrice > QuoteOffset
            ? referencePrice - QuoteOffset : (decimal?)null;
        var askPrice = referencePrice <= decimal.MaxValue - QuoteOffset
            ? referencePrice + QuoteOffset : (decimal?)null;
        var activeOrders = _context.GetActiveOrders();
        
        var bidToKeep = FindCurrentQuote(activeOrders, OrderSide.Buy, bidPrice);
        var askToKeep = FindCurrentQuote(activeOrders, OrderSide.Sell, askPrice);
        
        CancelStaleOrders(activeOrders, bidToKeep, askToKeep);
        
        if (bidToKeep is null && bidPrice.HasValue)
            TryPlaceQuote(OrderSide.Buy, bidPrice.Value);
        
        if (askToKeep is null && askPrice.HasValue)
            TryPlaceQuote(OrderSide.Sell, askPrice.Value);
    }
    
    public void Stop()
    {
        _isStopped = true;
        _context.CancelAllActiveOrders();
    }
    
    private OrderSnapshot? FindCurrentQuote(IReadOnlyList<OrderSnapshot> activeOrders, OrderSide side, decimal? targetPrice)
    {
        if (!targetPrice.HasValue)
            return null;

        return activeOrders.FirstOrDefault(order =>
            order.OrderType == OrderType.Limit &&
            order.OrderSide == side &&
            order.Price == targetPrice.Value &&
            order.RemainingSize == OrderSize);
    }
    
    private void CancelStaleOrders(IReadOnlyList<OrderSnapshot> activeOrders, OrderSnapshot? bidToKeep, OrderSnapshot? askToKeep)
    {
        foreach (var order in activeOrders)
        {
            if (order.Id == bidToKeep?.Id || order.Id == askToKeep?.Id)
                continue;

            _context.CancelOrder(order.Id);
        }
    }
    
    private void TryPlaceQuote(OrderSide side, decimal price)
    {
        var account = _context.GetAccountSnapshot();

        if (!HasRequiredResources(account, side, price))
            return;

        _context.PlaceOrder(side, OrderType.Limit, OrderSize, price);
    }

    private bool HasRequiredResources(TradingAccountSnapshot account, OrderSide side, decimal price)
    {
        if (side == OrderSide.Sell)
            return account.Position.AvailableQuantity >= OrderSize;

        try
        {
            return checked(price * OrderSize) <= account.AvailableCash;
        }
        catch (OverflowException)
        {
            return false;
        }
    }
    
}