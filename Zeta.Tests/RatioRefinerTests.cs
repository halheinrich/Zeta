using System.Globalization;
using System.Numerics;

namespace HalHeinrich.Numerics.Tests;

/// <summary>
/// Depth selection: that the target is read on the propagated error, that the provider owning
/// more of that error is the one driven deeper, and that a divisor enclosing zero is refined
/// rather than divided by.
/// </summary>
public sealed class RatioRefinerTests
{
    private static BigRational Ratio(BigInteger numerator, BigInteger denominator) =>
        new(numerator, denominator);

    private static string Inv(FormattableString message) => message.ToString(CultureInfo.InvariantCulture);

    /// <summary>A stub whose bound starts at <paramref name="first"/> and is multiplied by <paramref name="factor"/> each step.</summary>
    private static StubConstant Geometric(BigRational value, BigRational first, BigRational factor, int steps) =>
        StubConstant.Of([.. Enumerable
            .Range(0, steps)
            .Select(step => Approximation.Create(value, first * BigRational.Pow(factor, step)))]);

    // ---------- the rule ----------

    [Fact]
    public void RefineTo_AdvancesWhicheverProviderOwnsTheLargerShareOfTheError()
    {
        // Base 2 and divisor 4, both bounds starting at 1/2 and dropping by 16 each step. With
        // a = 2 and b = 4 the shares are alpha/(4 - beta) and 2*beta/(4*(4 - beta)), so their
        // ratio is 2*alpha/beta: the base is refined exactly while 2*alpha >= beta, which for
        // bounds 2^-(1+4i) and 2^-(1+4j) means while j >= i. Hand-derived, and it gives the path
        // below - base, divisor, base, divisor, ... after an initial base step.
        (int BaseStep, int DivisorStep)[] path =
            [(0, 0), (1, 0), (1, 1), (2, 1), (2, 2), (3, 2), (3, 3)];

        StubConstant baseline = Geometric(BigRational.FromInteger(2), Ratio(1, 2), Ratio(1, 16), 12);
        StubConstant divisorLine = Geometric(BigRational.FromInteger(4), Ratio(1, 2), Ratio(1, 16), 12);

        BigRational[] errors = [.. path.Select(state => RatioEnclosure
            .Of(baseline.StepAt(state.BaseStep), 1, divisorLine.StepAt(state.DivisorStep))
            .Ratio.MaxError)];

        // The fixture only pins a path if each state's error is strictly better than the last, so
        // that a target equal to one state's error cannot be met by an earlier state. Coarsening
        // to powers of two can flatten two neighbouring states, which is why the factor is 16.
        for (int step = 1; step < errors.Length; step++)
        {
            Assert.True(
                errors[step] < errors[step - 1],
                Inv($"Fixture is degenerate: state {step} did not improve on its predecessor."));
        }

        for (int step = 0; step < path.Length; step++)
        {
            using var refiner = new RatioRefiner(
                Geometric(BigRational.FromInteger(2), Ratio(1, 2), Ratio(1, 16), 12),
                1,
                Geometric(BigRational.FromInteger(4), Ratio(1, 2), Ratio(1, 16), 12));

            refiner.RefineTo(errors[step]);

            Assert.Equal(path[step].BaseStep, refiner.BaseStep);
            Assert.Equal(path[step].DivisorStep, refiner.DivisorStep);
        }
    }

    [Fact]
    public void RefineTo_DrivesTheCoarseDivisorFarPastTheSharpBase()
    {
        // The pi^3 / zeta(3) shape: one provider already far more accurate than the other. A
        // refiner halting on the base's own error would stop with the divisor barely refined.
        StubConstant sharpBase = StubConstant.Halving(BigRational.FromInteger(3), Ratio(1, 1_000_000_000));
        StubConstant coarseDivisor = StubConstant.Halving(BigRational.FromInteger(2), Ratio(1, 2));

        using var refiner = new RatioRefiner(sharpBase, 1, coarseDivisor);
        BigRational target = Ratio(1, 1_000_000);
        refiner.RefineTo(target);

        Assert.True(refiner.Current.Ratio.MaxError <= target, "The target was not met.");
        Assert.True(
            refiner.DivisorStep > refiner.BaseStep,
            Inv($"Expected the divisor to be driven deeper; got base {refiner.BaseStep}, divisor {refiner.DivisorStep}."));
    }

