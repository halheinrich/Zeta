using System.Globalization;
using System.Numerics;

namespace HalHeinrich.Numerics.Tests;

/// <summary>
/// The run: that it visits every target it was given whatever the output looks like, that the
/// matrix it assembles is dense and ordered, and that an ill-formed schedule is refused.
/// </summary>
public sealed class RatioRunTests
{
    private static BigRational Ratio(BigInteger numerator, BigInteger denominator) =>
        new(numerator, denominator);

    private static string Inv(FormattableString message) => message.ToString(CultureInfo.InvariantCulture);

    private static StubConstant SharpDivisorAtOne() =>
        StubConstant.Halving(BigRational.One, new BigRational(BigInteger.One, BigInteger.Pow(10, 12)));

    // ---------- the run does not stop early ----------

    [Fact]
    public void Execute_VisitsEveryTarget_EvenWhileTheSimplestCandidateHoldsSteady()
    {
        // The false positive the design rejects, built deliberately. The ratio sits at 501/100,
        // so the integer 5 is the simplest rational the evidence permits for two iterations
        // running and then stops being: any "unchanged for two rounds" rule would call the answer
        // here, and be wrong. Measured runs have produced this pattern twice on real targets.
        StubConstant baseLine = StubConstant.Halving(Ratio(501, 100), Ratio(1, 2));

        RatioRun run = RatioRun.Execute(
            baseLine, 1, SharpDivisorAtOne(), [Ratio(1, 2), Ratio(1, 16), Ratio(1, 128)]);

        Assert.Equal(3, run.Iterations.Count);
        Assert.Equal(3, run.Matrix.Ratios.Count);

        Assert.Equal(BigRational.FromInteger(5), run.Iterations[0].Simplest.Value);
        Assert.Equal(BigRational.FromInteger(5), run.Iterations[1].Simplest.Value);
        Assert.NotEqual(BigRational.FromInteger(5), run.Iterations[2].Simplest.Value);
    }

    [Fact]
    public void Execute_ReachesEveryTargetItWasGiven()
    {
        BigRational[] targets = [Ratio(1, 4), Ratio(1, 64), Ratio(1, 4096)];

        RatioRun run = RatioRun.Execute(
            StubConstant.Halving(Ratio(157, 50), Ratio(1, 2)),
            3,
            StubConstant.Halving(Ratio(6, 5), Ratio(1, 4)),
            targets);

        for (int index = 0; index < targets.Length; index++)
        {
            RatioIteration iteration = run.Iterations[index];

            Assert.Equal(targets[index], iteration.TargetError);
            Assert.True(
                iteration.Enclosure.Ratio.MaxError <= targets[index],
                Inv($"Iteration {index} stopped at {iteration.Enclosure.Ratio.MaxError}, above its target."));
        }
    }

    [Fact]
    public void Execute_DrivesTheTwoProvidersToDepthsThatOnlyEverIncrease()
    {
        RatioRun run = RatioRun.Execute(
            StubConstant.Halving(Ratio(157, 50), Ratio(1, 2)),
            3,
            StubConstant.Halving(Ratio(6, 5), Ratio(1, 4)),
            [Ratio(1, 4), Ratio(1, 64), Ratio(1, 4096), Ratio(1, 1 << 20)]);

        for (int index = 1; index < run.Iterations.Count; index++)
        {
            Assert.True(run.Iterations[index].BaseStep >= run.Iterations[index - 1].BaseStep);
            Assert.True(run.Iterations[index].DivisorStep >= run.Iterations[index - 1].DivisorStep);
        }

        // And the two are not lockstep: the point of halting on the propagated error is that the
        // provider owning more of it goes deeper.
        RatioIteration last = run.Iterations[^1];
        Assert.NotEqual(last.BaseStep, last.DivisorStep);
    }

    // ---------- the search ----------

