using System.Numerics;

namespace HalHeinrich.Numerics.Experiments;

/// <summary>A part of the survivor picture that only some walks can produce.</summary>
/// <remarks>
/// The survivor set itself is not here, because every walk produces it. What is here is what a
/// report can <i>lack</i>, so that a run which does not draw something says so as a value rather
/// than shipping a thinner picture and leaving the reader to notice.
/// </remarks>
internal enum SurvivorPanel
{
    /// <summary>
    /// The count still standing after each enclosure. It needs one walk per prefix, which is
    /// exactly what a deep run does not do.
    /// </summary>
    Collapse,

    /// <summary>
    /// The candidates nearest the final ratio that an enclosure refuted, drawn against the
    /// survivors so there is something to contrast them with. They come from walking the widest
    /// enclosure alone, which is the walk a deep run exists to skip.
    /// </summary>
    NearestExcluded,
}

/// <summary>Enumerates the rationals of bounded denominator that a set of enclosures leaves standing.</summary>
/// <param name="enclosures">The enclosures, every one of which a survivor must satisfy.</param>
/// <param name="denominatorBound">The largest denominator to consider.</param>
/// <returns>The survivors.</returns>
/// <remarks>
/// <see cref="SurvivorSearch.Survivors"/>'s shape, named so a report can be handed a walk to call.
/// Production always hands the real one, by leaving the argument out; a test hands one that counts
/// its calls, which is what makes "a deep run walks once" a count rather than a timing - the same
/// seam <see cref="RatioRun.Execute"/>'s optional searcher gives <see cref="NoSearch"/>'s tests.
/// </remarks>
internal delegate IEnumerable<BigRational> SurvivorWalk(
    IEnumerable<Approximation> enclosures, BigInteger denominatorBound);

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
/// command because the bound is derived from <see cref="DenominatorSweep"/>'s generic depth - by
/// construction rather than by luck, which is the distinction § 1 was amended to keep visible.
/// That derivation is § 2's sizing law for that searcher and not a report of a sweep: the survivor
/// command runs none, having no trend matrix to fill.
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

    /// <summary>How tightly pi is pinned before it is squared for the null.</summary>
    /// <remarks>
    /// Far tighter than the two figures anything prints, and cheap: the estimate it divides is a
    /// statistical one whose modelling error is enormous beside this, so what this buys is not
    /// accuracy but the absence of a hidden constant. The alternative was writing <c>6/pi^2</c> in
    /// as a literal, which is the thing <c>RealConstants</c> exists to stop.
    /// </remarks>
    private static readonly BigRational PiTolerance = new(BigInteger.One, BigInteger.Pow(10, 30));

    /// <summary>Pi squared, enclosed, so the null below is an interval and not a decimal.</summary>
    private static readonly Approximation PiSquared = SquarePi();

    private const string ExactEnclosureMessage =
        "An exact enclosure has no generic sweep depth to derive a bound from. A sweep against an " +
        "exact value halts at that value's own denominator rather than at eps^(-1/2), so the " +
        "derivation below would divide by a zero error. No provider in this bench is exact, so " +
        "this is a guard rather than a case.";

    private const string NoEnclosuresMessage =
        "A survivor report needs at least one enclosure to intersect.";

    /// <summary>What a deep walk cannot produce, in the order the chart would have drawn it.</summary>
    private static readonly SurvivorPanel[] DeepOmits = [SurvivorPanel.Collapse, SurvivorPanel.NearestExcluded];

    private readonly Approximation[] enclosures;
    private readonly long[] counts;
    private readonly BigRational[] survivors;
    private readonly BigRational[] tracked;
    private readonly SurvivorPanel[] omitted;

    private SurvivorReport(
        Approximation[] enclosures,
        long[] counts,
        long survivorCount,
        BigRational[] survivors,
        BigRational[] tracked,
        SurvivorBound bound,
        SurvivorPanel[] omitted)
    {
        this.enclosures = enclosures;
        this.counts = counts;
        this.survivors = survivors;
        this.tracked = tracked;
        this.omitted = omitted;
        SurvivorCount = survivorCount;
        Bound = bound;
    }

    /// <summary>Gets the enclosures the intersection ran over, in order.</summary>
    public IReadOnlyList<Approximation> Enclosures => enclosures;

    /// <summary>Gets what this report does not hold, and so what no picture of it may draw.</summary>
    /// <remarks>
    /// <para>
    /// Empty for a chart walk; <see cref="SurvivorPanel.Collapse"/> and
    /// <see cref="SurvivorPanel.NearestExcluded"/> for a deep one. <b>Both, not one</b>: the nearest
    /// excluded come from walking the widest enclosure alone, which is the chart's first prefix and
    /// the exact pass a deep run deletes, so what a deep picture has left is the survivors against
    /// the half-width with nothing to contrast them against.
    /// </para>
    /// <para>
    /// A value rather than prose so every place that renders this report reads the same answer -
    /// the chart, which replaces each panel with a statement of why it is missing, and the
    /// epilogue, which lists them. A picture that silently dropped a panel would look like a run in
    /// which nothing happened there.
    /// </para>
    /// </remarks>
    public IReadOnlyList<SurvivorPanel> Omitted => omitted;

    /// <summary>Gets how many rationals were still standing after each enclosure, in order.</summary>
    /// <remarks>
    /// <para>
    /// One entry per enclosure from a chart walk, and <b>none from a deep one</b>, which walks once
    /// with every enclosure and so never learns what any shorter prefix admits.
    /// <see cref="Omitted"/> carries <see cref="SurvivorPanel.Collapse"/> in that case, and
    /// <see cref="SurvivorCount"/> is the final figure either way.
    /// </para>
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
    /// <remarks>
    /// Held rather than read off <see cref="Counts"/>, since a deep walk has no counts to read it
    /// from. For a chart walk the two agree: this is the last entry.
    /// </remarks>
    public long SurvivorCount { get; }

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
    /// <para>
    /// A deep walk has no first pass, so it follows the survivors alone and <see cref="Omitted"/>
    /// carries <see cref="SurvivorPanel.NearestExcluded"/>.
    /// </para>
    /// </remarks>
    public IReadOnlyList<BigRational> Tracked => tracked;

    /// <summary>Gets the largest denominator considered, which is <see cref="Bound"/>'s <see cref="SurvivorBound.Q"/>.</summary>
    public BigInteger DenominatorBound => Bound.Q;

    /// <summary>Gets where the denominator bound came from: the precision, or the budget below it.</summary>
    /// <remarks>
    /// Carried on the report rather than beside it because two things drawn from the report read
    /// differently under a cap - the null, which falls below <c>6/pi^2</c>, and what an empty set
    /// refutes - and a chart that travels away from its run must still be able to say which.
    /// A chart walk is never capped: it refuses on cost instead.
    /// </remarks>
    public SurvivorBound Bound { get; }

    /// <summary>
    /// Gets how many survivors a generic target of this precision would leave under the whole
    /// bound, by chance alone.
    /// </summary>
    /// <remarks>
    /// <c>6/pi^2</c>, about 0.61, and the same figure at every precision:
    /// <see cref="DerivedBound"/> takes <c>Q = floor(eps^(-1/2))</c>, so the <c>eps</c> and the
    /// <c>Q^2</c> cancel and nothing is left that depends on how deep the run went. What follows
    /// from that about reading <see cref="SurvivorCount"/> is
    /// <c>../SPEC-rational-ratio.md</c> § 1's and is not argued again here.
    /// <para>
    /// <b>Below that under a cap.</b> When <see cref="Bound"/> is capped, <c>Q</c> sits under
    /// <c>eps^(-1/2)</c>, the cancellation is incomplete, and the figure is
    /// <c>6/pi^2</c> times the square of how far the cap fell short.
    /// </para>
    /// </remarks>
    public Approximation ExpectedUnderBound => ExpectedAt(DenominatorBound);

    /// <summary>How many survivors that simple a generic target of this precision would leave by chance.</summary>
    /// <param name="denominator">The denominator to price. Non-negative.</param>
    /// <returns>The expected count, enclosed.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="denominator"/> is negative.</exception>
    /// <remarks>
    /// An instance member rather than a second argument on <see cref="ExpectedSurvivors"/>,
    /// because the <c>eps</c> is not the caller's to choose: it is this run's final half-width,
    /// and pricing a survivor against any other precision reports a number about a run that did
    /// not happen.
    /// </remarks>
    public Approximation ExpectedAt(BigInteger denominator) =>
        ExpectedSurvivors(enclosures[^1].MaxError, denominator);

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

    /// <summary>
    /// The expected number of rationals of denominator at or below <paramref name="denominator"/>
    /// that an interval of half-width <paramref name="error"/> holds, for a target with no
    /// arithmetic reason to sit near a simple rational.
    /// </summary>
    /// <param name="error">The enclosure's half-width. Non-negative.</param>
    /// <param name="denominator">The denominator to price. Non-negative.</param>
    /// <returns><c>6 * error * denominator^2 / pi^2</c>, enclosed.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Either argument is negative.</exception>
    /// <remarks>
    /// <para>
    /// <b>The null a survivor set is read against, ratified into
    /// <c>../SPEC-rational-ratio.md</c> § 1 on 2026-09-08.</b> What the figure means, why the
    /// count rather than the simplest survivor's denominator is the wrong thing to lead with, and
    /// that it is an <i>upper</i> bound erring towards calling a survivor unremarkable, are all
    /// stated there and not restated here. The measurement behind it - 40 generic targets at each
    /// of three precisions - is in <c>../CASEBOOK.md</c>, which is where a measurement belongs.
    /// </para>
    /// <para>
    /// What is local is the arithmetic. <paramref name="error"/> is a half-width rather than a
    /// whole interval, so the factor of two in the interval's length is already folded into the
    /// six; and pi is taken from <see cref="MachinPi"/> and squared rather than <c>6/pi^2</c>
    /// being written in as a decimal, so the result is an enclosure carrying a proven bound like
    /// every other value in this bench.
    /// </para>
    /// <para>
    /// It is the one figure this command reports that is not a claim about the target. It is a
    /// claim about targets in general, which is exactly what a null is.
    /// </para>
    /// </remarks>
    public static Approximation ExpectedSurvivors(BigRational error, BigInteger denominator)
    {
        if (error.Sign < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(error), error, "A half-width is a distance and cannot be negative.");
        }

        ArgumentOutOfRangeException.ThrowIfNegative(denominator);

        BigRational span = BigRational.FromInteger(denominator);
        BigRational counted = BigRational.FromInteger(6) * error * span * span;

        return Approximation.Divide(Approximation.Exact(counted), PiSquared);
    }

    /// <summary>Pi, pinned and squared, through the provider rather than a literal.</summary>
    private static Approximation SquarePi()
    {
        IRealConstant pi = new MachinPi();

        return pi.ApproximateTo(PiTolerance).Pow(2);
    }

    /// <summary>Intersects the enclosures one at a time, counting what is left after each.</summary>
    /// <param name="enclosures">The enclosures, in order. At least one.</param>
    /// <param name="denominatorBound">The largest denominator to consider.</param>
    /// <param name="trackedCap">How many candidates the distance chart may follow. At least one.</param>
    /// <param name="afterEach">
    /// Called with each enclosure's index and the count still standing after it, so a caller can
    /// report progress on a walk whose first step is much the most expensive. May be null.
    /// </param>
    /// <param name="walk">The walk to call once per prefix; <see cref="SurvivorSearch.Survivors"/> when null.</param>
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
        Action<int, long>? afterEach = null,
        SurvivorWalk? walk = null)
    {
        Validate(enclosures, trackedCap);
        walk ??= SurvivorSearch.Survivors;

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

            foreach (BigRational survivor in walk(enclosures.Take(index + 1), denominatorBound))
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
            counts[^1],
            [.. standing],
            Follow(standing, nearest, trackedCap),
            new SurvivorBound(denominatorBound, null),
            []);
    }

    /// <summary>Intersects every enclosure in one walk, producing the survivor set and nothing that needs a prefix.</summary>
    /// <param name="enclosures">The enclosures, in order. At least one.</param>
    /// <param name="bound">
    /// Where the bound came from; the walk runs to its <see cref="SurvivorBound.Q"/>, which a
    /// budget may have capped below the derived depth.
    /// </param>
    /// <param name="trackedCap">How many survivors the distance chart may follow. At least one.</param>
    /// <param name="walk">The walk to call, once; <see cref="SurvivorSearch.Survivors"/> when null.</param>
    /// <returns>The report, with <see cref="Omitted"/> naming the two panels a single walk cannot produce.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="enclosures"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="enclosures"/> is empty.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="bound"/>'s <see cref="SurvivorBound.Q"/> is negative, or
    /// <paramref name="trackedCap"/> is below one.
    /// </exception>
    /// <remarks>
    /// <para>
    /// <b>The same survivor set as <see cref="Of"/>, element for element</b> - ruling 4 on
    /// <c>halheinrich/Math#64</c>. <see cref="SurvivorSearch"/> intersects every enclosure it is
    /// given and seeds from the narrowest, so this one call returns exactly what <see cref="Of"/>'s
    /// last prefix returns. Nothing is weakened and no searcher is written: it is a call site
    /// making one call where the chart makes one per prefix.
    /// </para>
    /// <para>
    /// <b>What it costs is the pictures.</b> The chart's widest prefix is what dominates its price
    /// and is also what the collapse's first point and the nearest-excluded family are made from,
    /// so skipping that walk loses both. <see cref="Omitted"/> says so. Rebuilding the family by
    /// walking the widest separately would spend the exact cost this exists to avoid, and having
    /// the search report near misses is a change to a contract whose promise is "everything still
    /// standing" - a separate question, left for its own issue.
    /// </para>
    /// <para>
    /// The walk is the final prefix, seeded from the narrowest enclosure, so at a derived bound its
    /// inner loop holds about one candidate and the whole cost is stepping the denominators.
    /// <see cref="SurvivorRun.Size"/> prices it that way.
    /// </para>
    /// </remarks>
    public static SurvivorReport Deep(
        IReadOnlyList<Approximation> enclosures,
        SurvivorBound bound,
        int trackedCap,
        SurvivorWalk? walk = null)
    {
        Validate(enclosures, trackedCap);
        walk ??= SurvivorSearch.Survivors;

        long count = 0;
        var standing = new List<BigRational>();

        foreach (BigRational survivor in walk(enclosures, bound.Q))
        {
            count++;

            if (standing.Count < SurvivorsShown)
            {
                standing.Add(survivor);
            }
        }

        return new SurvivorReport(
            [.. enclosures],
            [],
            count,
            [.. standing],
            Follow(standing, [], trackedCap),
            bound,
            [.. DeepOmits]);
    }

    /// <summary>The argument checks both walks share, made before either costs anything.</summary>
    private static void Validate(IReadOnlyList<Approximation> enclosures, int trackedCap)
    {
        ArgumentNullException.ThrowIfNull(enclosures);
        ArgumentOutOfRangeException.ThrowIfLessThan(trackedCap, 1);

        if (enclosures.Count == 0)
        {
            throw new ArgumentException(NoEnclosuresMessage, nameof(enclosures));
        }
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
