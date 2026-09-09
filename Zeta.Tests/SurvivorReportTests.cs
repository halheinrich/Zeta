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
        // SPEC-rational-ratio.md § 1's cancellation, pinned as the identity it is rather than as
        // a measurement: Q is derived as eps^(-1/2), so eps*Q^2 is 1 and 6/pi^2 is all that
        // survives. What § 1 draws from that about reading a count is its own and is not repeated
        // here. Three precisions fifteen decades apart, because an identity that held only near
        // the bench's own working range would be a coincidence.
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
    public void Estimate_CountsTheCandidatesOnePrefixAdmits()
    {
        // h*Q^2 + Q with h = 1/100 and Q = 100: a hundred from the interval widths and a hundred
        // from the one endpoint each denominator contributes.
        Assert.Equal(200, SurvivorRun.Estimate([At(BigRational.FromInteger(6), 1, 100)], 100));
    }

    [Fact]
    public void Estimate_PricesEveryPrefixAndNotOnlyTheWidest()
    {
        // The defect ruled on halheinrich/Math#64: SurvivorReport.Of enumerates each prefix
        // afresh, so the run pays the sum. Three prefixes of half-width 1/100, 1/200 and 1/400 at
        // Q = 100 cost 100 + 100, 50 + 100 and 25 + 100 - 475 against the 200 the widest alone
        // would have been priced at.
        IReadOnlyList<Approximation> enclosures =
        [
            At(BigRational.FromInteger(6), 1, 100),
            At(BigRational.FromInteger(6), 1, 200),
            At(BigRational.FromInteger(6), 1, 400),
        ];

        Assert.Equal(475, SurvivorRun.Estimate(enclosures, 100));
    }

    [Fact]
    public void Estimate_PricesEachPrefixAtItsNarrowestEnclosureAndNotItsLast()
    {
        // SurvivorSearch seeds its walk from the narrowest enclosure of the prefix it is given, so
        // that is what a prefix costs. In a run the enclosures only tighten and the narrowest is
        // the last; a list that widens again - which Distinct permits, since it collapses only
        // adjacent repeats - must not be priced as though the walk had got wider with it.
        IReadOnlyList<Approximation> tightens =
        [
            At(BigRational.FromInteger(6), 1, 100),
            At(BigRational.FromInteger(6), 1, 400),
        ];

        IReadOnlyList<Approximation> widensBack =
        [
            At(BigRational.FromInteger(6), 1, 100),
            At(BigRational.FromInteger(6), 1, 400),
            At(BigRational.FromInteger(6), 1, 100),
        ];

        Assert.Equal(325, SurvivorRun.Estimate(tightens, 100));
        Assert.Equal(325 + 25 + 100, SurvivorRun.Estimate(widensBack, 100));
    }

    [Fact]
    public void Estimate_CountsTheFloorAPrefixPaysForAdmittingNothing()
    {
        // The omitted term, and the fixture that makes it the whole cost: eight prefixes so narrow
        // that no denominator admits an integer still walk 1..Q apiece. The old estimate, pricing
        // the widest alone, called this 1,000 candidates; it is 8,000. At 1e-10 .. 1e-12 the real
        // schedule sits in exactly this regime.
        Approximation sliver = At(BigRational.FromInteger(6), 1, BigInteger.Pow(10, 9));
        IReadOnlyList<Approximation> eight = [.. Enumerable.Repeat(sliver, 8)];

        Assert.Equal(1_000, SurvivorRun.Estimate([sliver], 1_000));
        Assert.Equal(8_000, SurvivorRun.Estimate(eight, 1_000));
    }

    [Fact]
    public void Estimate_StaysExactWhereADoubleWouldNot()
    {
        // Q = 10^12, so Q squared is 10^24 - past the integers a double holds. A figure quoted in
        // a refusal must not be wrong in its leading digits.
        BigInteger bound = BigInteger.Pow(10, 12);

        Assert.Equal(
            BigInteger.Pow(10, 22) + bound,
            SurvivorRun.Estimate([At(BigRational.FromInteger(6), 1, 100)], bound));
    }

    [Fact]
    public void Estimate_RefusesNoEnclosuresAtAll() =>
        Assert.Throws<ArgumentException>(() => SurvivorRun.Estimate([], 100));

    // ---------- the budget, which is a predicted time and not a count ----------

    /// <summary>A price of one microsecond a candidate and nothing a denominator.</summary>
    /// <remarks>
    /// The two are separated so a test can move one and hold the other, which is the whole of what
    /// splitting the walk into two loops bought. Sample bounds of zero go with it: nothing here
    /// took a measurement, and a price carries where it was measured.
    /// </remarks>
    private static readonly WalkPrice CandidateMicrosecond =
        new(BigRational.Zero, new BigRational(1, 1_000_000), 0, 0);

    /// <summary>The same, with the microsecond charged to the denominator loop instead.</summary>
    private static readonly WalkPrice DenominatorMicrosecond =
        new(new BigRational(1, 1_000_000), BigRational.Zero, 0, 0);

    [Fact]
    public void Refuse_PassesARunItCanAffordAndStopsOneItCannot()
    {
        IReadOnlyList<Approximation> enclosures = [At(BigRational.FromInteger(6), 1, 100)];

        Assert.Null(SurvivorRun.Refuse(enclosures, 100, CandidateMicrosecond));
        Assert.NotNull(SurvivorRun.Refuse(enclosures, BigInteger.Pow(10, 9), CandidateMicrosecond));
    }

    [Fact]
    public void Refuse_PricesOneRunTwoWaysWhenTheCandidateCostsTwoDifferentThings()
    {
        // The whole of the change. Q = 100,000 and a half-width of 1e-2 give 100,000,000
        // candidates, which is 100 seconds at one microsecond apiece and 500 at five - the spread
        // ruling 2 measured between orders 3 and 10, at one schedule with only the order varying.
        // A budget counting candidates cannot tell these two runs apart, and one of them is a
        // five-minute wait while the other is more than twenty.
        IReadOnlyList<Approximation> enclosures = [At(BigRational.FromInteger(6), 1, 100)];
        WalkPrice dearer = CandidateMicrosecond with { PerCandidate = CandidateMicrosecond.PerCandidate * 5 };

        Assert.Equal(100_000_000, SurvivorRun.Size(enclosures, 100_000).Candidates);
        Assert.Null(SurvivorRun.Refuse(enclosures, 100_000, CandidateMicrosecond));
        Assert.NotNull(SurvivorRun.Refuse(enclosures, 100_000, dearer));
    }

    [Fact]
    public void Refuse_ChargesTheOuterLoopWhateverTheInnerOneAdmits()
    {
        // The floor the old estimate omitted, now priced in its own right. Forty prefixes too
        // narrow to admit a single candidate still step through 1..Q apiece: at Q = 40,000,000
        // that is 1.6 billion turns of the outer loop, and 1,600 seconds of them. A guard pricing
        // only what the intervals hold sees a run that costs nothing at all.
        Approximation sliver = At(BigRational.FromInteger(6), 1, BigInteger.Pow(10, 20));
        IReadOnlyList<Approximation> forty = [.. Enumerable.Repeat(sliver, 40)];

        Assert.Equal(BigInteger.Zero, SurvivorRun.Size(forty, 40_000_000).Candidates);
        Assert.Null(SurvivorRun.Refuse(forty, 40_000_000, CandidateMicrosecond));
        Assert.NotNull(SurvivorRun.Refuse(forty, 40_000_000, DenominatorMicrosecond));
    }

    [Fact]
    public void Refuse_BuysNothingAtAPriceOfZero()
    {
        // Calibrate returns zero prices for a sample with no work in it. Nothing is then predicted
        // to cost anything, which is right: the guard exists to stop a wait, and a measurement
        // that found no work found no wait either.
        Approximation sliver = At(BigRational.FromInteger(6), 1, BigInteger.Pow(10, 20));

        Assert.Null(SurvivorRun.Refuse(
            [sliver], BigInteger.Pow(10, 12), new WalkPrice(BigRational.Zero, BigRational.Zero, 0, 0)));
    }

    [Fact]
    public void Refuse_RefusesAPriceThatIsNotATime()
    {
        IReadOnlyList<Approximation> enclosures = [At(BigRational.FromInteger(6), 1, 100)];

        Assert.Throws<ArgumentOutOfRangeException>(() => SurvivorRun.Refuse(
            enclosures, 100, CandidateMicrosecond with { PerCandidate = -CandidateMicrosecond.PerCandidate }));
        Assert.Throws<ArgumentOutOfRangeException>(() => SurvivorRun.Refuse(
            enclosures, 100, DenominatorMicrosecond with { PerDenominator = -DenominatorMicrosecond.PerDenominator }));
    }

    [Fact]
    public void Refuse_SaysToShortenTheScheduleRatherThanLowerTheBound()
    {
        // The bound is derived on purpose. A refusal that invited lowering it would invite exactly
        // the hand-picked cap that made the exploration's second graph misleading.
        string? refusal = SurvivorRun.Refuse(
            [At(BigRational.FromInteger(6), 1, 100)], BigInteger.Pow(10, 9), CandidateMicrosecond);

        Assert.Contains("Shorten the schedule", refusal, StringComparison.Ordinal);
    }

    [Fact]
    public void Refuse_QuotesThePriceItWasHandedAndNotAConstant()
    {
        // A refusal reporting a count alone would read identically at every order, which is
        // exactly the transfer ruling 2 found does not hold.
        string? refusal = SurvivorRun.Refuse(
            [At(BigRational.FromInteger(6), 1, 100)],
            BigInteger.Pow(10, 9),
            CandidateMicrosecond with { PerCandidate = CandidateMicrosecond.PerCandidate * 7 });

        Assert.Contains("7.0 a candidate", refusal, StringComparison.Ordinal);
        Assert.Contains("budget of " + SurvivorRun.BudgetSeconds, refusal, StringComparison.Ordinal);
    }

    [Fact]
    public void Refuse_IsWhatADeeperScheduleNowRunsInto()
    {
        // The advice in that refusal became actionable when the schedule became an argument: a
        // caller can now shorten it. What follows is the two sides of the budget priced by hand,
        // at a widest enclosure of half-width 1e-2 - the default schedule's first target - and a
        // candidate costing the microsecond order 3 costs on this bench.
        //
        // The shipped last target of 1e-8 derives Q = 1e4, so the walk holds 1e-2 * 1e8 = 1e6
        // candidates, comfortably inside the budget. Four decades further derives Q = 1e6 and 1e10
        // of them, four orders past it. Nothing between them is asserted here: what the run
        // actually realises is tighter than what it was asked for, and only a run knows by how
        // much.
        IReadOnlyList<Approximation> enclosures = [At(BigRational.FromInteger(6), 1, 100)];

        BigInteger shipped = SurvivorReport.DerivedBound(At(BigRational.FromInteger(6), 1, BigInteger.Pow(10, 8)));
        BigInteger deeper = SurvivorReport.DerivedBound(At(BigRational.FromInteger(6), 1, BigInteger.Pow(10, 12)));

        Assert.Null(SurvivorRun.Refuse(enclosures, shipped, CandidateMicrosecond));
        Assert.NotNull(SurvivorRun.Refuse(enclosures, deeper, CandidateMicrosecond));
    }

    // ---------- the two loops, counted apart ----------

    [Fact]
    public void Size_CountsTheOuterLoopOncePerPrefixWhateverTheIntervalsHold()
    {
        // Q turns of the outer loop per prefix, exactly, and nothing about the half-widths enters
        // it. Three prefixes at Q = 100 is 300 turns whether they admit millions or none.
        Approximation wide = At(BigRational.FromInteger(6), 1, 100);
        Approximation sliver = At(BigRational.FromInteger(6), 1, BigInteger.Pow(10, 20));

        Assert.Equal(300, SurvivorRun.Size([wide, wide, wide], 100).Denominators);
        Assert.Equal(300, SurvivorRun.Size([sliver, sliver, sliver], 100).Denominators);
    }

    [Fact]
    public void Size_AddsUpToWhatTheReportsQuote() =>
        Assert.Equal(
            SurvivorRun.Estimate([At(BigRational.FromInteger(6), 1, 100)], 100),
            SurvivorRun.Size([At(BigRational.FromInteger(6), 1, 100)], 100).Total);

    // ---------- the calibration sample, whose size is decidable and whose timing is not ----------

    [Fact]
    public void Widest_FindsTheWidestEnclosureRatherThanTheFirst()
    {
        Approximation wide = At(BigRational.FromInteger(6), 1, 10);
        Approximation narrow = At(BigRational.FromInteger(6), 1, 1000);

        // Ordered as a run produces them, and then reversed - the answer must not move.
        Assert.Equal([wide], SurvivorRun.Widest([wide, narrow]));
        Assert.Equal([wide], SurvivorRun.Widest([narrow, wide]));
    }

    [Fact]
    public void Widest_RefusesNoEnclosuresAtAll() =>
        Assert.Throws<ArgumentException>(() => SurvivorRun.Widest([]));

    [Fact]
    public void SampleBound_DoublesUntilTheWidestWalkIsWorthTiming()
    {
        // Half-width 1e-2, so a walk of the widest enclosure alone to q holds q^2/100 candidates
        // and q denominators. At 512 that is 3,133 and short of the 4,000 the sample aims for; at
        // 1,024 it is 11,509 and past it. The overshoot is the doubling meeting a quadratic, and
        // is documented rather than tuned away.
        IReadOnlyList<Approximation> enclosures = [At(BigRational.FromInteger(6), 1, 100)];

        Assert.True(SurvivorRun.Size(enclosures, 512).Total < SurvivorRun.SampleCandidates);
        Assert.True(SurvivorRun.Size(enclosures, 1_024).Total >= SurvivorRun.SampleCandidates);
        Assert.Equal(1_024, SurvivorRun.SampleBound(enclosures, BigInteger.Pow(10, 9)));
    }

    [Fact]
    public void SampleBound_SizesItselfOnTheWidestEnclosureAndNotTheWholeList()
    {
        // The narrow prefixes carry the outer loop and none of the inner one, so counting them
        // would reach the target at a smaller bound and hand the solve two walks that barely
        // differ in what the intervals hold - which is the degeneracy the widest-alone sample
        // exists to avoid.
        Approximation wide = At(BigRational.FromInteger(6), 1, 100);
        Approximation sliver = At(BigRational.FromInteger(6), 1, BigInteger.Pow(10, 20));

        Assert.Equal(
            SurvivorRun.SampleBound([wide], BigInteger.Pow(10, 9)),
            SurvivorRun.SampleBound([wide, sliver, sliver, sliver], BigInteger.Pow(10, 9)));
    }

    [Fact]
    public void SampleBound_LeavesRoomForTheSecondWalkAndNeverPassesTheRun()
    {
        // A run smaller than the sample is sampled by being run, and the larger of the two sample
        // bounds must still fit inside it - so the doubling stops a factor of SampleSpread short
        // of Q rather than at Q.
        IReadOnlyList<Approximation> enclosures = [At(BigRational.FromInteger(6), 1, 100)];

        Assert.Equal(1_024, SurvivorRun.SampleBound(enclosures, 8_192));
        Assert.Equal(512, SurvivorRun.SampleBound(enclosures, 2_048));
        Assert.Equal(32, SurvivorRun.SampleBound(enclosures, 100));
        Assert.Equal(BigInteger.Zero, SurvivorRun.SampleBound(enclosures, 0));
    }

    [Fact]
    public void Calibrate_PricesNothingWhereThereIsNothingToWalk()
    {
        // The one branch of the calibration a test may assert. What the other branch returns is a
        // stopwatch reading, and ../AGENTS.md section Testing discipline keeps a test off the wall
        // clock - which is also why the guard is split the way it is: Size and SampleBound decide
        // what to measure, Refuse decides what to do with it, and all three are pure.
        WalkPrice free = SurvivorRun.Calibrate([At(BigRational.FromInteger(6), 1, 100)], 0);

        Assert.Equal(BigRational.Zero, free.PerDenominator);
        Assert.Equal(BigRational.Zero, free.PerCandidate);
    }

    // ---------- the schedule, which is an argument rather than a constant ----------

    [Fact]
    public void RefuseSchedule_AcceptsTheShippedDefault() =>
        Assert.Null(SurvivorRun.RefuseSchedule(SurvivorRun.DefaultFirstExponent, SurvivorRun.DefaultLastExponent));

    [Fact]
    public void RefuseSchedule_AcceptsASingleColumn()
    {
        // Deliberately unlike target, which refuses one because its whole output is a trend across
        // columns. This command's output is a survivor set, which one enclosure already produces -
        // and a run whose providers realise the same error twice collapses to one distinct
        // enclosure anyway, so nothing here is a new path.
        Assert.Null(SurvivorRun.RefuseSchedule(4, 4));
    }

    [Fact]
    public void RefuseSchedule_RefusesAScheduleThatLoosens()
    {
        // Refused on the argument so that TargetSchedule.Decades is never handed a range it would
        // reject with a message naming its own parameters instead of this experiment's.
        string? refusal = SurvivorRun.RefuseSchedule(8, 2);

        Assert.NotNull(refusal);
        Assert.Contains("loosens", refusal, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    public void RefuseSchedule_RefusesAnExponentThatNamesNoPrecision(int firstExponent)
    {
        // At zero the target is 1 and below it looser still, and Q = floor(eps^(-1/2)) and the
        // null 6*eps*q^2/pi^2 both read the exponent as a decimal place. TargetSchedule.Decades
        // accepts these and says so - a loose first column is a well-formed schedule. It is this
        // command's report that cannot carry one.
        Assert.NotNull(SurvivorRun.RefuseSchedule(firstExponent, SurvivorRun.DefaultLastExponent));
    }

    [Fact]
    public void RefuseSchedule_PutsNoCeilingOnDepth()
    {
        // There is no counterpart to target's MaxLastExponent, and that is the point: what a deep
        // schedule costs is priced off the enclosures a run realises, which this cannot see. A
        // schedule is refused for what it would cost, by Refuse, and never for being deep.
        Assert.Null(SurvivorRun.RefuseSchedule(2, 40));
    }

    // ---------- the arguments, read apart from anything they set off ----------

    [Fact]
    public void Interpret_DefaultsToTheOrderAndScheduleTheExhibitHasAlwaysRun()
    {
        // Literals, not the shipped constants, for the reason TargetRunGuardTests spells out about
        // its own: a test written against the constant moves with it, and this one exists to make
        // moving the default a deliberate act that reddens something.
        Assert.Null(SurvivorRun.Interpret([], out SurvivorRequest request));

        Assert.Equal(new SurvivorRequest(2, 2, 8), request);
        Assert.Equal(TargetSchedule.Decades(2, 8), request.Schedule());
    }

    [Fact]
    public void Interpret_TakesAnOrderAloneAndLeavesTheScheduleAtItsDefault()
    {
        Assert.Null(SurvivorRun.Interpret(["3"], out SurvivorRequest request));

        Assert.Equal(3, request.Order);
        Assert.Equal(TargetSchedule.Decades(2, 8), request.Schedule());
    }

    [Fact]
    public void Interpret_CarriesBothEndsThroughToTheScheduleBuilder()
    {
        // The whole change: an explicit pair reaches TargetSchedule.Decades unaltered, so the run
        // is driven to the targets that were asked for rather than to a constant pair.
        Assert.Null(SurvivorRun.Interpret(["3", "2", "12"], out SurvivorRequest request));

        Assert.Equal(new SurvivorRequest(3, 2, 12), request);
        Assert.Equal(TargetSchedule.Decades(2, 12), request.Schedule());
        Assert.NotEqual(TargetSchedule.Decades(2, 8), request.Schedule());
    }

    [Fact]
    public void Interpret_RefusesOneExponentRatherThanGuessWhichEndItNames()
    {
        // A lone exponent could name either end, and the two readings differ by ten decades of
        // cost per decade of disagreement. Taken as a pair or not at all.
        string? refusal = SurvivorRun.Interpret(["3", "12"], out SurvivorRequest request);

        Assert.NotNull(refusal);
        Assert.Contains("both ends", refusal, StringComparison.Ordinal);
        Assert.Equal(default(SurvivorRequest), request);
    }

    [Fact]
    public void Interpret_RefusesMoreArgumentsThanTheCommandHas() =>
        Assert.NotNull(SurvivorRun.Interpret(["3", "2", "12", "1"], out _));

    [Theory]
    [InlineData("three")]
    [InlineData("3.5")]
    [InlineData("")]
    public void Interpret_RefusesAnOrderThatIsNotAWholeNumber(string order)
    {
        string? refusal = SurvivorRun.Interpret([order], out _);

        Assert.NotNull(refusal);
        Assert.Contains("is not an order", refusal, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("two", "12")]
    [InlineData("2", "twelve")]
    [InlineData("2", "1e12")]
    public void Interpret_RefusesAnExponentThatIsNotAWholeNumber(string first, string last)
    {
        // The same treatment the order argument has always had, in the two new positions.
        string? refusal = SurvivorRun.Interpret(["3", first, last], out _);

        Assert.NotNull(refusal);
        Assert.Contains("is not an exponent", refusal, StringComparison.Ordinal);
    }

    [Fact]
    public void Interpret_ReportsTheFirstUnreadableArgumentAndNotTheLast()
    {
        // Both exponents are unreadable; the message names the one the caller would fix first.
        string? refusal = SurvivorRun.Interpret(["3", "two", "twelve"], out _);

        Assert.Contains("'two'", refusal, StringComparison.Ordinal);
        Assert.DoesNotContain("'twelve'", refusal, StringComparison.Ordinal);
    }

    [Fact]
    public void Interpret_PutsBothGuardsInFrontOfTheRun()
    {
        // Neither refusal is reachable only by starting a run - which is the whole reason the
        // reading of the arguments was split from the acting on them.
        Assert.Equal(SurvivorRun.RefuseOrder(1), SurvivorRun.Interpret(["1"], out _));
        Assert.Equal(SurvivorRun.RefuseSchedule(9, 3), SurvivorRun.Interpret(["3", "9", "3"], out _));
    }

    // ---------- what the reports say the run was ----------

    [Fact]
    public void ScheduleLabel_NamesBothEndsOfWhatWasAskedFor() =>
        Assert.Equal("1e-3 .. 1e-11", new SurvivorRequest(3, 3, 11).ScheduleLabel);

    [Fact]
    public void Preamble_ReportsTheScheduleThatRanAndNotTheDefault()
    {
        // The line named two constants while the span was fixed. Now that it is an argument, a
        // preamble still reading the default would describe a run that did not happen - and the
        // SVG's caption carries the same label from the same property, so they cannot disagree.
        using var notes = new StringWriter(CultureInfo.InvariantCulture);

        SurvivorRun.Preamble(notes, new SurvivorRequest(3, 3, 11), 9);

        string written = notes.ToString();

        Assert.Contains("schedule    1e-3 .. 1e-11, 9 targets", written, StringComparison.Ordinal);
        Assert.DoesNotContain("1e-2 .. 1e-8", written, StringComparison.Ordinal);
        Assert.Contains("pi^3 / zeta(3)", written, StringComparison.Ordinal);
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
