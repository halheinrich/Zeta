using System.Numerics;

namespace HalHeinrich.Numerics.Experiments;

/// <summary>
/// What <c>../SPEC-rational-ratio.md</c> § 2 step 6 says a run reports: the rationals of bounded
/// denominator that no enclosure excludes, the count still standing after each enclosure, and the
/// handful whose fate is worth drawing.
/// </summary>
/// <remarks>
/// <para>
/// <b>A pure function of a list of enclosures, which is what makes any of this testable.</b> The
/// run that produces those enclosures depends on wall-clock time and on providers whose answers
/// nobody knows, so nothing about it has a pass or a fail. An intersection over three enclosures a
/// test hands over by hand does, and it is the same code path the real run takes.
/// </para>
/// <para>
/// <b>The survivor set is an intersection across every enclosure, never a filter on the last
/// one</b>, because the enclosures do not nest: a candidate refuted by an early enclosure can sit
/// inside a later one, the two being centred on different values. <see cref="SurvivorSearch"/>
/// owns that argument and this type only passes the whole prefix to it.
/// </para>
/// <para>
/// <b>The denominator bound's axis is this search's, not the run's searcher's.</b>
/// <see cref="SurvivorSearch"/> takes a largest denominator, so what comes back is a denominator
/// claim whatever <see cref="RatioRun.Execute"/> was given to sweep with. The two agree in this
/// command because it runs <see cref="DenominatorSweep"/> and derives the bound from that
/// searcher's own generic depth - by construction rather than by luck, which is the distinction
/// § 1 was amended to keep visible.
/// </para>
/// </remarks>
internal sealed class SurvivorReport
{
    /// <summary>How many survivors are held for reporting, however many there are.</summary>
    /// <remarks>
    /// A survivor set is usually a handful and a refutation is empty, so this cap is not expected
    /// to bite. It exists because <see cref="SurvivorSearch"/> deliberately never materialises its
    /// candidate space, and a report that undid that by collecting everything would reintroduce
    /// the memory exhaustion that design avoids. <see cref="SurvivorCount"/> is the true figure
    /// either way.
    /// </remarks>
    public const int SurvivorsShown = 32;

    private const string ExactEnclosureMessage =
        "An exact enclosure has no generic sweep depth to derive a bound from. A sweep against an " +
        "exact value halts at that value's own denominator rather than at eps^(-1/2), so the " +
        "derivation below would divide by a zero error. No provider in this bench is exact, so " +
        "this is a guard rather than a case.";

    private readonly Approximation[] enclosures;
    private readonly long[] counts;
    private readonly BigRational[] survivors;
    private readonly BigRational[] tracked;

    private SurvivorReport(
        Approximation[] enclosures,
        long[] counts,
        BigRational[] survivors,
        BigRational[] tracked,
        BigInteger denominatorBound)
    {
        this.enclosures = enclosures;
        this.counts = counts;
        this.survivors = survivors;
        this.tracked = tracked;
        DenominatorBound = denominatorBound;
    }

    /// <summary>Gets the enclosures the intersection ran over, in order.</summary>
    public IReadOnlyList<Approximation> Enclosures => enclosures;

    /// <summary>Gets how many rationals were still standing after each enclosure, in order.</summary>
    /// <remarks>
    /// <para>
    /// Nonincreasing by construction: each entry counts the survivors of one more enclosure than
    /// the last, and an intersection only shrinks. The first entry is the count under a single
    /// enclosure and is the largest number this report holds - it is what the collapse is read
    /// against.
    /// </para>
    /// <para>
    /// A <see cref="long"/> rather than a <see cref="BigInteger"/>, and the reason is not that the
    /// count is small. It is that every entry is produced by counting an enumeration one candidate
    /// at a time, so a count that could overflow a <see cref="long"/> is a run that could not
    /// finish in any number of human lifetimes. The enumeration is the limit here, not the counter.
    /// </para>
    /// </remarks>
    public IReadOnlyList<long> Counts => counts;

    /// <summary>Gets the survivors of every enclosure, simplest denominator first, up to <see cref="SurvivorsShown"/>.</summary>
    /// <remarks>
    /// Empty is a refutation and is the strongest outcome available: no rational of denominator at
    /// or below <see cref="DenominatorBound"/> is consistent with the evidence. A short list poses
    /// a conjecture and establishes nothing.
    /// </remarks>
    public IReadOnlyList<BigRational> Survivors => survivors;

    /// <summary>Gets how many survivors there are, which <see cref="Survivors"/> may not list in full.</summary>
    public long SurvivorCount => counts.Length == 0 ? 0 : counts[^1];

