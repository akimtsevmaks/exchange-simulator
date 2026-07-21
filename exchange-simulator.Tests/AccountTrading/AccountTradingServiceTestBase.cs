using exchange_simulator.Enums;
using exchange_simulator.Models;
using exchange_simulator.Services;

namespace exchange_simulator.Tests.AccountTrading;

public abstract class AccountTradingServiceTestBase
{
    protected static Instrument GetAccountTestInstrument(
        long lotSize = 10,
        decimal initialPrice = 100m) =>
        new Instrument(Guid.NewGuid(), "TEST", "Test Instrument", lotSize, initialPrice);

    protected static Guid RegisterAccount(
        AccountTradingService service,
        decimal cash = 0,
        long instruments = 0)
    {
        var accountId = Guid.NewGuid();
        service.RegisterAccount(accountId);
        
        if (cash > 0)
            service.GrantInitialCash(accountId, cash);
        if (instruments > 0)
            service.GrantInitialInstruments(accountId, instruments);

        return accountId;
    }

    protected static TradingAccountSnapshot GetAccount(
        AccountTradingService service,
        Guid accountId)
    {
        var isFound = service.TryGetAccount(accountId, out var account);
        
        Assert.True(isFound);
        return Assert.IsType<TradingAccountSnapshot>(account);
    }

    protected static OrderCommandResult PlaceLimit(
        AccountTradingService service,
        Guid accountId,
        OrderSide side,
        long size = 10,
        decimal price = 100m) =>
        service.PlaceOrder(new PlaceOrderCommand(accountId, side, OrderType.Limit, size, price));
    
    protected static OrderCommandResult PlaceMarket(
        AccountTradingService service,
        Guid accountId,
        OrderSide side,
        long size = 10) =>
        service.PlaceOrder(new PlaceOrderCommand(accountId, side, OrderType.Market, size));
    
    protected static OrderSnapshot GetOrder(OrderCommandResult result) =>
        Assert.IsType<OrderSnapshot>(result.Order);
}