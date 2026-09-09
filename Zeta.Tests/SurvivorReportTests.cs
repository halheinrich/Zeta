using System.Globalization;
using System.Numerics;
using System.Xml.Linq;
using HalHeinrich.Numerics.Experiments;

namespace HalHeinrich.Numerics.Tests;

/// <summary>
/// The survivor report: the projection off a run's iterations, the derived denominator bound, the
/// intersection that is not a filter, and the two argument guards the command refuses on.
/// </summary>
/// <remarks>
/// <para>
/// <b>The command itself is not run here and has no pass or fail.</b> What is held is the
/// plumbing beneath it, every piece of which is a pure function: a list of enclosures in, a
/// survivor set out. <c>../AGENTS.md</c> § Exactness discipline puts the run in a runnable project
/// because its answer is unknown and it depends on wall-clock time; neither is true of an
/// intersection over three enclosures written out by hand.
/// </para>
/// <para>
/// The enclosures below are chosen to exercise decisions rather than to resemble anything. The
/// non-nesting pair is the case that matters most: it is the reason the survivor set is an
/// intersection across every enclosure, and a filter on the latest would pass every other test
/// here.
/// </para>
/// </remarks>
public sealed class SurvivorReportTests
{
    private const int Cap = 4;

    private static BigRational Ratio(BigInteger numerator, BigInteger denominator) =>
        new(numerator, denominator);

    private static Approximation At(BigRational value, BigInteger errorNumerator, BigInteger errorDenominator) =>
        Approximation.Create(value, new BigRational(errorNumerator, errorDenominator));

    private static string Inv(FormattableString message) => message.ToString(CultureInfo.InvariantCulture);

    // ---------- the derived bound ----------

    [Theory]
    [InlineData(1, 10000, 100)]     // an exact square: 1e-4 gives exactly 100
    [InlineData(1, 9999, 99)]       // just under it, so the floor bites
    [InlineData(1, 10201, 101)]     // 101 squared, to pin that the floor is not an off-by-one
    [InlineData(1, 2, 1)]           // sqrt(2) = 1.41..., floored to 1
    [InlineData(4, 9, 1)]           // sqrt(9/4) = 1.5, floored to 1
    [InlineData(9, 4, 0)]           // sqrt(4/9) = 0.66..., floored to 0 - a bound admitting nothing
    public void DerivedBound_IsTheFlooredInverseSquareRootOfTheError(
        int errorNumerator, int errorDenominator, int expected)
    {
        Approximation enclosure = At(BigRational.FromInteger(6), errorNumerator, errorDenominator);

        Assert.Equal(BigInteger.Parse(expected.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture),
            SurvivorReport.DerivedBound(enclosure));
    }

    [Fact]
    public void DerivedBound_IsExactAtSizesNoDoubleCouldHold()
    {
        // 10^-60 gives exactly 10^30, which a double cannot represent as an integer. The
        // derivation is integer arithmetic throughout, so this is not a size at which it degrades.
        Approximation enclosure = Approximation.Create(
            BigRational.FromInteger(6), new BigRational(BigInteger.One, BigInteger.Pow(10, 60)));

        Assert.Equal(BigInteger.Pow(10, 30), SurvivorReport.DerivedBound(enclosure));
    }