    [Fact]
    public void RefineTo_DrivesTheCoarseBaseFarPastTheSharpDivisor()
    {
        // The mirror. A refiner that always favoured one side would pass the test above and fail
        // this one, so the pair is what shows the choice is being made rather than assumed.
        StubConstant coarseBase = StubConstant.Halving(BigRational.FromInteger(3), Ratio(1, 2));
        StubConstant sharpDivisor = StubConstant.Halving(BigRational.FromInteger(2), Ratio(1, 1_000_000_000));

        using var refiner = new RatioRefiner(coarseBase, 1, sharpDivisor);
        BigRational target = Ratio(1, 1_000_000);
        refiner.RefineTo(target);

        Assert.True(refiner.Current.Ratio.MaxError <= target, "The target was not met.");
        Assert.True(
            refiner.BaseStep > refiner.DivisorStep,
            Inv($"Expected the base to be driven deeper; got base {refiner.BaseStep}, divisor {refiner.DivisorStep}."));
    }

    [Fact]
    public void RefineTo_DoesNotDriveEitherProviderPastWhatTheTargetNeeded()
    {
        // Rolling one provider back a step must leave the target unmet for at least one of them:
        // the state before the final advance is exactly such a roll-back, and its error exceeded
        // the target or the refiner would have stopped there. Only one direction is claimed - the
        // other roll-back reaches a state the run never visited, and nothing here proves anything
        // about it.
        StubConstant baseLine = StubConstant.Halving(BigRational.FromInteger(3), Ratio(1, 4));
        StubConstant divisorLine = StubConstant.Halving(BigRational.FromInteger(2), Ratio(1, 3));
        BigRational target = Ratio(1, 100_000);

        using var refiner = new RatioRefiner(baseLine, 1, divisorLine);
        refiner.RefineTo(target);

        Assert.True(refiner.BaseStep > 0 && refiner.DivisorStep > 0, "The fixture did not refine both sides.");

        BigRational shallowerBase = RatioEnclosure
            .Of(baseLine.StepAt(refiner.BaseStep - 1), 1, divisorLine.StepAt(refiner.DivisorStep))
            .Ratio.MaxError;
        BigRational shallowerDivisor = RatioEnclosure
            .Of(baseLine.StepAt(refiner.BaseStep), 1, divisorLine.StepAt(refiner.DivisorStep - 1))
            .Ratio.MaxError;

        Assert.True(
            shallowerBase > target || shallowerDivisor > target,
            Inv($"Both providers could have stopped a step earlier at base {refiner.BaseStep}, divisor {refiner.DivisorStep}."));
    }

    [Fact]
    public void RefineTo_MeetsTheTargetOnTheRatio_NotOnEitherComponent()
    {
        // The rule the whole design turns on. At the stopping point the ratio is inside the
        // target while the divisor's own bound is nowhere near it - so a refiner reading a
        // component's error would have kept going, and one reading the sharper component's would
        // have stopped far too early.
        StubConstant baseLine = StubConstant.Halving(BigRational.FromInteger(30), Ratio(1, 1_000_000_000));
        StubConstant divisorLine = StubConstant.Halving(Ratio(6, 5), Ratio(1, 2));
        BigRational target = Ratio(1, 1024);

        using var refiner = new RatioRefiner(baseLine, 1, divisorLine);
        refiner.RefineTo(target);

        Assert.True(refiner.Current.Ratio.MaxError <= target, "The ratio did not reach the target.");
        Assert.True(
            refiner.Current.Power.MaxError < target,
            "This fixture wants a base far sharper than the target, or it tests nothing.");
        Assert.True(
            refiner.Current.DivisorShare > refiner.Current.PowerShare,
            "This fixture wants the divisor to own most of the error.");
    }

    // ---------- refine first, do not catch ----------

    [Fact]
    public void Constructor_RefinesTheDivisorUntilItsEnclosureExcludesZero()
    {
        // Value 1 with bounds 4, 2, 1, 1/2: the first three enclose zero, since the condition is
        // |Value| > MaxError and the third is an equality. No real provider currently ships an
        // enclosure like this - every one of them excludes zero at step 0 - so this path exists
        // only under a stub, and refusing to write the stub would mean not testing it at all.
        StubConstant divisorLine = StubConstant.Of(
            Approximation.Create(BigRational.One, BigRational.FromInteger(4)),
            Approximation.Create(BigRational.One, BigRational.FromInteger(2)),
            Approximation.Create(BigRational.One, BigRational.One),
            Approximation.Create(BigRational.One, Ratio(1, 2)));

        using var refiner = new RatioRefiner(
            StubConstant.Halving(BigRational.FromInteger(3), Ratio(1, 8)), 1, divisorLine);

        Assert.Equal(3, refiner.DivisorStep);
        Assert.Equal(0, refiner.BaseStep);
        Assert.True(refiner.Current.Divisor.ExcludesZero);
        Assert.Equal(BigRational.FromInteger(3), refiner.Current.Ratio.Value);
    }

