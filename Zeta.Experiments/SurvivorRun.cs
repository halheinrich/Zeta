using System.Diagnostics;
using System.Globalization;
using System.Numerics;

namespace HalHeinrich.Numerics.Experiments;

/// <summary>
/// The deliverable: <c>pi^n / zeta(n)</c> run to a schedule, reported as
/// <c>../SPEC-rational-ratio.md</c> § 2 step 6 says a run reports - the survivor set under a
/// denominator bound fixed in advance - and drawn.
/// </summary>
/// <remarks>
/// <para>
/// <b>This is what <c>walk</c> and <c>target</c> could not say.</b> Both of those print a trend
/// matrix and a bound read off the last candidate a sweep proposed. § 2 was amended on 2026-09-08
/// to make the survivor set the result and the matrix presentation, and nothing in this repository
/// computed a survivor set until this command. The pipeline needed no reshaping to do it: the
/// search takes enclosures of the unknown, and a ratio run has been yielding them all along.
/// </para>
/// <para>
/// <b>The order is a parameter and the two providers are not.</b> Naming two arbitrary constants
/// is <c>halheinrich/Math#65</c> and is genuinely a reshape; naming the order is not, since
/// <see cref="EulerMaclaurinZeta"/> already takes one and the base is pi either way. An even order
/// has an exact answer that § 1 lists, so the survivor set is checkable by eye; an odd one does
/// not, which is the point of the bench.
/// </para>
/// <para>
/// <b>The schedule is a parameter with a default, and the default is what it is because of a cost
/// law rather than taste.</b> Every point of the collapse chart counts the rationals of denominator
/// at or below <c>Q</c> that its own prefix admits, walking <c>1..Q</c> afresh to do it, and the
/// widest prefix dominates the sum - about <c>h/eps</c> candidates, where <c>h</c> is the first
/// target and <c>eps</c> the last. So
/// deepening the schedule by one decade costs ten times as much, where <c>target</c>'s own
/// schedule costs roughly double per decade. A knob whose every notch is ten times the last is not
/// one to turn casually, which is why it was a constant until somebody had measured what the
/// constant cost: a scratchpad probe on 2026-09-08 took this same ratio to <c>Q = 4.4e7</c> and
/// found the schedule, not the providers and not the search, to be what confined the exhibit to
/// <c>Q = 11,585</c>. So the span moved to the caller with its price stated, and
/// <see cref="Refuse"/> still checks the estimate against a budget before spending it.
/// </para>
/// <para>
/// <b>Starting the schedule later is the cheaper knob, and the one worth reaching for first.</b>
/// The estimate is dominated by <c>h*Q^2</c>, so a decade off the last end multiplies <c>Q^2</c> by ten while a
/// decade off the first end divides <c>h</c> by less than that, and by no fixed factor. The reason
/// is <see cref="RatioEnclosure.Of"/>, which coarsens: every realised half-width is the least
/// power of two at or above the propagated bound, so <c>h</c> lives on a power-of-two grid <i>by
/// construction</i> rather than by anything the providers do. Which point of that grid a schedule
/// lands on is then set by the first provider step to meet the target, and the two interact.
/// Measured here at order 3, first ends of 2, 3, 4 and 5 realise <c>2^-8</c>, <c>2^-10</c>,
/// <c>2^-15</c> and <c>2^-17</c> - four, then thirty-two, then four again, about eight to the
/// decade on average. Deterministic and explainable, in other words, and not a quirk: a caller
/// wanting another decade of depth inside the same budget buys it by starting later, at the cost
/// of a shorter collapse chart. That trade is the caller's to make, which is the whole reason both
/// ends are arguments rather than only the last.
/// </para>
/// <para>
/// <b>No pass, no fail.</b> § Exactness discipline: a target with an unknown answer belongs in a
/// runnable project and its output is data. The exit code says only that the run completed.
/// </para>
/// </remarks>
internal static class SurvivorRun
{
    /// <summary>The first exponent of the schedule when none is given.</summary>
    public const int DefaultFirstExponent = 2;

    /// <summary>The last exponent of the schedule when none is given.</summary>
    /// <remarks>
    /// Unchanged from when the span was a constant, and deliberately: at order 3 it derives
    /// <c>Q = 11,585</c> and an opening step of about a million candidates, which is the few
    /// seconds the exhibit has always cost. A deeper default is a separate judgement with the cost
    /// law above attached to it, and would be made by someone deciding what a first-time reader
    /// should wait for.
    /// </remarks>
    public const int DefaultLastExponent = 8;

    /// <summary>The shallowest exponent either end of a schedule may name.</summary>
    /// <remarks>
    /// Every figure this command reports reads an exponent as a decimal place - the schedule
    /// label, <c>Q = floor(eps^(-1/2))</c>, and the null <c>6*eps*q^2/pi^2</c>. At zero the target
    /// is 1 and at anything below it the target is looser still, so there is no precision for
    /// those figures to be read from; a negative one does not even print, since the label would
    /// come out <c>1e--1</c>. <see cref="TargetSchedule.Decades"/> accepts such exponents and says
    /// so, because a loose first column is well-formed as a <i>schedule</i>. It is this command's
    /// report that cannot carry one, so the floor is here.
    /// </remarks>
    public const int MinExponent = 1;

    /// <summary>The zeta order when none is given.</summary>
    public const int DefaultOrder = 2;

    /// <summary>The largest order this command will run.</summary>
    /// <remarks>
    /// § 1's positive controls run to 16 because that is where the even orders stop having small
    /// denominators, so nothing above it would be checked against anything. This is a limit on
    /// what is worth running rather than on what would work.
    /// </remarks>
    public const int MaxOrder = 16;