    [Fact]
    public void Execute_EachIterationEndsOnACandidateItsEnclosureContains()
    {
        RatioRun run = RatioRun.Execute(
            StubConstant.Halving(Ratio(157, 50), Ratio(1, 2)),
            3,
            StubConstant.Halving(Ratio(6, 5), Ratio(1, 4)),
            [Ratio(1, 4), Ratio(1, 64), Ratio(1, 4096)]);

        foreach (RatioIteration iteration in run.Iterations)
        {
            Assert.NotEmpty(iteration.Candidates);
            Assert.Equal(iteration.Candidates[^1], iteration.Simplest);
            Assert.True(iteration.Simplest.IsEnclosed, "The terminating candidate must be enclosed.");

            for (int index = 1; index < iteration.Candidates.Count; index++)
            {
                Assert.True(
                    iteration.Candidates[index].Height > iteration.Candidates[index - 1].Height,
                    "Candidates must be of strictly increasing height.");
            }
        }
    }

    [Fact]
    public void Execute_UsesTheApproximatorItIsGiven()
    {
        RatioRun run = RatioRun.Execute(
            StubConstant.Halving(Ratio(501, 100), Ratio(1, 2)),
            1,
            SharpDivisorAtOne(),
            [Ratio(1, 2), Ratio(1, 16)],
            new CentreOnly());

        foreach (RatioIteration iteration in run.Iterations)
        {
            Assert.Single(iteration.Candidates);
            Assert.Equal(iteration.Enclosure.Ratio.Value, iteration.Simplest.Value);
        }
    }

    // ---------- the matrix ----------

    [Fact]
    public void Execute_BuildsADenseMatrixWithOneCellPerCandidatePerIteration()
    {
        RatioRun run = RatioRun.Execute(
            StubConstant.Halving(Ratio(157, 50), Ratio(1, 2)),
            3,
            StubConstant.Halving(Ratio(6, 5), Ratio(1, 4)),
            [Ratio(1, 4), Ratio(1, 64), Ratio(1, 4096)]);

        Assert.Equal(3, run.Matrix.Ratios.Count);
        Assert.NotEmpty(run.Matrix.Rows);

        foreach (TrendRow row in run.Matrix.Rows)
        {
            Assert.Equal(run.Matrix.Ratios.Count, row.Distances.Count);

            for (int column = 0; column < row.Distances.Count; column++)
            {
                BigRational expected = BigRational.Abs(row.Candidate - run.Matrix.Ratios[column].Value);
                Assert.Equal(expected, row.Distances[column]);
            }
        }
    }

    [Fact]
    public void Execute_MatrixRowsCoverEveryCandidateEveryIterationSurfaced()
    {
        RatioRun run = RatioRun.Execute(
            StubConstant.Halving(Ratio(157, 50), Ratio(1, 2)),
            3,
            StubConstant.Halving(Ratio(6, 5), Ratio(1, 4)),
            [Ratio(1, 4), Ratio(1, 64), Ratio(1, 4096)]);

        HashSet<BigRational> surfaced = [.. run.Iterations.SelectMany(i => i.Candidates).Select(c => c.Value)];
        HashSet<BigRational> rows = [.. run.Matrix.Rows.Select(r => r.Candidate)];

        Assert.Equal(surfaced, rows);

        // Ordered by height, so a candidate lands in the same place whichever iteration found it.
        for (int index = 1; index < run.Matrix.Rows.Count; index++)
        {
            Assert.True(run.Matrix.Rows[index].Height >= run.Matrix.Rows[index - 1].Height);
        }
    }

    [Fact]
    public void Execute_RecordsTheEarliestIterationThatSurfacedEachCandidate()
    {
        RatioRun run = RatioRun.Execute(
            StubConstant.Halving(Ratio(157, 50), Ratio(1, 2)),
            3,
            StubConstant.Halving(Ratio(6, 5), Ratio(1, 4)),
            [Ratio(1, 4), Ratio(1, 64), Ratio(1, 4096)]);

        foreach (TrendRow row in run.Matrix.Rows)
        {
            int earliest = run.Iterations
                .Select((iteration, index) => (iteration, index))
                .First(pair => pair.iteration.Candidates.Any(c => c.Value == row.Candidate))
                .index;

            Assert.Equal(earliest, row.FirstSeenAt);
        }
    }