    [Fact]
    public void DerivedBound_RefusesAnExactEnclosure()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => SurvivorReport.DerivedBound(Approximation.Exact(BigRational.FromInteger(6))));
    }

    // ---------- the projection off a run ----------

    [Fact]
    public void EnclosuresOf_TakesTheRatioAndNotAnOperand()
    {
        // The run is built so that all four candidate enclosures are centred on different values:
        // the base on 3, its square on 9, the divisor on 3/2 and the ratio on 6. A projection that
        // reached for the wrong one produces a survivor set that is entirely well formed and about
        // the wrong number, which is why this asserts on what the search decides rather than
        // reading the projection back out.
        RatioRun run = RatioRun.Execute(
            StubConstant.Halving(BigRational.FromInteger(3), Ratio(1, 8)),
            2,
            StubConstant.Halving(Ratio(3, 2), Ratio(1, BigInteger.Pow(10, 12))),
            [Ratio(1, 16), Ratio(1, 1024)]);

        SurvivorReport report = SurvivorReport.Of(
            SurvivorReport.EnclosuresOf(run), 20, Cap);

        Assert.Equal([BigRational.FromInteger(6)], report.Survivors);
        Assert.DoesNotContain(BigRational.FromInteger(3), report.Survivors);
        Assert.DoesNotContain(BigRational.FromInteger(9), report.Survivors);
        Assert.DoesNotContain(Ratio(3, 2), report.Survivors);
    }

    [Fact]
    public void EnclosuresOf_KeepsOneEnclosurePerIteration()
    {
        RatioRun run = RatioRun.Execute(
            StubConstant.Halving(BigRational.FromInteger(3), Ratio(1, 8)),
            2,
            StubConstant.Halving(Ratio(3, 2), Ratio(1, BigInteger.Pow(10, 12))),
            [Ratio(1, 16), Ratio(1, 256), Ratio(1, 1024)]);

        Assert.Equal(run.Iterations.Count, SurvivorReport.EnclosuresOf(run).Count);
    }

    // ---------- collapsing repeated enclosures ----------

    [Fact]
    public void Distinct_CollapsesAdjacentRepeatsOnly()
    {
        Approximation first = At(BigRational.FromInteger(6), 1, 2);
        Approximation second = At(BigRational.FromInteger(6), 1, 4);

        // The last entry repeats the first without being adjacent to it. Collapsing that too would
        // be de-duplication rather than the intended dropping of a column the refiner did not
        // advance, and would silently drop evidence in a run whose enclosure widened back.
        Assert.Equal(
            [first, second, first],
            SurvivorReport.Distinct([first, first, second, second, second, first]));
    }

    [Fact]
    public void Distinct_LeavesASingleEnclosureAlone()
    {
        Approximation only = At(BigRational.FromInteger(6), 1, 2);

        Assert.Equal([only], SurvivorReport.Distinct([only]));
    }

    // ---------- the intersection ----------

    [Fact]
    public void Of_CountsWhatEachEnclosureLeavesStanding()
    {
        // Q = 2, so the candidates are p/1 and p/2 in lowest terms.
        //   [5.5, 6.5] holds 6/1, 11/2 and 13/2.
        //   [5.75, 6.25] holds 6/1 alone: 12/2 is not in lowest terms.
        SurvivorReport report = SurvivorReport.Of(
            [At(BigRational.FromInteger(6), 1, 2), At(BigRational.FromInteger(6), 1, 4)], 2, Cap);

        Assert.Equal([3L, 1L], report.Counts);
        Assert.Equal([BigRational.FromInteger(6)], report.Survivors);
        Assert.Equal(1, report.SurvivorCount);
    }

    [Fact]
    public void Of_IntersectsAcrossEveryEnclosureRatherThanFilteringOnTheLast()
    {
        // The enclosures do not nest: 5.9 is in the first and not the second, 6.2 in the second
        // and not the first. SurvivorSearch's own remarks use this pair, and it is the reason the
        // result is an intersection - a filter on the latest enclosure would keep 31/5, which the
        // first enclosure has already refuted permanently.
        Approximation centred = At(BigRational.FromInteger(6), 1, 10);
        Approximation shifted = At(Ratio(61, 10), 1, 10);

        SurvivorReport pair = SurvivorReport.Of([centred, shifted], 10, Cap);
        SurvivorReport latest = SurvivorReport.Of([shifted], 10, Cap);

        Assert.Equal([BigRational.FromInteger(6), Ratio(61, 10)], pair.Survivors);
        Assert.Contains(Ratio(31, 5), latest.Survivors);
        Assert.DoesNotContain(Ratio(31, 5), pair.Survivors);
    }

    [Fact]
    public void Of_LeavesTheCountsNonincreasing()
    {
        SurvivorReport report = SurvivorReport.Of(
            [
                At(BigRational.FromInteger(6), 1, 2),
                At(BigRational.FromInteger(6), 1, 4),
                At(BigRational.FromInteger(6), 1, 100),
            ],
            20,
            Cap);

        for (int index = 1; index < report.Counts.Count; index++)
        {
            Assert.True(
                report.Counts[index] <= report.Counts[index - 1],
                Inv($"Count {index} rose to {report.Counts[index]} from {report.Counts[index - 1]}."));
        }
    }

    [Fact]
    public void Of_ReportsAnEmptySetAsARefutationRatherThanThrowing()
    {
        // No rational of denominator 1 lies in [6.2, 6.4], so nothing survives. An empty result is
        // the strongest outcome this bench produces and must be reachable.
        SurvivorReport report = SurvivorReport.Of([At(Ratio(63, 10), 1, 10)], 1, Cap);

        Assert.Equal(0, report.SurvivorCount);
        Assert.Empty(report.Survivors);
    }

    // ---------- what the distance chart follows ----------

    [Fact]
    public void Of_FollowsTheSurvivorsFirstAndThenTheNearestExcluded()
    {
        // The count goes 3 then 1, so no stage ever holds a small set larger than the answer. A
        // rule keyed on the stage would leave one line on the chart and nothing to compare it
        // with; keying on distance from the final ratio keeps the near misses that died last.
        SurvivorReport report = SurvivorReport.Of(
            [At(BigRational.FromInteger(6), 1, 2), At(BigRational.FromInteger(6), 1, 4)], 2, Cap);

        Assert.Equal(BigRational.FromInteger(6), report.Tracked[0]);
        Assert.Contains(Ratio(11, 2), report.Tracked);
        Assert.Contains(Ratio(13, 2), report.Tracked);
    }

    [Fact]
    public void Of_FollowsNoMoreCandidatesThanTheCap()
    {
        SurvivorReport report = SurvivorReport.Of(
            [At(BigRational.FromInteger(6), 1, 2)], 40, 3);

        Assert.True(report.Counts[0] > 3, "The fixture must offer more candidates than the cap.");
        Assert.Equal(3, report.Tracked.Count);
    }

    [Fact]
    public void Of_FollowsTheNearestByExactDistanceAndNotByDenominator()
    {
        // 6/1 is nearest at distance 0; then 11/2 and 13/2 at 1/2 each. A selection that took the
        // simplest denominators first would agree here by accident, so the cap is set to admit
        // exactly the nearest two and the third is checked to be absent.
        SurvivorReport report = SurvivorReport.Of(
            [At(BigRational.FromInteger(6), 51, 100), At(BigRational.FromInteger(6), 1, 4)], 2, 2);

        Assert.Equal([BigRational.FromInteger(6), Ratio(11, 2)], report.Tracked);
    }

    // ---------- guards ----------

    [Fact]
    public void Of_RefusesNoEnclosuresAtAll()
    {
        // Nothing refutes, so every rational within the bound survives and there are infinitely
        // many of them. An empty result would report complete refutation from no evidence.
        Assert.Throws<ArgumentException>(() => SurvivorReport.Of([], 10, Cap));
    }

    [Fact]
    public void Of_RefusesACapBelowOne()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => SurvivorReport.Of([At(BigRational.FromInteger(6), 1, 2)], 10, 0));
    }

    // ---------- the null ----------

    [Fact]
    public void ExpectedSurvivors_IsSixOverPiSquaredForAUnitIntervalAtDenominatorOne()
    {
        // 6*eps*q^2/pi^2 with eps = 1 and q = 1 is 6/pi^2 = 0.60792710185...
        Approximation expected = SurvivorReport.ExpectedSurvivors(BigRational.One, 1);

        Assert.True(expected.Value > new BigRational(6079271, 10000000));
        Assert.True(expected.Value < new BigRational(6079272, 10000000));
        Assert.Equal("0.61", Presentation.Roughly(expected.Value));

        // Bracketed rather than compared against a decimal literal, because the result is an
        // enclosure and a narrow one: pi is pinned to 1e-30 before it is squared, so a seven-digit
        // literal is nowhere near inside it. What is asserted is that the figure carries a proven
        // bound like every other quantity here rather than arriving as a hard-coded 0.61.
        Assert.False(expected.IsExact);
        Assert.True(expected.MaxError < new BigRational(BigInteger.One, BigInteger.Pow(10, 25)));
    }

    [Theory]
    [InlineData(3)]
    [InlineData(5)]
    [InlineData(15)]
    public void ExpectedSurvivors_IsTheSameAtEveryPrecisionOnceTheBoundIsDerived(int decades)
    {
        // The load-bearing claim, and it is an identity rather than a measurement. Q is derived as
        // eps^(-1/2), so eps*Q^2 is 1 and 6/pi^2 is all that survives. Running deeper therefore
        // does not thin the spurious survivors - it only gives them larger denominators, which is
        // why a bare count is not evidence however impressive the collapse that produced it.
        BigRational error = new(BigInteger.One, BigInteger.Pow(10, 2 * decades));
        BigInteger bound = BigInteger.Pow(10, decades);

        Assert.Equal(
            SurvivorReport.ExpectedSurvivors(BigRational.One, 1).Value,
            SurvivorReport.ExpectedSurvivors(error, bound).Value);
    }

    [Fact]
    public void ExpectedSurvivors_GrowsWithTheSquareOfTheDenominator()
    {
        BigRational error = new(BigInteger.One, BigInteger.Pow(10, 8));

        BigRational one = SurvivorReport.ExpectedSurvivors(error, 500).Value;
        BigRational four = SurvivorReport.ExpectedSurvivors(error, 1000).Value;

        // Exact: only pi is enclosed, and it is the same enclosure in both.
        Assert.Equal(one * BigRational.FromInteger(4), four);
    }

    [Fact]
    public void ExpectedSurvivors_IsZeroWhereNothingCouldSurvive()
    {
        Assert.Equal(BigRational.Zero, SurvivorReport.ExpectedSurvivors(BigRational.Zero, 11585).Value);
        Assert.Equal(BigRational.Zero, SurvivorReport.ExpectedSurvivors(BigRational.One, 0).Value);
    }

    [Fact]
    public void ExpectedSurvivors_RefusesANegativeHalfWidthOrDenominator()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => SurvivorReport.ExpectedSurvivors(BigRational.MinusOne, 10));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => SurvivorReport.ExpectedSurvivors(BigRational.One, -1));
    }

    [Fact]
    public void ExpectedAt_PricesAgainstTheFinalHalfWidthAndNotTheFirst()
    {
        // The whole point of the figure is that it describes the run that happened. Pricing
        // against the widest enclosure would overstate the null by the factor the run narrowed by,
        // and would make every survivor look unremarkable.
        SurvivorReport report = SurvivorReport.Of(
            [At(BigRational.FromInteger(6), 1, 2), At(BigRational.FromInteger(6), 1, 4)], 2, Cap);

        Assert.Equal(SurvivorReport.ExpectedSurvivors(new BigRational(1, 4), 7).Value, report.ExpectedAt(7).Value);
        Assert.NotEqual(SurvivorReport.ExpectedSurvivors(new BigRational(1, 2), 7).Value, report.ExpectedAt(7).Value);
    }

    [Fact]
    public void ExpectedUnderBound_PricesTheWholeBound()
    {
        SurvivorReport report = SurvivorReport.Of(
            [At(BigRational.FromInteger(6), 1, 4)], 2, Cap);

        Assert.Equal(report.ExpectedAt(report.DenominatorBound).Value, report.ExpectedUnderBound.Value);
    }

    // ---------- the command's own refusals, which are pure functions ----------

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void RefuseOrder_RefusesAnOrderZetaHasNoValueFor(int order) =>
        Assert.NotNull(SurvivorRun.RefuseOrder(order));

    [Fact]
    public void RefuseOrder_AcceptsTheDefaultAndTheCeiling()
    {
        Assert.Null(SurvivorRun.RefuseOrder(SurvivorRun.DefaultOrder));
        Assert.Null(SurvivorRun.RefuseOrder(SurvivorRun.MaxOrder));
    }

    [Fact]
    public void RefuseOrder_RefusesPastTheCeilingAndSaysWhy() =>
        Assert.Contains("positive controls", SurvivorRun.RefuseOrder(SurvivorRun.MaxOrder + 1), StringComparison.Ordinal);

    [Fact]
    public void Estimate_CountsTheCandidatesUnderTheWidestEnclosure()
    {
        // h*Q^2 + Q with h = 1/100 and Q = 100: a hundred from the interval widths and a hundred
        // from the one endpoint each denominator contributes.
        Assert.Equal(200, SurvivorRun.Estimate(At(BigRational.FromInteger(6), 1, 100), 100));
    }

    [Fact]
    public void Estimate_StaysExactWhereADoubleWouldNot()
    {
        // Q = 10^12, so Q squared is 10^24 - past the integers a double holds. A figure quoted in
        // a refusal must not be wrong in its leading digits.
        BigInteger bound = BigInteger.Pow(10, 12);

        Assert.Equal(
            BigInteger.Pow(10, 22) + bound,
            SurvivorRun.Estimate(At(BigRational.FromInteger(6), 1, 100), bound));
    }

    [Fact]
    public void Refuse_PassesARunItCanAffordAndStopsOneItCannot()
    {
        Approximation widest = At(BigRational.FromInteger(6), 1, 100);

        Assert.Null(SurvivorRun.Refuse(widest, 100));
        Assert.NotNull(SurvivorRun.Refuse(widest, BigInteger.Pow(10, 9)));
    }

    [Fact]
    public void Refuse_SaysToShortenTheScheduleRatherThanLowerTheBound()
    {
        // The bound is derived on purpose. A refusal that invited lowering it would invite exactly
        // the hand-picked cap that made the exploration's second graph misleading.
        string? refusal = SurvivorRun.Refuse(At(BigRational.FromInteger(6), 1, 100), BigInteger.Pow(10, 9));

        Assert.Contains("Shorten the schedule", refusal, StringComparison.Ordinal);
    }

    // ---------- the document ----------

    [Fact]
    public void Chart_WritesWellFormedXmlForARealReport()
    {
        SurvivorReport report = SurvivorReport.Of(
            [At(BigRational.FromInteger(6), 1, 2), At(BigRational.FromInteger(6), 1, 4)], 2, Cap);

        using var document = new StringWriter(CultureInfo.InvariantCulture);
        SurvivorChart.Write(document, report, new ChartCaption(
            "pi^2 / zeta(2)", "MachinPi, EulerMaclaurinZeta(2)", "DenominatorSweep", "1e-2 .. 1e-4", "Q = 2"));

        XDocument parsed = XDocument.Parse(document.ToString());

        // One line for the collapse, one for the half-width, and one per candidate the distance
        // panel follows. Deriving the figure rather than writing it out is what makes this fail if
        // a series is ever dropped instead of drawn off the edge.
        Assert.Equal("svg", parsed.Root?.Name.LocalName);
        Assert.Equal(
            2 + report.Tracked.Count,
            parsed.Descendants().Count(node => node.Name.LocalName == "polyline"));

        // The null rides on the chart, not only in the terminal. A chart travels away from the
        // run that made it, and a survivor count with no null beside it cannot be read.
        string drawn = document.ToString();
        Assert.Contains("6/1 (" + Presentation.Roughly(report.ExpectedAt(1).Value) + ")", drawn, StringComparison.Ordinal);
        Assert.Contains("at every precision", drawn, StringComparison.Ordinal);
    }

    [Fact]
    public void Chart_DrawsAnEmptySurvivorSetRatherThanFailingOnIt()
    {
        SurvivorReport report = SurvivorReport.Of([At(Ratio(63, 10), 1, 10)], 1, Cap);

        using var document = new StringWriter(CultureInfo.InvariantCulture);
        SurvivorChart.Write(document, report, new ChartCaption(
            "pi^2 / zeta(2)", "MachinPi, EulerMaclaurinZeta(2)", "DenominatorSweep", "1e-1", "Q = 1"));

        string drawn = document.ToString();

        Assert.NotNull(XDocument.Parse(drawn).Root);
        Assert.Contains("empty", drawn, StringComparison.Ordinal);
        Assert.Contains("refutation", drawn, StringComparison.Ordinal);
    }
}
