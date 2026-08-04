using exchange_simulator.Enums;
using exchange_simulator.Models.TradingCore;

namespace exchange_simulator.Tests.LocalMarket;

public class LocalMarketLifecycleTests : LocalMarketTestBase
{
    [Fact]
    public void Constructor_ShouldCreateThreeFundedAccountsWithExplicitOperations()
    {
        // Act
        var market = GetMarket(initialCash: 10000m, initialInstruments: 100);

        // Assert
        var accountIds = new[]
        {
            market.MarketMakerAccountId,
            market.NoiseBotAccountId,
            market.ManualAccountId
        };

        Assert.Equal(3, accountIds.Distinct().Count());

        foreach (var accountId in accountIds)
        {
            var account = GetAccount(market, accountId);

            Assert.Equal(10000m, account.CashBalance);
            Assert.Equal(100, account.Position.Quantity);
            Assert.Equal(
                [
                    AccountOperationType.InitialCashGranted,
                    AccountOperationType.InitialInstrumentsGranted
                ],
                market.GetAccountOperations(accountId)
                    .Select(operation => operation.Type));
        }
    }
    
    [Fact]
    public void Constructor_ShouldThrow_WhenStepIntervalIsSubMillisecond()
    {
        // Act
        var act = () =>
            GetMarket(stepInterval: TimeSpan.FromTicks(TimeSpan.TicksPerMillisecond - 1));

        // Assert
        var exception = Assert.Throws<ArgumentOutOfRangeException>(act);
        Assert.Equal("stepInterval", exception.ParamName);
    }

    [Fact]
    public void Constructor_ShouldAcceptOneMillisecondStepInterval()
    {
        // Act
        var market = GetMarket(stepInterval: TimeSpan.FromMilliseconds(1));

        // Assert
        Assert.Equal(LocalMarketStatus.Created, market.Status);
    }


    [Fact]
    public void Step_ShouldRunMarketMakerBeforeNoiseBot()
    {
        // Arrange
        var market = GetMarketWithScriptedNoiseBot(context =>
        {
            context.PlaceOrder(OrderSide.Buy, OrderType.Market, size: 1);
        });

        // Act
        market.Step();

        // Assert
        var trade = Assert.Single(market.GetTrades());

        Assert.Equal(101m, trade.Price);
        Assert.True(market.TryGetOrder(market.NoiseBotAccountId, trade.BuyOrderId, out _));
        Assert.True(market.TryGetOrder(market.MarketMakerAccountId, trade.SellOrderId, out _));
    }

    [Fact]
    public void Step_ShouldContinueWithNextBot_WhenPreviousBotFails()
    {
        // Arrange
        var secondBotSteps = 0;
        var market = GetMarket(market => [
            new ScriptedTradingBot(
                market,
                market.MarketMakerAccountId,
                _ => throw new InvalidOperationException("Expected failure.")),
            new ScriptedTradingBot(
                market,
                market.NoiseBotAccountId,
                _ => secondBotSteps++)
        ]);

        // Act
        market.Step();

        // Assert
        Assert.Equal(1, secondBotSteps);

        var failure = Assert.Single(market.GetBotFailures());
        Assert.Equal(market.MarketMakerAccountId, failure.AccountId);
        Assert.Equal("Step", failure.Operation);
        Assert.Equal(nameof(InvalidOperationException), failure.ExceptionType);
    }

    [Fact]
    public async Task Step_ShouldNotRunInParallel()
    {
        // Arrange
        using var firstStepEntered = new ManualResetEventSlim();
        using var releaseFirstStep = new ManualResetEventSlim();
        using var secondStepEntered = new ManualResetEventSlim();
        var executionCount = 0;

        var market = GetMarket(market => [
            new ScriptedTradingBot(
                market,
                market.MarketMakerAccountId,
                _ =>
                {
                    var currentExecution = Interlocked.Increment(ref executionCount);

                    if (currentExecution == 1)
                    {
                        firstStepEntered.Set();
                        releaseFirstStep.Wait(TimeSpan.FromSeconds(2));
                    }
                    else if (currentExecution == 2)
                    {
                        secondStepEntered.Set();
                    }
                }),
            new ScriptedTradingBot(
                market,
                market.NoiseBotAccountId,
                _ => { })
        ]);

        var firstStep = Task.Run(() => market.Step());
        
        Assert.True(firstStepEntered.Wait(TimeSpan.FromSeconds(2)));

        var secondStep = Task.Run(() => market.Step());

        // Act & Assert
        try
        {
            Assert.False(secondStepEntered.Wait(TimeSpan.FromMilliseconds(100)));
        }
        finally
        {
            releaseFirstStep.Set();
        }

        await Task.WhenAll(firstStep, secondStep)
            .WaitAsync(TimeSpan.FromSeconds(2));

        Assert.True(secondStepEntered.IsSet);
        Assert.Equal(2, executionCount);
    }

