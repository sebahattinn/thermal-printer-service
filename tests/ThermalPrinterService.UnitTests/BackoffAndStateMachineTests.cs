using ThermalPrinterService.Application.StateMachine;
using ThermalPrinterService.Domain.Enums;
using ThermalPrinterService.Infrastructure.Connection;

namespace ThermalPrinterService.UnitTests;

public sealed class ExponentialBackoffTests
{
    [Fact]
    public void Attempt_increases_delay_until_capped()
    {
        var b = new ExponentialBackoff(
            initial: TimeSpan.FromMilliseconds(100),
            max: TimeSpan.FromMilliseconds(1000),
            multiplier: 2.0,
            jitterFactor: 0.0); // jitter kapalı -> deterministik

        Assert.Equal(100, b.GetDelay(1).TotalMilliseconds, 0);
        Assert.Equal(200, b.GetDelay(2).TotalMilliseconds, 0);
        Assert.Equal(400, b.GetDelay(3).TotalMilliseconds, 0);
        Assert.Equal(800, b.GetDelay(4).TotalMilliseconds, 0);
        Assert.Equal(1000, b.GetDelay(5).TotalMilliseconds, 0); // capped
        Assert.Equal(1000, b.GetDelay(99).TotalMilliseconds, 0); // capped
    }

    [Fact]
    public void Zero_attempt_returns_initial()
    {
        var b = new ExponentialBackoff(
            TimeSpan.FromMilliseconds(50), TimeSpan.FromMilliseconds(500), 2.0, 0.0);
        Assert.Equal(50, b.GetDelay(0).TotalMilliseconds, 0);
    }

    [Fact]
    public void Jitter_stays_within_band()
    {
        var b = new ExponentialBackoff(
            TimeSpan.FromMilliseconds(100), TimeSpan.FromMilliseconds(1000), 2.0, 0.2);

        // attempt=3 -> base=400ms, jitter +/- 80ms -> [320, 480]
        for (var i = 0; i < 200; i++)
        {
            var d = b.GetDelay(3).TotalMilliseconds;
            Assert.InRange(d, 320, 480);
        }
    }
}

public sealed class PrinterStateMachineTests
{
    [Fact]
    public void Starts_in_disconnected_state()
    {
        var sm = new PrinterStateMachine();
        Assert.Equal(PrinterState.Disconnected, sm.Current);
    }

    [Fact]
    public void Valid_transitions_succeed_and_fire_event()
    {
        var sm = new PrinterStateMachine();
        var fired = 0;
        sm.Transitioned += (_, _) => fired++;

        sm.TransitionTo(PrinterState.Ready);
        sm.TransitionTo(PrinterState.PaperOut);
        sm.TransitionTo(PrinterState.Ready);

        Assert.Equal(PrinterState.Ready, sm.Current);
        Assert.Equal(3, fired);
    }

    [Fact]
    public void Same_state_transition_is_noop_and_does_not_fire_event()
    {
        var sm = new PrinterStateMachine();
        sm.TransitionTo(PrinterState.Ready);

        var fired = 0;
        sm.Transitioned += (_, _) => fired++;
        sm.TransitionTo(PrinterState.Ready);

        Assert.Equal(0, fired);
    }

    [Fact]
    public void Invalid_transition_throws()
    {
        var sm = new PrinterStateMachine();
        // Disconnected -> PaperOut izinli değil (matris: yalnızca Ready/CommError).
        Assert.Throws<InvalidOperationException>(() => sm.TransitionTo(PrinterState.PaperOut));
    }

    [Fact]
    public void CommError_can_recover_to_any_observable_state()
    {
        var sm = new PrinterStateMachine();
        sm.TransitionTo(PrinterState.Ready);
        sm.TransitionTo(PrinterState.CommError);

        Assert.True(sm.CanTransitionTo(PrinterState.Ready));
        Assert.True(sm.CanTransitionTo(PrinterState.PaperOut));
        Assert.True(sm.CanTransitionTo(PrinterState.Disconnected));
    }

    [Fact]
    public void Reset_returns_to_disconnected()
    {
        var sm = new PrinterStateMachine();
        sm.TransitionTo(PrinterState.Ready);
        sm.Reset();
        Assert.Equal(PrinterState.Disconnected, sm.Current);
    }
}