    /// <summary>How many candidates the distance chart follows.</summary>
    /// <remarks>
    /// <para>
    /// Small on purpose, and four rather than six because of what the nearest excluded candidates
    /// look like. With <c>Q</c> derived as <c>eps^(-1/2)</c>, the closest rational under the bound
    /// sits about <c>1/Q</c> from the answer, so the nearest rivals are all at very nearly the same
    /// distance and draw very nearly the same line. Three of them establish that they behave as a
    /// family; five only thicken the stroke.
    /// </para>
    /// <para>
    /// The contrast the panel exists for is between the survivor and that family, not among its
    /// members: the survivor's distance keeps falling with the half-width, and the rivals flatten
    /// at <c>1/Q</c> and are crossed.
    /// </para>
    /// </remarks>
    public const int TrackedCap = 4;

    /// <summary>The searcher this command drives the pipeline with, which does nothing.</summary>
    /// <remarks>
    /// <para>
    /// <b>A run whose result is a survivor set has no use for a trend matrix, and was paying for
    /// one at every target.</b> <see cref="NoSearch"/> carries the argument and the measurements;
    /// what is here is the wiring, and the fact that it is a member rather than a literal at the
    /// call site so that a test can put it through <see cref="RatioRun.Execute"/>'s own searcher
    /// parameter and count what it proposes.
    /// </para>
    /// <para>
    /// Stateless, so one instance serves every run.
    /// </para>
    /// </remarks>
    public static IRationalApproximator Searcher { get; } = new NoSearch();

    /// <summary>How long this command will spend enumerating candidates before it refuses.</summary>
    /// <remarks>
    /// <para>
    /// <b>A time, because a count does not transfer between orders.</b> Measured on
    /// <c>halheinrich/Math#64</c> at one schedule with only the order varying: 6.0, 11.7 and 33.3
    /// microseconds a candidate at orders 3, 6 and 10 - monotone, and a factor of five and a half
    /// across a range the exhibit is meant to run. So sixty million candidates, which this budget
    /// replaces, was six minutes at order 3 and thirty-three at order 10 under one number that
    /// claimed to mean the same thing at both. The price is flat in <c>Q</c> (3.5 to 6.9
    /// microseconds across a 362-fold range) and rises with the <i>order</i>, because what an
    /// exact <see cref="Approximation.Contains"/> cross-multiplies is the enclosure endpoint,
    /// whose size grows with the order rather than with the candidate's denominator.
    /// </para>
    /// <para>
    /// Five minutes, which is the sixty million candidates this replaces expressed at the order-3
    /// price they were measured at - so the schedules the exhibit already documents are admitted
    /// and refused exactly as before, and what changes is the high orders, where a count was
    /// admitting half-hour runs.
    /// </para>
    /// <para>
    /// <b>Not a wall-clock abort.</b> The prediction is made before the search and the search then
    /// runs to completion, so a slow machine refuses runs a fast one admits and neither reports a
    /// different answer. A timed abort would instead cut the walk short at whatever depth the
    /// clock ran out at, which makes <c>Q</c> - and so the bound the run claims - a property of how
    /// fast the machine was that day. <c>../AGENTS.md</c> § Exactness discipline: the refusal may
    /// be machine-dependent, the result may not.
    /// </para>
    /// </remarks>
    public const int BudgetSeconds = 300;

    /// <summary>About how much work the smaller of the two calibration walks aims to be.</summary>
    /// <remarks>
    /// <para>
    /// Large enough that the clock is not the error - four thousand units is tens of milliseconds
    /// at every price measured here, against a <see cref="Stopwatch"/> tick of a hundred
    /// nanoseconds - and small enough to be lost in the run it prices. It is a target rather than
    /// a count: <see cref="SampleBound"/> doubles a denominator bound until the size reaches it,
    /// and that size is quadratic in the bound, so a sample may overshoot by up to a factor of
    /// four.
    /// </para>
    /// <para>
    /// The whole calibration is this walk twice - once discarded - plus one at
    /// <see cref="SampleSpread"/> times the bound, which lands near half a second at the default
    /// schedule and does not grow with <c>Q</c>. It is paid on every run, including the ones that
    /// go on to be refused, and it buys the only thing that makes a time budget honest: a price
    /// measured on this machine, at this order, against these enclosures, rather than a constant
    /// carried from the bench somebody last measured on.
    /// </para>
    /// </remarks>
    public const int SampleCandidates = 4_000;

    /// <summary>How far apart the calibration's two sample bounds sit.</summary>
    /// <remarks>
    /// One walk cannot separate the outer loop's price from the inner one's; two at different
    /// bounds can, because the outer count is linear in the bound and the inner one quadratic.
    /// Four rather than two, because the inner term is what the solution recovers as a difference:
    /// at a spread of two the larger walk's inner work is a tenth of its total on the default
    /// schedule, and a couple of per cent of timing noise moves the answer by a third. At four it
    /// is not, and the larger walk still costs a fraction of a second.
    /// </remarks>
    public const int SampleSpread = 4;

    /// <summary>How many times each sample bound is walked, of which the fastest is the timing.</summary>
    /// <remarks>
    /// <para>
    /// <b>The first walk of anything in this process is not a measurement of the walk.</b> The
    /// runtime compiles in tiers, and a method reaches the optimised tier only after a call count
    /// and a delay that <i>restarts</i> while new methods are still being compiled - so a sample
    /// taken shortly after start-up times the promotion as much as the work.
    /// </para>
    /// <para>
    /// Measured here 2026-09-09, and it is not a small effect. The same sample walk - 3,584
    /// denominators, 1,290 candidates - took 63 ms inside <c>survivors 3</c> and 23 ms inside
    /// <c>survivors 3 2 9</c>, whose longer pipeline had already warmed the same arithmetic.
    /// Nearly threefold, on nearly identical work, decided by what had run before it. The cold
    /// reading also made the candidate price come out <i>negative</i> and be clamped away, because
    /// the inner loop's share of a small sample is a few per cent and a threefold error swamps it.
    /// </para>
    /// <para>
    /// The fastest of a few passes rather than their mean, and that is not a preference for the
    /// flattering number: every contaminant here - compilation, tier promotion, a scheduler
    /// preempting the thread - only ever <i>adds</i> time, so the minimum is the closest estimate
    /// of steady-state cost a short sample can give.
    /// </para>
    /// </remarks>
    public const int SamplePasses = 3;