    [Fact]
    public async Task StopAsync_ShouldPreventFutureAutonomousAndManualSteps()
    {
        // Arrange
        var firstStepCompleted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var executionCount = 0;
        var market = GetMarket(
            market => [
                new ScriptedTradingBot(
                    market,
                    market.MarketMakerAccountId,
                    _ =>
                    {
                        Interlocked.Increment(ref executionCount);
                        firstStepCompleted.TrySetResult();
                    }),
                new ScriptedTradingBot(
                    market,
                    market.NoiseBotAccountId,
                    _ => { })
            ],
            stepInterval: TimeSpan.FromMilliseconds(10));

        await market.StartAsync();
        await firstStepCompleted.Task.WaitAsync(TimeSpan.FromSeconds(2));

        // Act
        await market.StopAsync().WaitAsync(TimeSpan.FromSeconds(2));
        var executionsAfterStop = Volatile.Read(ref executionCount);
        await Task.Delay(50);

        // Assert
        Assert.Equal(LocalMarketStatus.Stopped, market.Status);
        Assert.Equal(
            executionsAfterStop,
            Volatile.Read(ref executionCount));
        Assert.Throws<InvalidOperationException>(() =>
            market.Step());
    }

    [Fact]
    public void TryStopBot_ShouldCancelItsOrdersWithoutBlockingManualTrading()
    {
        // Arrange
        var market = GetMarketWithScriptedNoiseBot(_ => { });
        market.Step();

        Assert.Equal(2, market.GetActiveOrders(market.MarketMakerAccountId).Count);

        // Act
        var isStopped = market.TryStopBot(market.MarketMakerAccountId);
        var manualResult = PlaceLimit(market, market.ManualAccountId, OrderSide.Buy, size: 10, price: 50m);
        market.Step();

        // Assert
        Assert.True(isStopped);
        Assert.True(manualResult.IsSuccess);
        Assert.Empty(market.GetActiveOrders(market.MarketMakerAccountId));
    }

    [Fact]
    public void ManualAndBotCommands_ShouldPassThroughSameValidation()
    {
        // Arrange
        var market = GetMarket(lotSize: 10);
        var manualCommand = new PlaceOrderCommand(market.ManualAccountId, OrderSide.Buy, OrderType.Limit, Size: 15, Price: 100m);
        var botCommand = manualCommand with
        {
            OwnerId = market.NoiseBotAccountId
        };

        // Act
        var manualResult = market.PlaceOrder(manualCommand);
        var botResult = market.PlaceOrder(botCommand);

        // Assert
        Assert.False(manualResult.IsSuccess);
        Assert.False(botResult.IsSuccess);
        Assert.Equal(
            OrderRejectionReason.QuantityNotMultipleOfLotSize,
            manualResult.RejectionReason);
        Assert.Equal(
            OrderRejectionReason.QuantityNotMultipleOfLotSize,
            botResult.RejectionReason);
        Assert.Empty(market.GetActiveOrders(market.ManualAccountId));
        Assert.Empty(market.GetActiveOrders(market.NoiseBotAccountId));
    }

    [Fact]
    public void ReferencePrice_ShouldChangeOnlyAfterTrade()
    {
        // Arrange
        var market = GetMarketWithScriptedNoiseBot(_ => { });

        // Act & Assert
        market.Step();
        Assert.Equal(100m, market.GetReferencePrice());
        Assert.Empty(market.GetTrades());

        PlaceLimit(market, market.ManualAccountId, OrderSide.Buy, size: 10, price: 90m);
        Assert.Equal(100m, market.GetReferencePrice());
        Assert.Empty(market.GetTrades());

        var marketBuy = PlaceMarket(market, market.ManualAccountId, OrderSide.Buy, size: 10);
        Assert.True(marketBuy.IsSuccess);
        Assert.Single(marketBuy.Trades);
        Assert.Equal(101m, market.GetReferencePrice());
    }
}