    /// <summary>Gets the candidates whose distances are worth plotting: the survivors, and the nearest excluded.</summary>
    /// <remarks>
    /// <para>
    /// <b>Chosen by distance from the final ratio, not by the stage they died at</b>, and the
    /// difference is not cosmetic. A stage-based rule looks reasonable and fails on the runs worth
    /// drawing: the zeta(2) collapse goes 637,397 then 16,215 then 1, so no stage ever holds a
    /// small set that is bigger than the answer, and a chart built that way has one line on it and
    /// nothing to compare.
    /// </para>
    /// <para>
    /// Distance is the right axis because it is what decides survival. A candidate at distance
    /// <c>d</c> outlives every enclosure whose half-width exceeds <c>d</c>, so the nearest
    /// candidates are exactly the ones refuted last - which is what a chart of distance against
    /// half-width exists to show, and what "the nearest excluded" means.
    /// </para>
    /// <para>
    /// The survivors go first so a legend reads answer-first, then the nearest that are not
    /// already there. Both come from one pass each: the survivors from the last enclosure's walk,
    /// the nearest from the first's.
    /// </para>
    /// </remarks>
    public IReadOnlyList<BigRational> Tracked => tracked;

    /// <summary>Gets the largest denominator considered.</summary>
    public BigInteger DenominatorBound { get; }

    /// <summary>The enclosures of the unknown a run produced: one per iteration, in order.</summary>
    /// <param name="run">The completed run.</param>
    /// <returns>The ratio enclosure of each iteration.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="run"/> is null.</exception>
    /// <remarks>
    /// The projection <see cref="RatioRun"/>'s own remarks name: <see cref="SurvivorSearch"/> takes
    /// enclosures of the unknown, and a ratio run's are the <see cref="RatioEnclosure.Ratio"/> of
    /// each <see cref="RatioIteration.Enclosure"/>. It is one line and it is here rather than
    /// inline because getting it wrong - handing over the divisor's enclosure, say - would produce
    /// a survivor set that is entirely well-formed and about the wrong number.
    /// </remarks>
    public static IReadOnlyList<Approximation> EnclosuresOf(RatioRun run)
    {
        ArgumentNullException.ThrowIfNull(run);

        return [.. run.Iterations.Select(iteration => iteration.Enclosure.Ratio)];
    }

    /// <summary>The same enclosures with adjacent repeats collapsed.</summary>
    /// <param name="enclosures">The enclosures, in order.</param>
    /// <returns>The distinct ones, in order.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="enclosures"/> is null.</exception>
    /// <remarks>
    /// <para>
    /// A schedule can ask for a target the realised bound has already passed, in which case the
    /// refiner correctly does nothing and two columns carry the same enclosure. Intersecting an
    /// enclosure with itself refutes nothing, so a repeated column costs a full enumeration to
    /// produce a flat step that reads as evidence of stalling.
    /// </para>
    /// <para>
    /// <b>Which basis a count is quoted on is part of the count.</b> The zeta(2) walk prints three
    /// readings of one run for exactly this reason - per target, per distinct enclosure, and over a
    /// sub-schedule - because a sequence quoted without its basis cannot be checked. This command
    /// plots per distinct enclosure and says so on the chart.
    /// </para>
    /// </remarks>
    public static IReadOnlyList<Approximation> Distinct(IReadOnlyList<Approximation> enclosures)
    {
        ArgumentNullException.ThrowIfNull(enclosures);

        var kept = new List<Approximation>();

        for (int index = 0; index < enclosures.Count; index++)
        {
            if (index == 0 || enclosures[index] != enclosures[index - 1])
            {
                kept.Add(enclosures[index]);
            }
        }

        return kept;
    }

    /// <summary>The denominator bound this run earns: the depth a generic sweep would have reached.</summary>
    /// <param name="enclosure">The narrowest enclosure, whose error sets the depth.</param>
    /// <returns><c>floor(eps^(-1/2))</c>, where <c>eps</c> is that enclosure's error.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="enclosure"/> is exact.</exception>
    /// <remarks>
    /// <para>
    /// <b>Derived rather than picked, and that is the whole point of the parameter.</b> § 2 sizes a
    /// generic sweep at about <c>eps^(-1/2)</c> denominators, so a bound at that depth is the one
    /// the run would have reached anyway had it been asked for a rational rather than for a
    /// refutation. A cap chosen by hand is what made the exploration's second graph misleading: it
    /// decides how impressive the collapse looks, and nothing checks it.
    /// </para>
    /// <para>
    /// The derivation presumes the denominator axis, since <c>eps^(-1/2)</c> is
    /// <see cref="DenominatorSweep"/>'s generic depth rather than <see cref="HeightSweep"/>'s. So a
    /// command deriving its bound this way must sweep denominators, which is why the one here does
    /// not take a searcher.
    /// </para>
    /// <para>
    /// Exact in integers, with no square root of a <see cref="double"/> anywhere. Writing
    /// <c>eps</c> as <c>n/d</c> in lowest terms, <c>eps^(-1/2)</c> is <c>sqrt(d/n)</c>, which is
    /// <c>sqrt(d*n)/n</c>; and <c>floor(floor(x)/m) = floor(x/m)</c> for a positive integer
    /// <c>m</c>, so one integer square root and one integer division give the exact floor with no
    /// adjustment step to get wrong.
    /// </para>
    /// </remarks>
    public static BigInteger DerivedBound(Approximation enclosure)
    {
        if (enclosure.IsExact)
        {
            throw new ArgumentOutOfRangeException(nameof(enclosure), ExactEnclosureMessage);
        }

        BigInteger numerator = enclosure.MaxError.Numerator;
        BigInteger denominator = enclosure.MaxError.Denominator;

        return IntegerMath.Sqrt(denominator * numerator) / numerator;
    }