    [Fact]
    public void Constructor_DoesNotRefineADivisorThatAlreadyExcludesZero()
    {
        StubConstant divisorLine = StubConstant.Halving(BigRational.FromInteger(2), Ratio(1, 2));

        using var refiner = new RatioRefiner(
            StubConstant.Halving(BigRational.FromInteger(3), Ratio(1, 8)), 1, divisorLine);

        Assert.Equal(0, refiner.DivisorStep);
    }

    // ---------- incremental cost ----------

    [Fact]
    public void RefineTo_AcrossSeveralTargets_PullsEachProviderOnlyAsDeepAsItWent()
    {
        // Incremental across calls, not restarted per target: reaching step n costs n + 1 pulls
        // in total and not n + 1 per target. The stub counts what it handed out; what it is being
        // held to is the refiner's consumption, which it has no say in.
        StubConstant baseLine = StubConstant.Halving(BigRational.FromInteger(3), Ratio(1, 2));
        StubConstant divisorLine = StubConstant.Halving(BigRational.FromInteger(2), Ratio(1, 2));

        using var refiner = new RatioRefiner(baseLine, 1, divisorLine);
        foreach (int power in new[] { 4, 8, 12, 16, 20 })
        {
            refiner.RefineTo(new BigRational(BigInteger.One, BigInteger.Pow(2, power)));
        }

        Assert.Equal(refiner.BaseStep + 1, baseLine.Pulled);
        Assert.Equal(refiner.DivisorStep + 1, divisorLine.Pulled);
    }

    [Fact]
    public void RefineTo_WithATargetAlreadyMet_RefinesNothing()
    {
        StubConstant baseLine = StubConstant.Halving(BigRational.FromInteger(3), Ratio(1, 1024));
        StubConstant divisorLine = StubConstant.Halving(BigRational.FromInteger(2), Ratio(1, 1024));

        using var refiner = new RatioRefiner(baseLine, 1, divisorLine);
        int before = baseLine.Pulled + divisorLine.Pulled;

        refiner.RefineTo(BigRational.One);

        Assert.Equal(0, refiner.BaseStep);
        Assert.Equal(0, refiner.DivisorStep);
        Assert.Equal(before, baseLine.Pulled + divisorLine.Pulled);
    }

    // ---------- guards ----------

    [Fact]
    public void Constructor_RejectsANullProvider()
    {
        StubConstant real = StubConstant.Halving(BigRational.FromInteger(2), Ratio(1, 2));

        Assert.Throws<ArgumentNullException>(() => new RatioRefiner(null!, 1, real));
        Assert.Throws<ArgumentNullException>(() => new RatioRefiner(real, 1, null!));
    }

    [Fact]
    public void Constructor_RejectsAnExponentBelowOne_BeforePayingForARefinement()
    {
        StubConstant baseLine = StubConstant.Halving(BigRational.FromInteger(3), Ratio(1, 2));
        StubConstant divisorLine = StubConstant.Halving(BigRational.FromInteger(2), Ratio(1, 2));

        Assert.Throws<ArgumentOutOfRangeException>(() => new RatioRefiner(baseLine, 0, divisorLine));

        // The point of validating in the constructor rather than letting the composition reject
        // it: a rejected exponent costs no refinement from either provider.
        Assert.Equal(0, baseLine.Pulled);
        Assert.Equal(0, divisorLine.Pulled);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(-1, 2)]
    public void RefineTo_RejectsANonPositiveTarget(int numerator, int denominator)
    {
        using var refiner = new RatioRefiner(
            StubConstant.Halving(BigRational.FromInteger(3), Ratio(1, 2)),
            1,
            StubConstant.Halving(BigRational.FromInteger(2), Ratio(1, 2)));

        Assert.Throws<ArgumentOutOfRangeException>(() => refiner.RefineTo(Ratio(numerator, denominator)));
    }

    [Fact]
    public void RefineTo_AfterDisposal_Throws()
    {
        var refiner = new RatioRefiner(
            StubConstant.Halving(BigRational.FromInteger(3), Ratio(1, 2)),
            1,
            StubConstant.Halving(BigRational.FromInteger(2), Ratio(1, 2)));

        refiner.Dispose();
        refiner.Dispose();

        Assert.Throws<ObjectDisposedException>(() => refiner.RefineTo(Ratio(1, 1024)));
    }
}
