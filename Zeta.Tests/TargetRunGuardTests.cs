using System.Globalization;
using HalHeinrich.Numerics.Experiments;

namespace HalHeinrich.Numerics.Tests;

/// <summary>
/// The guard on <c>Zeta.Experiments</c>' pi^3/zeta(3) schedule: that its last target still
/// realises the error the sweep depth was measured at, and that its ceiling still admits its own
/// default.
/// </summary>
/// <remarks>
/// <para>
/// <b>A defect here would be a hang, not a failure, which is why this exists.</b>
/// <c>../AGENTS.md</c> § Testing discipline: where a defect's symptom is non-termination, assert
/// the precondition of runnability on a path that skips the expensive step. Everything below pays
/// for provider refinement and <b>never runs a sweep</b>, so it costs milliseconds whatever the
/// answer - where the run it guards costs about three quarters of a minute and its last column
/// carries almost all of that.
/// </para>
/// <para>
/// <b>What this checks is that a measurement still applies - it does not predict a depth.</b> At
/// the shipped last target the realised error was about 1e-13.25 and the sweep was measured at
/// 3,939,833 denominators, inside a stated budget of five million. If <see cref="MachinPi"/> or
/// <see cref="BorweinZetaThree"/> ever converges differently and drags the realised error below
/// the floor here, that measurement no longer describes the shipped schedule and nothing is known
/// about what it would cost. The right response is to re-measure, which is what a red test here
/// is asking for.
/// </para>
/// <para>
/// <b>The floor is not derived from the depth by a law.</b> Sweep depth falls into two regimes;
/// <c>../SPEC-rational-ratio.md</c> § 2, "What a search costs: sweep depth, and why no law sizes a
/// guard", owns both and how far apart they fall, and they are not restated here. Which regime
/// this target is in cannot be known in advance, because it is the question being asked - so a
/// bound computed from the schedule would have to trust exactly the extrapolation a budget exists
/// to protect against.
/// </para>
/// <para>
/// This is a control and belongs here rather than beside the runner, because it has a known
/// answer: what a given provider realises at a given target is deterministic. The run it guards
/// has no known answer and stays in <c>Zeta.Experiments</c>.
/// </para>
/// </remarks>
public sealed class TargetRunGuardTests
{
    /// <summary>The exponent the sweep depth was actually measured at.</summary>
    /// <remarks>
    /// <b>A literal, and deliberately not <c>TargetRun.DefaultLastExponent</c>.</b> A guard
    /// written against the shipped constant moves with it: raise the default a decade and every
    /// assertion below quietly re-baselines onto a schedule nobody has priced, which is the
    /// failure this class exists to prevent rather than a smaller version of it. Written out
    /// here, moving the default reddens
    /// <see cref="TheShippedDefaultIsTheExponentThatWasMeasured"/> in milliseconds and demands a
    /// fresh measurement.
    /// <para>
    /// Measured at this bench in Release, 2026-09-07: base step 10, divisor step 19, realised
    /// about 1e-13.25, and the sweep from it reached 3,939,833 denominators in about 24 s, inside
    /// a stated budget of five million.
    /// </para>
    /// </remarks>
    private const int MeasuredLastExponent = 13;

    private static string Inv(FormattableString message) => message.ToString(CultureInfo.InvariantCulture);

    [Fact]
    public void TheShippedDefaultIsTheExponentThatWasMeasured()
    {
        Assert.Equal(MeasuredLastExponent, TargetRun.DefaultLastExponent);
    }

    [Fact]
    public void TheLastTargetStillRealisesTheErrorTheSweepDepthWasMeasuredAt()
    {
        // Refinement only, no sweep, so this costs milliseconds whatever the answer.
        using var refiner = new RatioRefiner(new MachinPi(), 3, new BorweinZetaThree());

        refiner.RefineTo(TargetSchedule.Decade(MeasuredLastExponent));
        BigRational realised = refiner.Current.Ratio.MaxError;

        Assert.True(
            realised <= TargetSchedule.Decade(MeasuredLastExponent),
            Inv($"The last target was not met at all; the refiner stopped at {realised}."));

        // The floor. Below it the providers are landing somewhere the depth has never been
        // measured, and the shipped schedule's cost becomes unknown rather than merely larger.
        Assert.True(
            realised > TargetSchedule.Decade(MeasuredLastExponent + 1),
            Inv($"The last target realised {realised}, tighter than this schedule was measured for; re-measure the sweep depth before trusting the run's cost."));
    }

    [Fact]
    public void OneDecadeFurtherIsAFreshProviderStep_SoTheScheduleStopsByBudgetAndNotByAccident()
    {
        // The other side of the band: the schedule does not stop where it does because the next
        // target would change nothing. It stops because the next one is a real step deeper, whose
        // sweep this bench has only ever extrapolated - about 1.4e7 denominators and 90 s.
        using var refiner = new RatioRefiner(new MachinPi(), 3, new BorweinZetaThree());

        refiner.RefineTo(TargetSchedule.Decade(MeasuredLastExponent + 1));

        Assert.True(
            refiner.Current.Ratio.MaxError < TargetSchedule.Decade(MeasuredLastExponent + 1),
            "The next decade was expected to land on a new provider step; if it no longer does, " +
            "this schedule's stopping point needs re-deciding rather than re-measuring.");
    }

    // ---------- the ceiling, which is a budget rather than a correctness device ----------

    [Fact]
    public void TheCeilingAdmitsTheDefaultSchedule()
    {
        // A ceiling lowered beneath the default would make the shipped command refuse its own
        // out-of-the-box invocation - a failure nothing else here would notice, since Refuse is
        // reached before any refinement is paid for.
        Assert.True(
            TargetRun.MaxLastExponent >= TargetRun.DefaultLastExponent,
            "The ceiling must admit the default, or `target` refuses with no argument at all.");

        Assert.Null(TargetRun.Refuse(TargetRun.DefaultLastExponent));
        Assert.Null(TargetRun.Refuse(TargetRun.MaxLastExponent));
    }

    [Fact]
    public void TheCeilingRefusesWhatItCannotPriceAndSaysWhatItWouldHaveCost()
    {
        // A budget, not a correctness device: both searches provably terminate, so nothing here
        // prevents a wrong answer. What it prevents is a run finishing long after anyone stopped
        // waiting, and the refusal quotes the measurements rather than a formula.
        string? refusal = TargetRun.Refuse(TargetRun.MaxLastExponent + 1);

        Assert.NotNull(refusal);
        Assert.Contains("measured", refusal, StringComparison.Ordinal);
        Assert.Contains("extrapolated", refusal, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(TargetRun.FirstExponent)]
    [InlineData(TargetRun.FirstExponent - 1)]
    public void AScheduleWithNoTrendIsRefused(int lastExponent)
    {
        // One column shows no trend, and fewer than one is not a schedule. Refused on the
        // argument so that TargetSchedule.Decades is never handed a range it would reject with a
        // message about its own parameters instead of about the experiment.
        Assert.NotNull(TargetRun.Refuse(lastExponent));
    }
}