    /// <summary>Intersects the enclosures one at a time, counting what is left after each.</summary>
    /// <param name="enclosures">The enclosures, in order. At least one.</param>
    /// <param name="denominatorBound">The largest denominator to consider.</param>
    /// <param name="trackedCap">How many candidates the distance chart may follow. At least one.</param>
    /// <param name="afterEach">
    /// Called with each enclosure's index and the count still standing after it, so a caller can
    /// report progress on a walk whose first step is much the most expensive. May be null.
    /// </param>
    /// <returns>The report.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="enclosures"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="enclosures"/> is empty.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="denominatorBound"/> is negative, or <paramref name="trackedCap"/> is below one.
    /// </exception>
    /// <remarks>
    /// <b>Each prefix is enumerated afresh rather than filtered from the last.</b> The obvious
    /// alternative - materialise the first enclosure's survivors and whittle them down - holds the
    /// largest set this report ever sees, which for a derived bound runs to millions.
    /// <see cref="SurvivorSearch"/> seeds each walk with the narrowest enclosure of the prefix it
    /// is given, so every step after the first is far cheaper than the first and the whole loop
    /// costs about twice what its opening step does.
    /// </remarks>
    public static SurvivorReport Of(
        IReadOnlyList<Approximation> enclosures,
        BigInteger denominatorBound,
        int trackedCap,
        Action<int, long>? afterEach = null)
    {
        ArgumentNullException.ThrowIfNull(enclosures);
        ArgumentOutOfRangeException.ThrowIfLessThan(trackedCap, 1);

        if (enclosures.Count == 0)
        {
            throw new ArgumentException(
                "A survivor report needs at least one enclosure to intersect.", nameof(enclosures));
        }

        var counts = new long[enclosures.Count];
        var nearest = new List<(BigRational Candidate, BigRational Distance)>();
        var standing = new List<BigRational>();
        BigRational centre = enclosures[^1].Value;

        for (int index = 0; index < enclosures.Count; index++)
        {
            bool widest = index == 0;
            bool narrowest = index == enclosures.Count - 1;
            long count = 0;

            standing.Clear();

            foreach (BigRational survivor in SurvivorSearch.Survivors(
                enclosures.Take(index + 1), denominatorBound))
            {
                count++;

                if (widest)
                {
                    Offer(nearest, survivor, centre, trackedCap);
                }

                if (narrowest && standing.Count < SurvivorsShown)
                {
                    standing.Add(survivor);
                }
            }

            counts[index] = count;
            afterEach?.Invoke(index, count);
        }

        return new SurvivorReport(
            [.. enclosures],
            counts,
            [.. standing],
            Follow(standing, nearest, trackedCap),
            denominatorBound);
    }

    /// <summary>The survivors first, then the nearest candidates not already among them.</summary>
    private static BigRational[] Follow(
        List<BigRational> standing,
        List<(BigRational Candidate, BigRational Distance)> nearest,
        int cap)
    {
        var followed = new List<BigRational>(standing.Take(cap));

        foreach ((BigRational candidate, _) in nearest)
        {
            if (followed.Count >= cap)
            {
                break;
            }

            if (!followed.Contains(candidate))
            {
                followed.Add(candidate);
            }
        }

        return [.. followed];
    }

    /// <summary>Keeps a candidate if it is among the <paramref name="cap"/> nearest seen so far.</summary>
    /// <remarks>
    /// <para>
    /// An insertion sort over a list that never grows past the cap, which is six in this command.
    /// The distance is carried alongside rather than recomputed on every comparison: this runs
    /// once per candidate under the widest enclosure, which is hundreds of thousands of times, and
    /// a rational subtraction per comparison would make the selection cost more than the search it
    /// is watching.
    /// </para>
    /// <para>
    /// The comparison is exact rational arithmetic. Comparing decimal distances instead is the one
    /// place a floating-point shortcut here could silently pick the wrong candidate to draw - two
    /// near-misses can agree to every digit a <see cref="double"/> holds and still be ordered.
    /// </para>
    /// </remarks>
    private static void Offer(
        List<(BigRational Candidate, BigRational Distance)> nearest,
        BigRational candidate,
        BigRational centre,
        int cap)
    {
        BigRational distance = BigRational.Abs(candidate - centre);

        if (nearest.Count == cap && distance >= nearest[^1].Distance)
        {
            return;
        }

        int at = 0;
        while (at < nearest.Count && nearest[at].Distance <= distance)
        {
            at++;
        }

        nearest.Insert(at, (candidate, distance));

        if (nearest.Count > cap)
        {
            nearest.RemoveAt(nearest.Count - 1);
        }
    }
}