    /// <summary>How long the calibration walks before it starts believing its own clock.</summary>
    /// <remarks>
    /// <para>
    /// <b>The runtime's optimised tier arrives on a timer, not only on a call count</b>, and the
    /// timer restarts while new methods are still being compiled. So a fixed number of short passes
    /// does not reach steady state: measured here 2026-09-09 on the default schedule, ten
    /// successive walks of the same sample ran 51, 51, 52, 43, 62, 48, 36, 35, 34 and 37
    /// milliseconds - flat for three passes at a third above the truth, which is exactly long
    /// enough to fool a stop-when-two-agree rule.
    /// </para>
    /// <para>
    /// Three tenths of a second is what it took to fall through on this bench. It is a wall clock
    /// rather than a pass count so a slower machine spends fewer passes reaching the same place,
    /// and it bounds the whole calibration at well under a second on any run.
    /// </para>
    /// <para>
    /// <b>It is not the whole answer and must not be relied on as one.</b>
    /// <c>Zeta.Experiments.csproj</c> turns off quick-jitting for loop-bearing methods, so the walk
    /// is compiled at full optimisation the first time it runs and the sample sees the code the run
    /// will. That project file carries the measurement; what this constant then buys is the
    /// residue - the scheduler, the caches, whatever a first pass over cold data costs.
    /// </para>
    /// </remarks>
    public const double SampleWarmUpSeconds = 0.3;

    private const string NoEnclosuresMessage =
        "A run with no enclosures intersects nothing and has no cost to estimate. " +
        "SurvivorReport.Of refuses the same list for the same reason.";

    private const string NegativePriceMessage =
        "A candidate cannot cost negative time. A price comes from Calibrate, which divides a " +
        "stopwatch reading by a candidate count and so cannot produce one.";

    /// <summary>Seconds to microseconds, for the one figure this command reports in them.</summary>
    internal static readonly BigRational Million = BigRational.FromInteger(1_000_000);

    /// <summary>Runs the survivor report and writes it.</summary>
    /// <param name="arguments">The command's arguments, as <see cref="Interpret"/> reads them.</param>
    /// <returns>Zero when the run completed, 2 when the arguments or the estimated cost were refused.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="arguments"/> is null.</exception>
    public static int Run(string[] arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        TextWriter notes = Console.Error;

        string? refused = Interpret(arguments, out SurvivorRequest request);
        if (refused is not null)
        {
            notes.WriteLine(refused);
            return 2;
        }

        IReadOnlyList<BigRational> schedule = request.Schedule();
        Preamble(notes, request, schedule.Count);

        var clock = Stopwatch.StartNew();
        RatioRun run = RatioRun.Execute(
            new MachinPi(), request.Order, new EulerMaclaurinZeta(request.Order), schedule, Searcher);
        IReadOnlyList<Approximation> enclosures = SurvivorReport.Distinct(SurvivorReport.EnclosuresOf(run));

        BigInteger bound = SurvivorReport.DerivedBound(enclosures[^1]);
        WalkPrice price = Calibrate(enclosures, bound);

        SurvivorRefusal? tooDear = Refuse(enclosures, bound, price);
        if (tooDear is not null)
        {
            notes.WriteLine();
            notes.WriteLine(tooDear.Value.Message);
            return 2;
        }

        Sizing(notes, run, enclosures, bound, price);

        var walk = Stopwatch.StartNew();
        SurvivorReport report = SurvivorReport.Of(
            enclosures,
            bound,
            TrackedCap,
            (index, count) => notes.WriteLine(string.Create(CultureInfo.InvariantCulture,
                $"  enclosure {index}  half-width {Presentation.Magnitude(enclosures[index].MaxError),-9}  " +
                $"still standing {count:N0}")));

        walk.Stop();
        clock.Stop();

        SurvivorChart.Write(Console.Out, report, Caption(request, run, enclosures, bound));
        Epilogue(notes, report, request.Order, clock.Elapsed.TotalSeconds, walk.Elapsed.TotalSeconds);

        return 0;
    }

    /// <summary>What the command's arguments ask for, or the reason they are refused.</summary>
    /// <param name="arguments">
    /// Nothing, the order alone, or the order followed by both ends of the schedule -
    /// <c>survivors</c>, <c>survivors 3</c>, <c>survivors 3 2 12</c>.
    /// </param>
    /// <param name="request">
    /// What was asked for, when this returns null; <see langword="default"/> otherwise.
    /// </param>
    /// <returns>The refusal to print, or null when the request stands.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="arguments"/> is null.</exception>
    /// <remarks>
    /// <para>
    /// <b>Reading the arguments is separated from acting on them, and every case below is a test's
    /// to hand over.</b> <c>../CLAUDE.md</c> § Shell records the seam and why: an argument path
    /// reachable only by starting a run is a path nothing checks, and each of the defects that
    /// reached a push in this project lived in exactly that gap. So this returns a value rather
    /// than printing one, and <see cref="Run"/> is left with nothing to decide.
    /// </para>
    /// <para>
    /// <b>Two arguments are refused rather than guessed at.</b> A second argument could name the
    /// last exponent, with the first left at its default, or the first with the last left at its -
    /// and the two readings differ by ten decades of cost. The schedule is taken as a pair or not
    /// at all, so that no invocation quietly means something other than what it looks like.
    /// </para>
    /// </remarks>
    public static string? Interpret(string[] arguments, out SurvivorRequest request)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        request = default;

        if (arguments.Length is 2 or > 3)
        {
            return "survivors takes the order of zeta, and then both ends of the schedule or " +
                "neither: 'survivors', 'survivors 3', or 'survivors 3 2 12'. One exponent alone " +
                "would leave it guessing which end of the schedule you meant.";
        }

        int order = DefaultOrder;
        int first = DefaultFirstExponent;
        int last = DefaultLastExponent;
        string? unreadable = null;

        if (arguments.Length > 0 && !Whole(arguments[0], "an order", "survivors 3", out order, ref unreadable))
        {
            return unreadable;
        }

