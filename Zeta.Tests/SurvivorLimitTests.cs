using System.Numerics;
using HalHeinrich.Numerics.Experiments;

namespace HalHeinrich.Numerics.Tests;

/// <summary>
/// The survivor limit checked during the walk: a report handed a walk that yields past its limit
/// stops at the first survivor over it and returns a refusal instead of a report.
/// </summary>
/// <remarks>
/// <para>
/// <b>No timing, and no real walk.</b> Each test hands the report a delegate that yields as many
/// survivors as the case needs and counts how many were pulled from it, which is what makes "stops
/// at the first survivor past the limit" a count rather than an observation - the same seam the
/// deep walk's call-count tests use. <c>halheinrich/Math#79</c>'s leg 2 ruling asks for exactly
/// this: the during-walk refusal is held by handing the report a delegate yielding past the limit.
/// </para>
/// <para>
/// What the values yielded are does not matter to the limit, so they are the integers, which every
/// enclosure below contains; the enclosure only has to exist for the report's argument checks.
/// </para>
/// </remarks>
public sealed class SurvivorLimitTests
{
    private const int Cap = 4;

    private static readonly Approximation[] One =
        [Approximation.Create(BigRational.FromInteger(6), BigRational.FromInteger(1000))];

    private static readonly Approximation[] Three =
    [
        Approximation.Create(BigRational.FromInteger(6), BigRational.FromInteger(1000)),
        Approximation.Create(BigRational.FromInteger(6), BigRational.FromInteger(100)),
        Approximation.Create(BigRational.FromInteger(6), BigRational.FromInteger(10)),
    ];

    // ---------- the limit itself ----------

    [Fact]
    public void None_IsTheDefaultAndIsPassedByNothing()
    {
        Assert.Equal(SurvivorLimit.None, default);
        Assert.Null(SurvivorLimit.None.Count);
        Assert.False(SurvivorLimit.None.IsPassedBy(long.MaxValue));
    }

    [Fact]
    public void At_IsReachedExactlyWithoutBeingPassed()
    {
        SurvivorLimit limit = SurvivorLimit.At(5);

        Assert.Equal(5, limit.Count);
        Assert.False(limit.IsPassedBy(5));
        Assert.True(limit.IsPassedBy(6));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void At_RefusesACountBelowOneRatherThanReadingItAsNone(long survivors)
    {
        // Zero would refuse every run holding a survivor, which is every control; None already
        // spells the other reading, so zero is refused rather than guessed at.
        Assert.Throws<ArgumentOutOfRangeException>(() => SurvivorLimit.At(survivors));
    }

    [Fact]
    public void DuringTheWalk_RefusesACountThatHasNotPassed()
    {
        Assert.Throws<ArgumentException>(
            () => SurvivorCountRefusal.DuringTheWalk(5, SurvivorLimit.At(5), 10, 1, null, SurvivorMode.Deep));
        Assert.Throws<ArgumentException>(
            () => SurvivorCountRefusal.DuringTheWalk(6, SurvivorLimit.None, 10, 1, null, SurvivorMode.Deep));
    }

    // ---------- a deep walk ----------

    [Fact]
    public void Deep_StopsAtTheFirstSurvivorPastTheLimitAndReturnsNoReport()
    {
        var walk = new Yielding(100);

        SurvivorOutcome outcome = SurvivorReport.Deep(One, new SurvivorBound(10, null), Cap, walk.Walk, SurvivorLimit.At(5));

        SurvivorCountRefusal refusal = Assert.IsType<SurvivorCountRefusal>(outcome.Refusal);
        Assert.Equal(SurvivorCountBasis.Found, refusal.Basis);
        Assert.Equal(BigRational.FromInteger(6), refusal.Count);
        Assert.Equal(5, refusal.Limit);
        Assert.Equal(10, refusal.DenominatorBound);
        Assert.Equal(1, refusal.Prefixes);
        Assert.Null(refusal.PassedAt);
        Assert.Equal(SurvivorMode.Deep, refusal.Mode);

        // The moment it passes: one past the limit pulled, and not one more.
        Assert.Equal(6, walk.Pulled);

        // A refused run has no report, and reading one fails loudly rather than returning a
        // report of a walk that stopped part-way.
        Assert.Throws<InvalidOperationException>(() => outcome.Report);
    }

    [Fact]
    public void Deep_CompletesAWalkThatReachesTheLimitExactly()
    {
        SurvivorOutcome outcome = SurvivorReport.Deep(
            One, new SurvivorBound(10, null), Cap, new Yielding(5).Walk, SurvivorLimit.At(5));

        Assert.Null(outcome.Refusal);
        Assert.Equal(5, outcome.Report.SurvivorCount);
    }

    // ---------- a chart walk: the limit is on the total across its prefixes ----------

    [Fact]
    public void Of_CountsTheLimitAcrossEveryPrefixAndStopsInThePrefixThatPassesIt()
    {
        // Three survivors a prefix against a limit of seven: 3, then 6, then the second survivor of
        // the third prefix is the eighth. No single prefix comes near the limit, so a per-walk
        // count would never refuse; what the run spends is the total, and that is what is limited.
        var walk = new Yielding(3);
        var reported = new List<int>();

        SurvivorOutcome outcome = SurvivorReport.Of(
            Three, 10, Cap, walk.Walk, SurvivorLimit.At(7), (index, _) => reported.Add(index));

        SurvivorCountRefusal refusal = Assert.IsType<SurvivorCountRefusal>(outcome.Refusal);
        Assert.Equal(SurvivorCountBasis.Found, refusal.Basis);
        Assert.Equal(BigRational.FromInteger(8), refusal.Count);
        Assert.Equal(2, refusal.PassedAt);
        Assert.Equal(3, refusal.Prefixes);
        Assert.Equal(SurvivorMode.Chart, refusal.Mode);
        Assert.Equal(8, walk.Pulled);

        // The prefix that passed reports no progress, since it did not finish.
        Assert.Equal([0, 1], reported);
        Assert.Throws<InvalidOperationException>(() => outcome.Report);
    }

    [Fact]
    public void Of_CompletesUnderNoLimitHoweverManySurvivors()
    {
        SurvivorOutcome outcome = SurvivorReport.Of(Three, 10, Cap, new Yielding(1000).Walk, SurvivorLimit.None);

        Assert.Null(outcome.Refusal);
        Assert.Equal([1000L, 1000L, 1000L], outcome.Report.Counts);
    }

    // ---------- the outcome ----------

    [Fact]
    public void Outcome_RefusesToBeBuiltFromNothing()
    {
        Assert.Throws<ArgumentNullException>(() => SurvivorOutcome.Completed(null!));
        Assert.Throws<ArgumentNullException>(() => SurvivorOutcome.Refused(null!));
    }

    /// <summary>A walk that yields the integers 1..n on every call and counts how many were pulled in all.</summary>
    private sealed class Yielding(int perCall)
    {
        public long Pulled { get; private set; }

        public IEnumerable<BigRational> Walk(IEnumerable<Approximation> enclosures, BigInteger denominatorBound)
        {
            for (int value = 1; value <= perCall; value++)
            {
                Pulled++;
                yield return BigRational.FromInteger(value);
            }
        }
    }
}