    // ---------- edges and guards ----------

    [Fact]
    public void Execute_WithNoTargets_YieldsAnHonestlyEmptyRun()
    {
        RatioRun run = RatioRun.Execute(
            StubConstant.Halving(BigRational.FromInteger(3), Ratio(1, 2)),
            2,
            StubConstant.Halving(BigRational.FromInteger(2), Ratio(1, 2)),
            []);

        Assert.Empty(run.Iterations);
        Assert.Empty(run.Matrix.Ratios);
        Assert.Empty(run.Matrix.Rows);
        Assert.Equal(2, run.Exponent);
    }

    [Fact]
    public void Execute_WithASingleTarget_YieldsOneColumn()
    {
        RatioRun run = RatioRun.Execute(
            StubConstant.Halving(BigRational.FromInteger(3), Ratio(1, 2)),
            2,
            StubConstant.Halving(BigRational.FromInteger(2), Ratio(1, 2)),
            [Ratio(1, 64)]);

        Assert.Single(run.Iterations);
        Assert.Single(run.Matrix.Ratios);
    }

    [Theory]
    [MemberData(nameof(IllFormedSchedules))]
    public void Execute_RejectsAScheduleThatIsNotStrictlyDecreasingAndPositive(BigRational[] targets)
    {
        Assert.Throws<ArgumentException>(() => RatioRun.Execute(
            StubConstant.Halving(BigRational.FromInteger(3), Ratio(1, 2)),
            1,
            StubConstant.Halving(BigRational.FromInteger(2), Ratio(1, 2)),
            targets));
    }

    public static TheoryData<BigRational[]> IllFormedSchedules() =>
    [
        // A repeat is a duplicate column rather than fresh evidence.
        [new BigRational(1, 4), new BigRational(1, 4)],
        // Increasing: a bound already proven tighter is not un-proven.
        [new BigRational(1, 64), new BigRational(1, 4)],
        // Zero and negative targets are never met, because a bound tends to zero without reaching it.
        [new BigRational(1, 4), BigRational.Zero],
        [BigRational.MinusOne],
    ];

    [Fact]
    public void Execute_RejectsANullArgument()
    {
        StubConstant real = StubConstant.Halving(BigRational.FromInteger(2), Ratio(1, 2));
        BigRational[] targets = [Ratio(1, 4)];

        Assert.Throws<ArgumentNullException>(() => RatioRun.Execute(null!, 1, real, targets));
        Assert.Throws<ArgumentNullException>(() => RatioRun.Execute(real, 1, null!, targets));
        Assert.Throws<ArgumentNullException>(() => RatioRun.Execute(real, 1, real, null!));
    }

    [Fact]
    public void Execute_RejectsAnExponentBelowOne()
    {
        StubConstant real = StubConstant.Halving(BigRational.FromInteger(2), Ratio(1, 2));

        Assert.Throws<ArgumentOutOfRangeException>(
            () => RatioRun.Execute(real, 0, real, [Ratio(1, 4)]));
    }

    /// <summary>
    /// A searcher that proposes the enclosure's own centre and stops. Not a rational approximator
    /// in any useful sense - its only job is to be distinguishable from <c>DenominatorSweep</c>,
    /// so that "the run used the searcher it was handed" is a claim with evidence behind it.
    /// </summary>
    private sealed class CentreOnly : IRationalApproximator
    {
        public IEnumerable<RationalCandidate> Search(Approximation enclosure)
        {
            yield return RationalCandidate.Against(enclosure.Value, enclosure);
        }
    }
}
