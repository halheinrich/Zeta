using System.Numerics;

namespace HalHeinrich.Numerics.Experiments;

/// <summary>
/// How many survivors a walk may produce before the run is refused, or no limit at all.
/// </summary>
/// <remarks>
/// <para>
/// <b>A count, not a time, and only for the walk whose cost is its survivors.</b> Ruled on
/// <c>halheinrich/Math#79</c> leg 2: <see cref="FareyWalk"/> costs about <c>log Q</c> plus one
/// step a survivor, so what a run of it spends is set by how many survivors it finds, and a limit
/// on that count bounds the run without a stopwatch. <see cref="DenominatorWalk"/> costs <c>Q</c>
/// denominators whatever it finds, so a count says nothing about its price; it keeps the
/// calibrated time budget and walks under <see cref="None"/>.
/// </para>
/// <para>
/// <b>A value with a meaningful default.</b> <see langword="default"/> is <see cref="None"/>, which
/// is a real state - the reference walk's - rather than a limit of zero, which no run could
/// satisfy and no caller could want. That is why this is a struct where
/// <see cref="SurvivorCountRefusal"/> is a class.
/// </para>
/// </remarks>
internal readonly record struct SurvivorLimit
{
    private readonly long count;

    private SurvivorLimit(long count) => this.count = count;

    /// <summary>Gets no limit: the walk runs to completion however many survivors it finds.</summary>
    public static SurvivorLimit None => default;

    /// <summary>Gets the largest number of survivors a walk may produce, or null for <see cref="None"/>.</summary>
    public long? Count => count == 0 ? null : count;

    /// <summary>A limit of exactly this many survivors.</summary>
    /// <param name="survivors">The largest count a walk may reach. At least one.</param>
    /// <returns>The limit.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="survivors"/> is below one.</exception>
    /// <remarks>
    /// Zero is refused rather than read as "none": a limit of zero would refuse every run with a
    /// survivor in it, which is every control, and <see cref="None"/> already spells the other
    /// reading.
    /// </remarks>
    public static SurvivorLimit At(long survivors)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(survivors, 1);

        return new SurvivorLimit(survivors);
    }

    /// <summary>Whether this many survivors has passed the limit.</summary>
    /// <param name="survivors">How many a walk has produced so far.</param>
    /// <returns>True when there is a limit and the count is above it; reaching it exactly is allowed.</returns>
    public bool IsPassedBy(long survivors) => count != 0 && survivors > count;
}

/// <summary>When a survivor-count refusal was decided: on the expectation, or on the walk itself.</summary>
internal enum SurvivorCountBasis
{
    /// <summary>Before the walk, on the expected count <c>6*h*Q^2/pi^2</c> summed over what would be walked.</summary>
    Expected,

    /// <summary>During the walk, the moment the count actually produced passed the limit.</summary>
    Found,
}

/// <summary>
/// A <see cref="FareyWalk"/> run refused for its survivor count: what was counted, against which
/// limit, and where.
/// </summary>
/// <remarks>
/// <para>
/// <b>One limit checked at two moments</b>, ruled on <c>halheinrich/Math#79</c> leg 2. Before the
/// walk the expectation is checked, which refuses most of what would pass before anything is
/// spent; during it the count itself is, because an expectation is not a bound. Both are the same
/// refusal of the same policy, so they are one type told apart by <see cref="Basis"/>.
/// </para>
/// <para>
/// <b>A value, like every refusal this runner makes.</b> A refusal is an expected outcome of the
/// count policy, with the same shape as <see cref="SurvivorRefusal"/> and the rest: a value the run
/// renders before exiting 2. Exceptions stay for defects - <see cref="FareyWalk"/> throws
/// <see cref="System.Diagnostics.UnreachableException"/> when its own invariants break - and a run
/// refused by policy is not one.
/// </para>
/// <para>
/// A sealed class rather than a struct, because no refusal is a coherent default: an all-zero one
/// would claim a run refused at a limit of zero.
/// </para>
/// </remarks>
internal sealed record SurvivorCountRefusal
{
    private SurvivorCountRefusal(
        SurvivorCountBasis basis,
        BigRational count,
        long limit,
        BigInteger denominatorBound,
        int prefixes,
        int? passedAt,
        SurvivorMode mode)
    {
        Basis = basis;
        Count = count;
        Limit = limit;
        DenominatorBound = denominatorBound;
        Prefixes = prefixes;
        PassedAt = passedAt;
        Mode = mode;
    }

    /// <summary>Gets whether the expectation or the walk itself passed the limit.</summary>
    public SurvivorCountBasis Basis { get; }

    /// <summary>
    /// Gets what was counted: the upper end of the expected count's enclosure, or the survivors
    /// actually produced - which is one more than the limit, since the walk stops at the first
    /// survivor past it.
    /// </summary>
    public BigRational Count { get; }

    /// <summary>Gets the limit that was passed.</summary>
    public long Limit { get; }

    /// <summary>Gets the denominator bound the walk ran, or would have run, to.</summary>
    public BigInteger DenominatorBound { get; }

    /// <summary>Gets how many walks the run makes: one per prefix for a chart, one for a deep run.</summary>
    public int Prefixes { get; }

    /// <summary>
    /// Gets the index of the chart prefix whose walk passed the limit, or null - before any walk, or
    /// for a deep run, which has one walk and no prefixes.
    /// </summary>
    public int? PassedAt { get; }

    /// <summary>Gets which walk the run was: one per prefix, or one over everything.</summary>
    public SurvivorMode Mode { get; }

    /// <summary>A refusal decided before the walk, on the expected count.</summary>
    /// <param name="expected">The upper end of the expected count, summed over what would be walked.</param>
    /// <param name="limit">The limit it passed.</param>
    /// <param name="denominatorBound">The bound the walk would have run to.</param>
    /// <param name="prefixes">How many walks the run would have made.</param>
    /// <param name="mode">The run's mode.</param>
    /// <returns>The refusal.</returns>
    public static SurvivorCountRefusal BeforeTheWalk(
        BigRational expected, long limit, BigInteger denominatorBound, int prefixes, SurvivorMode mode) =>
        new(SurvivorCountBasis.Expected, expected, limit, denominatorBound, prefixes, null, mode);

    /// <summary>A refusal decided during the walk, the moment the survivors produced passed the limit.</summary>
    /// <param name="found">The survivors produced, summed over every walk so far - one past the limit.</param>
    /// <param name="limit">The limit it passed.</param>
    /// <param name="denominatorBound">The bound the walk was running to.</param>
    /// <param name="prefixes">How many walks the run would have made.</param>
    /// <param name="passedAt">The chart prefix whose walk passed it; null for a deep run.</param>
    /// <param name="mode">The run's mode.</param>
    /// <returns>The refusal.</returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="found"/> has not passed <paramref name="limit"/> - which includes
    /// <see cref="SurvivorLimit.None"/>, since nothing passes that.
    /// </exception>
    public static SurvivorCountRefusal DuringTheWalk(
        long found, SurvivorLimit limit, BigInteger denominatorBound, int prefixes, int? passedAt, SurvivorMode mode)
    {
        if (!limit.IsPassedBy(found) || limit.Count is not { } most)
        {
            throw new ArgumentException(NotPassedMessage, nameof(found));
        }

        return new(SurvivorCountBasis.Found, BigRational.FromInteger(found), most, denominatorBound, prefixes, passedAt, mode);
    }

    private const string NotPassedMessage =
        "A walk is refused for its count only once the count has passed the limit. Reaching the limit " +
        "exactly is allowed, and no count passes the absence of one.";
}