        if (arguments.Length == 3 &&
            (!Whole(arguments[1], "an exponent", "survivors 3 2 12", out first, ref unreadable) ||
             !Whole(arguments[2], "an exponent", "survivors 3 2 12", out last, ref unreadable)))
        {
            return unreadable;
        }

        string? refusal = RefuseOrder(order) ?? RefuseSchedule(first, last);
        if (refusal is not null)
        {
            return refusal;
        }

        request = new SurvivorRequest(order, first, last);
        return null;
    }

    /// <summary>The reason this order will not be run, or null when it will.</summary>
    /// <param name="order">The requested order.</param>
    /// <returns>The refusal, or null.</returns>
    /// <remarks>
    /// A pure function of the argument, so it is held against tests without paying for a run - the
    /// same seam <c>target</c> uses, and for the same reason: an argument path reachable only by
    /// starting a long computation is a path nothing checks.
    /// </remarks>
    public static string? RefuseOrder(int order)
    {
        if (order < 2)
        {
            return "The order must be at least 2. At s = 1 the series is the harmonic one and does " +
                "not converge, so there is no zeta(1) to divide by.";
        }

        return order > MaxOrder
            ? string.Create(CultureInfo.InvariantCulture,
                $"Refusing order {order}: this command runs no higher than {MaxOrder}. Section 1's " +
                $"positive controls stop there, so nothing above it can be checked against a known " +
                $"answer, and an unchecked exhibit is not worth the wait.")
            : null;
    }

    /// <summary>The reason this schedule will not be run, or null when it will.</summary>
    /// <param name="firstExponent">The first target's exponent, as <c>10^-firstExponent</c>.</param>
    /// <param name="lastExponent">The last target's exponent, as <c>10^-lastExponent</c>.</param>
    /// <returns>The refusal, or null.</returns>
    /// <remarks>
    /// <para>
    /// <b>Shape only. What a schedule costs is <see cref="Refuse"/>'s question and cannot be
    /// answered here</b>, because the estimate is priced off the enclosures a run <i>realises</i>
    /// and not off the targets it was asked for - and the two differ by however far the providers
    /// overshoot. So there is no ceiling on the last exponent to match <c>target</c>'s: a deep
    /// schedule is refused for what it would cost, once that is known, rather than for being deep.
    /// </para>
    /// <para>
    /// <b>Unlike <c>target</c>, a single-column schedule is allowed.</b> That command refuses one
    /// because its whole output is a trend across columns; this one's output is a survivor set,
    /// which one enclosure already produces - and a run whose providers realise the same error
    /// twice collapses to a single distinct enclosure anyway, so the path is not a new one.
    /// </para>
    /// <para>
    /// A pure function of the two arguments, held against tests without a run behind it - the same
    /// seam <see cref="RefuseOrder"/> and <see cref="Refuse"/> use, for the same reason.
    /// </para>
    /// </remarks>
    public static string? RefuseSchedule(int firstExponent, int lastExponent)
    {
        if (firstExponent < MinExponent)
        {
            return string.Create(CultureInfo.InvariantCulture,
                $"The schedule's exponents must be at least {MinExponent}, and the first one is " +
                $"{firstExponent}. An exponent at or below zero names a target of 1 or looser, which " +
                $"is not a precision: Q is derived as floor(eps^(-1/2)) and every survivor is priced " +
                $"against 6*eps*q^2/pi^2, and both read the exponent as a decimal place. The last " +
                $"exponent is covered by the same floor, since it may not precede the first.");
        }

        return lastExponent < firstExponent
            ? string.Create(CultureInfo.InvariantCulture,
                $"The schedule 1e-{firstExponent} .. 1e-{lastExponent} loosens. The last exponent must be " +
                $"at or after the first, since the exponents grow as the targets tighten. Refused " +
                $"here rather than by TargetSchedule.Decades, whose message would name its own " +
                $"parameters instead of this experiment's arguments.")
            : null;
    }

    /// <summary>The reason this run's enumeration will not be paid for, or null when it will.</summary>
    /// <param name="enclosures">Every enclosure the run will intersect, in order.</param>
    /// <param name="denominatorBound">The derived bound.</param>
    /// <param name="price">What each loop costs here, as <see cref="Calibrate"/> measures it.</param>
    /// <returns>The refusal, or null.</returns>
    /// <remarks>
    /// <para>
    /// A pure function of its arguments, which is what lets a test exercise the refusal without a
    /// run behind it. The measurement is the caller's: this sizes the walk, multiplies by a price
    /// it was handed, and compares the product with <see cref="BudgetSeconds"/> - every step of it
    /// exact rational arithmetic over given values.
    /// </para>
    /// <para>
    /// It returns a <see cref="SurvivorRefusal"/> rather than the sentence, so that which end of
    /// the schedule the advice names is a value a test can read rather than a phrase it has to
    /// match. That distinction is the point of the change: the defect being fixed here was a
    /// sentence that was true of one end and false of the other.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="enclosures"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="enclosures"/> is empty.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Either half of <paramref name="price"/> is negative.</exception>
    public static SurvivorRefusal? Refuse(
        IReadOnlyList<Approximation> enclosures,
        BigInteger denominatorBound,
        WalkPrice price)
    {
        if (price.PerDenominator.Sign < 0 || price.PerCandidate.Sign < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(price), price, NegativePriceMessage);
        }

        WalkSize size = Size(enclosures, denominatorBound);

        return price.Seconds(size) > BigRational.FromInteger(BudgetSeconds)
            ? new SurvivorRefusal(
                size,
                price,
                denominatorBound,
                enclosures.Count,
                ScheduleEnd.First,
                ScheduleEnd.Last)
            : null;
    }

    /// <summary>The widest enclosure of a run, which is the one the calibration samples.</summary>
    /// <param name="enclosures">Every enclosure the run will intersect, in order.</param>
    /// <returns>That enclosure alone, as a list a walk can be run against.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="enclosures"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="enclosures"/> is empty.</exception>
    /// <remarks>
    /// Found by half-width rather than taken as <c>enclosures[0]</c>. A run's enclosures only
    /// tighten so the two agree there, and this is what makes the same function right for a list a
    /// test hands over in any order - the same reason <see cref="Size"/> tracks a running minimum.
    /// </remarks>
    public static IReadOnlyList<Approximation> Widest(IReadOnlyList<Approximation> enclosures)
    {
        ArgumentNullException.ThrowIfNull(enclosures);

        if (enclosures.Count == 0)
        {
            throw new ArgumentException(NoEnclosuresMessage, nameof(enclosures));
        }

        Approximation widest = enclosures[0];

        foreach (Approximation enclosure in enclosures)
        {
            if (enclosure.MaxError > widest.MaxError)
            {
                widest = enclosure;
            }
        }

        return [widest];
    }

    /// <summary>The smaller of the two denominator bounds the calibration walks to.</summary>
    /// <param name="enclosures">Every enclosure the run will intersect, in order.</param>
    /// <param name="denominatorBound">The derived bound the real walk will run to.</param>
    /// <returns>
    /// The least power of two at which a walk of the widest enclosure alone reaches
    /// <see cref="SampleCandidates"/>, and never more than <paramref name="denominatorBound"/> - a
    /// run smaller than the sample is sampled by being run.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="enclosures"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="enclosures"/> is empty.</exception>
    /// <remarks>
    /// <para>
    /// A pure function, so what the sample is asked to do is decidable without timing anything.
    /// Only how long it then takes is a measurement.
    /// </para>
    /// <para>
    /// Doubling rather than solving for the bound: the size is a sum of quadratics whose
    /// coefficients are the realised half-widths, and inverting it exactly would put a second
    /// spelling of the cost law in the file. Doubling asks <see cref="Size"/> itself, so the sample
    /// is sized by the same arithmetic the prediction uses and cannot drift from it.
    /// </para>
    /// </remarks>
    public static BigInteger SampleBound(IReadOnlyList<Approximation> enclosures, BigInteger denominatorBound)
    {
        IReadOnlyList<Approximation> widest = Widest(enclosures);
        BigInteger bound = BigInteger.One;

        while (bound * SampleSpread < denominatorBound && Size(widest, bound).Total < SampleCandidates)
        {
            bound *= 2;
        }

        return BigInteger.Min(bound, denominatorBound);
    }

    /// <summary>Times two short walks over the real enclosures and solves for what each loop costs.</summary>
    /// <param name="enclosures">Every enclosure the run will intersect, in order.</param>
    /// <param name="denominatorBound">The derived bound the real walk will run to.</param>
    /// <returns>
    /// The two prices, with the bounds they were measured at. Both are zero when there is no
    /// sample to walk, which is a run with nothing to price rather than a free one.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="enclosures"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="enclosures"/> is empty.</exception>
    /// <remarks>
    /// <para>
    /// <b>The only measured quantity in this command, and it decides only what to spend.</b>
    /// Everything the run reports - the survivor set, <c>Q</c>, the null - is exact arithmetic over
    /// proven bounds. This is a stopwatch, and it is confined to the question of whether to start;
    /// <c>../AGENTS.md</c> § Exactness discipline bans an empirical number from a computational
    /// path, and a cost guard is not one.
    /// </para>
    /// <para>
    /// <b>It walks the production path rather than a model of it.</b>
    /// <see cref="SurvivorReport.Of"/> at a smaller bound does exactly what the real walk does,
    /// with the same seeding and the same bookkeeping, so nothing here can drift from what it is
    /// pricing.
    /// </para>
    /// <para>
    /// <b>The sample walks the widest enclosure alone, not the whole prefix list, and that is what
    /// makes the inner price recoverable at all.</b> The two equations are only as independent as
    /// the candidate share differs between them, and against the full list that share is a few per
    /// cent at either bound - measured 2026-09-09, the inner term was 3% of the larger sample's
    /// time on the default schedule, so timing jitter drove the solved inner price negative on
    /// every run and it was clamped away. The widest enclosure has the highest candidate density
    /// available, which lifts that share to a fifth or more and makes the difference a signal
    /// rather than a rounding error.
    /// </para>
    /// <para>
    /// <b>What that costs is an assumption, stated here rather than hidden, and it is why this
    /// guard errs towards admitting.</b> Both prices are measured against the <i>widest</i>
    /// enclosure's endpoints and then applied to every prefix - and a narrower enclosure carries
    /// larger endpoints, so the prefixes the sample never touches cost more than it charges for
    /// them. Measured on this bench 2026-09-09, the prediction came to between 0.6 and 0.85 of the
    /// realised walk across four schedules. The epilogue prints the realised walk beside the
    /// prediction for exactly that reason: the guard's own error is on the screen of every run
    /// rather than something a reader has to take on trust.
    /// </para>
    /// <para>
    /// <b>Two bounds, a factor of <see cref="SampleSpread"/> apart, because one walk cannot
    /// separate the two loops.</b> A walk to <c>q</c> costs
    /// <c>a*Denominators(q) + b*Candidates(q)</c>, and the denominator count is linear in <c>q</c>
    /// where the candidate count is quadratic - so two bounds give two independent equations and
    /// the solution below is exact. One bound gives only the blend, and the blend is a property of
    /// the sample rather than of the machine: measured here, the same walk that costs 10
    /// microseconds a unit at <c>q = 1,024</c> costs 3.1 at <c>Q = 11,585</c>, because the first is
    /// 58% outer loop and the second 11%. A single-price sample scaled by a count overstated the
    /// real walk threefold, which is the whole reason this solves rather than divides.
    /// </para>
    /// <para>
    /// Each bound is walked <see cref="SamplePasses"/> times and the fastest kept, which is what
    /// keeps the two timings comparable across a tiering runtime; that constant's remarks carry
    /// the measurement behind it.
    /// </para>
    /// <para>
    /// A price that comes out negative is clamped to zero. The inner price is what this
    /// arrangement pins down; the outer one is the residue left after it, and on a shallow schedule
    /// that residue is small enough to land either side of zero from pass to pass. Clamping keeps
    /// the prediction monotone in the walk size, and it errs in the same direction as everything
    /// else here.
    /// </para>
    /// </remarks>
    public static WalkPrice Calibrate(IReadOnlyList<Approximation> enclosures, BigInteger denominatorBound)
    {
        IReadOnlyList<Approximation> widest = Widest(enclosures);
        BigInteger small = SampleBound(enclosures, denominatorBound);
        BigInteger large = BigInteger.Min(small * SampleSpread, denominatorBound);

        WalkSize lower = Size(widest, small);
        WalkSize upper = Size(widest, large);

        if (lower.Total.IsZero)
        {
            return new WalkPrice(BigRational.Zero, BigRational.Zero, small, large);
        }

        // The larger walk goes first and runs for at least SampleWarmUpSeconds, because the
        // runtime's optimised tier arrives on a timer as much as on a call count. By the time the
        // smaller walk is timed, everything under both of them is compiled the way the real walk
        // will find it - and the warm-up is not thrown away, it IS the larger measurement.
        BigRational upperSeconds = Fastest(widest, large, SampleWarmUpSeconds);
        BigRational lowerSeconds = Fastest(widest, small, 0);

        BigRational determinant =
            BigRational.FromInteger((lower.Denominators * upper.Candidates) -
                                    (upper.Denominators * lower.Candidates));

        if (determinant.IsZero)
        {
            // One equation, so only the blend is recoverable - and it is exactly right in the one
            // case that reaches here, where the sample bound has been capped at Q and the sample
            // is the run.
            BigRational blended = lowerSeconds / BigRational.FromInteger(lower.Total);
            return new WalkPrice(blended, blended, small, large);
        }

        BigRational perDenominator =
            ((lowerSeconds * BigRational.FromInteger(upper.Candidates)) -
             (upperSeconds * BigRational.FromInteger(lower.Candidates))) / determinant;

        BigRational perCandidate =
            ((upperSeconds * BigRational.FromInteger(lower.Denominators)) -
             (lowerSeconds * BigRational.FromInteger(upper.Denominators))) / determinant;

        return new WalkPrice(
            AtLeastNothing(perDenominator),
            AtLeastNothing(perCandidate),
            small,
            large);
    }

    /// <summary>How many candidates the whole intersection walks, both loops together.</summary>
    /// <param name="enclosures">Every enclosure the run will intersect, in order.</param>
    /// <param name="denominatorBound">The derived bound.</param>
    /// <returns>The estimate, truncated to an integer.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="enclosures"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="enclosures"/> is empty.</exception>
    /// <remarks>
    /// The figure the reports quote, which is <see cref="WalkSize.Total"/>. What it is not is a
    /// price: see <see cref="WalkSize"/> for why the two loops are counted apart.
    /// </remarks>
    public static BigInteger Estimate(IReadOnlyList<Approximation> enclosures, BigInteger denominatorBound) =>
        Size(enclosures, denominatorBound).Total;

    /// <summary>How much work the whole intersection is, counted as its two loops.</summary>
    /// <param name="enclosures">Every enclosure the run will intersect, in order.</param>
    /// <param name="denominatorBound">The derived bound.</param>
    /// <returns>The two counts, each truncated to an integer.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="enclosures"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="enclosures"/> is empty.</exception>
    /// <remarks>
    /// <para>
    /// <b>Every prefix is counted, not just the first.</b> One prefix steps through the
    /// denominators <c>1..Q</c> and offers, at each, the integers in an interval of width
    /// <c>2*h*q</c> - so about <c>Q</c> turns of the outer loop and <c>h*Q^2</c> of the inner, for
    /// a prefix of half-width <c>h</c>. <see cref="SurvivorReport.Of"/> enumerates each prefix
    /// afresh, so the run pays the sum over all of them and not the first term.
    /// </para>
    /// <para>
    /// <b>Omitting the outer loop was the guard's defect, ruled on <c>halheinrich/Math#64</c>
    /// 2026-09-09.</b> The estimate priced <c>enclosures[0]</c> alone while the call site held the
    /// whole list. At <c>1e-6 .. 1e-12</c> the omitted term exceeds the term that was counted; at
    /// <c>1e-10 .. 1e-12</c> it is the entire cost, where the shipped figure implied 20.8
    /// microseconds a candidate and the corrected one implies 6.9. A per-candidate price read off
    /// an estimate that omits most of the work is a measurement of the estimate.
    /// </para>
    /// <para>
    /// Each prefix is sized at its <i>narrowest</i> enclosure, which is the one
    /// <see cref="SurvivorSearch"/> seeds its walk from. In a run the enclosures only tighten, so
    /// that is the prefix's last element; the running minimum below is what makes the same
    /// arithmetic right for a list handed over by a test in any order.
    /// </para>
    /// <para>
    /// The inner count ignores the reduction to lowest terms, which removes a constant fraction,
    /// so it overstates by something under a factor of two and never understates. Exact rational
    /// arithmetic truncated at the end, not floating point: <c>Q</c> runs to millions and its
    /// square past a <see cref="double"/>'s integer range, where a figure quoted in a refusal would
    /// start being wrong in its leading digits.
    /// </para>
    /// </remarks>
    public static WalkSize Size(IReadOnlyList<Approximation> enclosures, BigInteger denominatorBound)
    {
        ArgumentNullException.ThrowIfNull(enclosures);

        if (enclosures.Count == 0)
        {
            throw new ArgumentException(NoEnclosuresMessage, nameof(enclosures));
        }

        BigRational bound = BigRational.FromInteger(denominatorBound);
        BigRational square = bound * bound;
        BigRational narrowest = enclosures[0].MaxError;
        BigRational candidates = BigRational.Zero;

        foreach (Approximation enclosure in enclosures)
        {
            if (enclosure.MaxError < narrowest)
            {
                narrowest = enclosure.MaxError;
            }

            candidates += narrowest * square;
        }

        return new WalkSize(
            denominatorBound * enclosures.Count,
            candidates.Numerator / candidates.Denominator);
    }

    /// <summary>A price with the noise of a short timing clamped out of it.</summary>
    private static BigRational AtLeastNothing(BigRational price) =>
        price.Sign < 0 ? BigRational.Zero : price;

    /// <summary>Walks the enclosures to one bound repeatedly and keeps the fastest pass.</summary>
    /// <param name="enclosures">What to walk.</param>
    /// <param name="bound">The denominator bound to walk to.</param>
    /// <param name="atLeastSeconds">
    /// Keep going until this much wall clock has been spent, however few passes that is - zero for
    /// a bound whose predecessor has already warmed the code.
    /// </param>
    private static BigRational Fastest(
        IReadOnlyList<Approximation> enclosures, BigInteger bound, double atLeastSeconds)
    {
        long fastest = long.MaxValue;
        var spent = Stopwatch.StartNew();
        int pass = 0;

        while (pass < SamplePasses || spent.Elapsed.TotalSeconds < atLeastSeconds)
        {
            var clock = Stopwatch.StartNew();
            SurvivorReport.Of(enclosures, bound, TrackedCap);
            clock.Stop();

            fastest = Math.Min(fastest, clock.ElapsedTicks);
            pass++;
        }

        spent.Stop();

        return new BigRational(fastest, Stopwatch.Frequency);
    }

    /// <summary>Reads one argument as a whole number, or explains why it is not one.</summary>
    /// <param name="argument">The text as it arrived.</param>
    /// <param name="noun">What the position names, as "an order" or "an exponent".</param>
    /// <param name="example">An invocation that would have worked.</param>
    /// <param name="value">The number, when this returns true.</param>
    /// <param name="refusal">
    /// Set to the explanation on a failure, and left alone on a success - so the first unreadable
    /// argument is the one reported rather than the last one looked at.
    /// </param>
    /// <returns>True when the argument was a whole number.</returns>
    private static bool Whole(string argument, string noun, string example, out int value, ref string? refusal)
    {
        if (int.TryParse(argument, NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
        {
            return true;
        }

        refusal = string.Create(CultureInfo.InvariantCulture,
            $"'{argument}' is not {noun}. Give a whole number, as in '{example}'.");
        return false;
    }

    private static ChartCaption Caption(
        SurvivorRequest request,
        RatioRun run,
        IReadOnlyList<Approximation> enclosures,
        BigInteger bound)
    {
        Approximation final = enclosures[^1];

        return new ChartCaption(
            string.Create(CultureInfo.InvariantCulture, $"pi^{request.Order} / zeta({request.Order})"),
            string.Create(CultureInfo.InvariantCulture, $"MachinPi, EulerMaclaurinZeta({request.Order})"),
            "SurvivorSearch",
            string.Create(CultureInfo.InvariantCulture,
                $"{request.ScheduleLabel}, {run.Iterations.Count} targets, " +
                $"{enclosures.Count} distinct enclosures"),
            string.Create(CultureInfo.InvariantCulture,
                $"Q = {bound} = floor(eps^(-1/2)), DenominatorSweep's generic depth at " +
                $"eps = {Presentation.Magnitude(final.MaxError)}, the final enclosure's half-width"));
    }

    /// <summary>Writes what this run is about to do, before it costs anything.</summary>
    /// <param name="notes">Where the prose goes, which is stderr in a real run.</param>
    /// <param name="request">What was asked for.</param>
    /// <param name="columns">How many targets the schedule realised.</param>
    /// <remarks>
    /// Public on an internal class, unlike its siblings below, because the schedule line is a
    /// claim that can now be wrong: it named two constants while the span was fixed and names
    /// <paramref name="request"/> since the span became an argument. Handed a
    /// <see cref="StringWriter"/> it is a pure function of its arguments, so a test holds it to
    /// reporting the schedule that ran rather than the one that would have.
    /// </remarks>
    public static void Preamble(TextWriter notes, SurvivorRequest request, int columns)
    {
        ArgumentNullException.ThrowIfNull(notes);

        int order = request.Order;

        notes.WriteLine(string.Create(CultureInfo.InvariantCulture,
            $"pi^{order} / zeta({order}) - the survivor set, which is what section 2 step 6 says a run reports."));
        notes.WriteLine();
        notes.WriteLine(string.Create(CultureInfo.InvariantCulture,
            $"  providers   MachinPi, EulerMaclaurinZeta({order})   search  SurvivorSearch"));
        notes.WriteLine(string.Create(CultureInfo.InvariantCulture,
            $"  schedule    {request.ScheduleLabel}, {columns} targets"));
        notes.WriteLine();
        notes.WriteLine(order % 2 == 0
            ? "  an even order, so section 1 lists an exact answer for it and the survivor set can"
            : "  an odd order, so nobody knows whether the ratio is rational. The survivor set is");
        notes.WriteLine(order % 2 == 0
            ? "  be checked by eye. The value is not repeated here - section 1 owns that list."
            : "  the result, and a short one is a conjecture rather than a finding.");
        notes.WriteLine();
        notes.WriteLine("running the pipeline.");
    }

    private static void Sizing(
        TextWriter notes,
        RatioRun run,
        IReadOnlyList<Approximation> enclosures,
        BigInteger bound,
        WalkPrice price)
    {
        WalkSize size = Size(enclosures, bound);

        notes.WriteLine(string.Create(CultureInfo.InvariantCulture,
            $"  {run.Iterations.Count} targets realised {enclosures.Count} distinct enclosures; " +
            $"repeats are dropped, since intersecting an enclosure with itself refutes nothing."));
        notes.WriteLine(string.Create(CultureInfo.InvariantCulture,
            $"  Q = {bound}, derived as floor(eps^(-1/2)) from the final half-width " +
            $"{Presentation.Magnitude(enclosures[^1].MaxError)} - the depth a generic sweep reaches."));
        notes.WriteLine(string.Create(CultureInfo.InvariantCulture,
            $"  the walk is {size.Denominators:N0} denominators and {size.Candidates:N0} candidates " +
            $"over all {enclosures.Count} prefixes,"));
        notes.WriteLine(string.Create(CultureInfo.InvariantCulture,
            $"  of which the opening step is {Estimate([enclosures[0]], bound):N0}."));
        notes.WriteLine(string.Create(CultureInfo.InvariantCulture,
            $"  samples to q = {price.SmallSample} and q = {price.LargeSample} priced a denominator at " +
            $"{Presentation.Roughly(price.PerDenominator * Million)} microseconds"));
        notes.WriteLine(string.Create(CultureInfo.InvariantCulture,
            $"  and a candidate at {Presentation.Roughly(price.PerCandidate * Million)}, so the walk " +
            $"predicts {Presentation.Roughly(price.Seconds(size))} s against a budget of {BudgetSeconds}."));
        notes.WriteLine("  The price is this machine's; the answer is not.");
        notes.WriteLine();
        notes.WriteLine("intersecting, one enclosure at a time:");
    }

    /// <summary>What a generic target of this precision would have left standing anyway.</summary>
    /// <remarks>
    /// <b>Printed because <c>../SPEC-rational-ratio.md</c> § 1 asks for the figure beside every
    /// survivor set</b>, on the same rule that has always required the refute-and-bound caveat
    /// there. The prose below is that reporting; the rule it reports is § 1's and is not argued
    /// again here.
    /// </remarks>
    private static void Null(TextWriter notes, SurvivorReport report)
    {
        notes.WriteLine();
        notes.WriteLine("  That second column is the null: how many survivors of that denominator a");
        notes.WriteLine("  GENERIC target of this precision leaves by chance - 6*eps*q^2/pi^2, from");
        notes.WriteLine("  this run's own final half-width. Near 1 is noise. A real answer prices far");
        notes.WriteLine("  below it, because its denominator is small: an even order's 6/1 comes out");
        notes.WriteLine("  around 4.5e-9 at this precision, which is what a finding looks like from");
        notes.WriteLine("  the inside.");
        notes.WriteLine();
        notes.WriteLine(string.Create(CultureInfo.InvariantCulture,
            $"  Under the whole bound the figure is {Presentation.Roughly(report.ExpectedUnderBound.Value)}, " +
            $"and it is that at EVERY precision:"));
        notes.WriteLine("  Q is derived as eps^(-1/2), so the eps and the Q^2 cancel and 6/pi^2 is all");
        notes.WriteLine("  that is left. Running deeper does not thin the spurious survivors - it only");
        notes.WriteLine("  gives them larger denominators. So no depth of run makes a bare count into");
        notes.WriteLine("  evidence.");
        notes.WriteLine();
        notes.WriteLine("  The estimate prices one enclosure where a run intersects several, so it is");
        notes.WriteLine("  an upper bound on the null and errs towards calling a survivor unremarkable.");
    }

    private static void Epilogue(
        TextWriter notes, SurvivorReport report, int order, double seconds, double walkSeconds)
    {
        notes.WriteLine();
        notes.WriteLine(string.Create(CultureInfo.InvariantCulture,
            $"{report.Enclosures.Count} enclosures, {seconds:F2} s, of which the walk the budget " +
            $"prices was {walkSeconds:F2} s. The SVG is on stdout - redirect it."));
        notes.WriteLine();
        notes.WriteLine("WHAT THIS RUN ESTABLISHES");
        notes.WriteLine();

        if (report.SurvivorCount == 0)
        {
            notes.WriteLine(string.Create(CultureInfo.InvariantCulture,
                $"  Nothing of denominator at or below {report.DenominatorBound} survives every enclosure."));
            notes.WriteLine("  That is a refutation, and it is the strongest result this bench produces.");
        }
        else
        {
            bool one = report.SurvivorCount == 1;

            notes.WriteLine(string.Create(CultureInfo.InvariantCulture,
                $"  Of every rational whose denominator is at most {report.DenominatorBound}, exactly"));
            notes.WriteLine(string.Create(CultureInfo.InvariantCulture,
                $"  {report.SurvivorCount:N0} {(one ? "is" : "are")} consistent with the enclosures - " +
                $"{(one ? "this one, and no other" : "these, and no others")}:"));
            notes.WriteLine();
            notes.WriteLine("      survivor                        expected by chance at its own q");

            foreach (BigRational survivor in report.Survivors)
            {
                notes.WriteLine(string.Create(CultureInfo.InvariantCulture,
                    $"      {survivor.Numerator + "/" + survivor.Denominator,-30}  " +
                    $"{Presentation.Roughly(report.ExpectedAt(survivor.Denominator).Value)}"));
            }

            if (report.SurvivorCount > report.Survivors.Count)
            {
                notes.WriteLine(string.Create(CultureInfo.InvariantCulture,
                    $"      ... and {report.SurvivorCount - report.Survivors.Count:N0} more, not listed."));
            }

            Null(notes, report);
        }

        notes.WriteLine();
        notes.WriteLine("  The axis is denominators. That is SurvivorSearch's own axis - it takes a");
        notes.WriteLine("  largest denominator - and Q is set to the depth a generic DenominatorSweep");
        notes.WriteLine("  would have reached at this precision. That is section 2's sizing law for");
        notes.WriteLine("  that searcher, not a sweep this run performed: it performs none, since a");
        notes.WriteLine("  survivor set is what decides and the trend matrix is presentation.");
        notes.WriteLine();
        notes.WriteLine("WHAT IT DOES NOT");
        notes.WriteLine();
        notes.WriteLine("  Numerics refute a rational relation and bound the height of one. Nothing");
        notes.WriteLine("  finite establishes one. A surviving candidate poses a conjecture and is not");
        notes.WriteLine("  evidence; a deeper run refutes it and offers another.");

        if (order % 2 == 0)
        {
            notes.WriteLine();
            notes.WriteLine("  This order is even, so the answer is known and the set above is a control:");
            notes.WriteLine("  it says the machinery agrees with section 1, not that the machinery found");
            notes.WriteLine("  something. The odd orders are the question.");
        }
    }
}
