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

    /// <summary>How long a deep run's one sample walks before it starts believing its own clock.</summary>
    /// <remarks>
    /// <para>
    /// <b>Longer than the chart's, because the deep sample falls later and the chart's figure was
    /// read off the plateau before the fall.</b> Measured here 2026-09-10 in Release on
    /// <c>deep 3 4 11</c>, logging every pass of the sample: 16, 15, 15, 15, 15 microseconds a
    /// denominator through 0.43 s, then 7.0 at 0.52 s and between 6.9 and 8.7 for the next two and a
    /// half seconds. A tier promotion, in other words, arriving after the
    /// <see cref="SampleWarmUpSeconds"/> the chart uses. At that figure the sample priced the walk
    /// at 11 s against a realised 5.8 - 1.9 times over - and five flat passes gave no sign of it,
    /// which is the failure that constant's own remarks describe, at twice the size.
    /// </para>
    /// <para>
    /// A plausible reason it falls later, not a measured one: a chart sample walks the widest
    /// enclosure, whose many candidates drive every method in the walk through its call counts
    /// quickly, where a deep sample admits almost nothing and so exercises only the stepping.
    /// </para>
    /// <para>
    /// One second is about twice the measured fall. The two ways of getting it wrong are not
    /// symmetric, which is why it errs long: a sample read too early over-prices, which admits
    /// less and never more, while a second too many is a second on a run that exists to take
    /// minutes.
    /// </para>
    /// </remarks>
    public const double DeepSampleWarmUpSeconds = 1.0;

    private const string NoEnclosuresMessage =
        "A run with no enclosures intersects nothing and has no cost to estimate. " +
        "SurvivorReport.Of refuses the same list for the same reason.";

    private const string NegativePriceMessage =
        "A candidate cannot cost negative time. A price comes from Calibrate, which divides a " +
        "stopwatch reading by a candidate count and so cannot produce one.";

    /// <summary>Seconds to microseconds, for the one figure this command reports in them.</summary>
    internal static readonly BigRational Million = BigRational.FromInteger(1_000_000);

    /// <summary>The command a caller types for a mode.</summary>
    /// <param name="mode">The mode.</param>
    /// <returns><c>survivors</c> for the chart, <c>deep</c> for the single walk.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="mode"/> is not a defined mode.</exception>
    /// <remarks>
    /// Every message that shows an invocation to copy goes through this, so advice printed by a
    /// deep run names the command that would repeat it rather than the one that would draw charts.
    /// </remarks>
    public static string CommandFor(SurvivorMode mode) => mode switch
    {
        SurvivorMode.Chart => Program.SurvivorsCommand,
        SurvivorMode.Deep => Program.DeepCommand,
        _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "Not a survivor mode."),
    };

    /// <summary>Runs the survivor report and writes it.</summary>
    /// <param name="arguments">The command's arguments, as <see cref="Interpret"/> reads them.</param>
    /// <param name="mode">How to walk the enclosures, which is which command was typed.</param>
    /// <returns>Zero when the run completed, 2 when the arguments or the estimated cost were refused.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="arguments"/> is null.</exception>
    public static int Run(string[] arguments, SurvivorMode mode)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        TextWriter notes = Console.Error;

        string? refused = Interpret(arguments, mode, out SurvivorRequest request);
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

        BigInteger derived = SurvivorReport.DerivedBound(enclosures[^1]);
        string? unreachable = RefuseUnreachableControl(request.Order, derived, mode);
        if (unreachable is not null)
        {
            notes.WriteLine();
            notes.WriteLine(unreachable);
            return 2;
        }

        WalkPrice price = Calibrate(enclosures, derived, mode);
        SurvivorBound bound;

        if (mode == SurvivorMode.Deep)
        {
            // Ruling 5: a deep run is never refused for its cost - its Q comes down to what the
            // budget affords instead. Which makes the control check above necessary but no longer
            // sufficient, since a cap can fall below an answer the derived bound reached.
            bound = Afford(enclosures, derived, price);

            string? unaffordable = RefuseUnaffordableControl(request.Order, bound);
            if (unaffordable is not null)
            {
                notes.WriteLine();
                notes.WriteLine(unaffordable);
                return 2;
            }
        }
        else
        {
            bound = new SurvivorBound(derived, null);

            SurvivorRefusal? tooDear = Refuse(enclosures, derived, price);
            if (tooDear is not null)
            {
                notes.WriteLine();
                notes.WriteLine(tooDear.Value.Message);
                return 2;
            }
        }

        Sizing(notes, run, enclosures, bound, price, mode);

        var walk = Stopwatch.StartNew();
        SurvivorReport report = mode == SurvivorMode.Deep
            ? SurvivorReport.Deep(enclosures, bound, TrackedCap)
            : SurvivorReport.Of(
                enclosures,
                derived,
                TrackedCap,
                (index, count) => notes.WriteLine(string.Create(CultureInfo.InvariantCulture,
                    $"  enclosure {index}  half-width {Presentation.Magnitude(enclosures[index].MaxError),-9}  " +
                    $"still standing {count:N0}")));

        walk.Stop();
        clock.Stop();

        if (mode == SurvivorMode.Deep)
        {
            notes.WriteLine(string.Create(CultureInfo.InvariantCulture,
                $"  all {enclosures.Count} enclosures  still standing {report.SurvivorCount:N0}"));
        }

        SurvivorChart.Write(Console.Out, report, Caption(request, run, enclosures, bound));
        Epilogue(
            notes,
            report,
            request.Order,
            clock.Elapsed.TotalSeconds,
            new BigRational(walk.ElapsedTicks, Stopwatch.Frequency),
            price.Seconds(Size(enclosures, bound.Q, mode)));

        return 0;
    }

    /// <summary>What the command's arguments ask for, or the reason they are refused.</summary>
    /// <param name="arguments">
    /// Nothing, the order alone, or the order followed by both ends of the schedule -
    /// <c>survivors</c>, <c>survivors 3</c>, <c>survivors 3 2 12</c>, and the same after <c>deep</c>.
    /// </param>
    /// <param name="mode">
    /// Which command the arguments followed. It changes no rule here, only which command a refusal
    /// shows as the invocation to copy - the two share one grammar on purpose.
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
    public static string? Interpret(string[] arguments, SurvivorMode mode, out SurvivorRequest request)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        request = default;
        string command = CommandFor(mode);

        if (arguments.Length is 2 or > 3)
        {
            return string.Create(CultureInfo.InvariantCulture,
                $"{command} takes the order of zeta, and then both ends of the schedule or " +
                $"neither: '{command}', '{command} 3', or '{command} 3 2 12'. One exponent alone " +
                $"would leave it guessing which end of the schedule you meant.");
        }

        int order = DefaultOrder;
        int first = DefaultFirstExponent;
        int last = DefaultLastExponent;
        string? unreadable = null;
        string orderExample = command + " 3";
        string scheduleExample = command + " 3 2 12";

        if (arguments.Length > 0 && !Whole(arguments[0], "an order", orderExample, out order, ref unreadable))
        {
            return unreadable;
        }

        if (arguments.Length == 3 &&
            (!Whole(arguments[1], "an exponent", scheduleExample, out first, ref unreadable) ||
             !Whole(arguments[2], "an exponent", scheduleExample, out last, ref unreadable)))
        {
            return unreadable;
        }

        string? refusal = RefuseOrder(order) ?? RefuseSchedule(first, last);
        if (refusal is not null)
        {
            return refusal;
        }

        request = new SurvivorRequest(order, first, last, mode);
        return null;
    }

    /// <summary>The reason this order will not be run, or null when it will.</summary>
    /// <param name="order">The requested order.</param>
    /// <returns>The refusal, or null.</returns>
    /// <remarks>
    /// <para>
    /// <b>There is no ceiling here, and there was one until <c>halheinrich/Math#68</c>.</b>
    /// <c>MaxOrder = 16</c> refused anything higher on the ground that "section 1's positive
    /// controls stop there, so nothing above it can be checked against a known answer" - false at
    /// every even order without limit, as <see cref="EvenZetaRatio"/> now generates. It was a
    /// hand-typed list of eight wearing a mathematical reason, and it refused <i>odd</i> orders
    /// for an argument about even ones, which applied consistently would forbid order 3 - the
    /// project's whole research target.
    /// </para>
    /// <para>
    /// What replaces it is the principle <see cref="RefuseSchedule"/> was already arguing one
    /// method away: a run is refused for what it would cost, once that is known - which is
    /// <see cref="Refuse"/>'s question - or for a bound it cannot reach, which is
    /// <see cref="RefuseUnreachableControl"/>'s - and never for being high.
    /// </para>
    /// <para>
    /// A pure function of the argument, so it is held against tests without paying for a run - the
    /// same seam <c>target</c> uses, and for the same reason: an argument path reachable only by
    /// starting a long computation is a path nothing checks.
    /// </para>
    /// </remarks>
    public static string? RefuseOrder(int order) =>
        order < 2
            ? "The order must be at least 2. At s = 1 the series is the harmonic one and does " +
              "not converge, so there is no zeta(1) to divide by."
            : null;

    /// <summary>The reason this control cannot find its own answer, or null when it can.</summary>
    /// <param name="order">The requested order.</param>
    /// <param name="denominatorBound">The bound the run derived.</param>
    /// <param name="mode">Which command to show as the invocation that would reach.</param>
    /// <returns>The refusal, or null - always null for an odd order.</returns>
    /// <remarks>
    /// <para>
    /// <b>The defect <c>MaxOrder</c> was accidentally hiding.</b> An even order's answer is an
    /// exact rational of known denominator, so a run whose <c>Q</c> falls below that denominator
    /// is not searching a candidate set the answer is in - and reports an empty survivor set,
    /// which the epilogue calls "a refutation, and the strongest result this bench produces". It
    /// would be a false refutation of a true answer, the one direction
    /// <c>../SPEC-rational-ratio.md</c> § 2 forbids. Order 18 needs <c>Q >= 43,867</c> and the
    /// default schedule reaches 11,585, so <c>survivors 18</c> would have printed exactly that,
    /// with nothing on the page to say anything was wrong.
    /// </para>
    /// <para>
    /// <b>A refusal rather than a footnote, on § Exactness discipline's rule that a result is
    /// reported with its limitation.</b> A caveat under an empty set would still be an empty set
    /// on the chart, and the chart is what travels.
    /// </para>
    /// <para>
    /// <b>Silent on an odd order, which is not an oversight.</b> Nobody knows a denominator to
    /// compare against there - that is the question - so there is no bound this could check and no
    /// empty set it could call false. An odd run's empty set is a genuine refutation.
    /// </para>
    /// <para>
    /// <b>The rule is <see cref="SurvivorSearch.IsReachable"/>'s, and is not restated here.</b>
    /// <c>../SPEC-rational-ratio.md</c> § 2 states it once and <c>RationalApproximation</c>
    /// implements it once, so this method only chooses whether to ask - an even order, whose answer
    /// is known - and what to print when the answer is no. The denominator the message quotes is a
    /// fact about the answer, read off <see cref="EvenZetaRatio.Of"/>, and decides nothing.
    /// </para>
    /// <para>
    /// A pure function of two values, decidable before any search: the answer comes from
    /// <see cref="EvenZetaRatio"/> and <c>Q</c> from the enclosures the pipeline has already
    /// realised. It sits beside <see cref="Refuse"/> in <see cref="Run"/> and goes first, since a
    /// run that cannot find its answer should not be priced before it is turned down.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="denominatorBound"/> is negative and the order is even - refused by
    /// <see cref="SurvivorSearch.IsReachable"/>, which owns what a valid bound is.
    /// </exception>
    public static string? RefuseUnreachableControl(int order, BigInteger denominatorBound, SurvivorMode mode)
    {
        string command = CommandFor(mode);

        if (!EvenZetaRatio.IsKnown(order))
        {
            return null;
        }

        BigRational answer = EvenZetaRatio.Of(order);

        return !SurvivorSearch.IsReachable(answer, denominatorBound)
            ? string.Create(CultureInfo.InvariantCulture,
                $"Refusing this run: it could not find its own answer.\n" +
                $"  the answer   pi^{order}/zeta({order}) = {EvenZetaRatio.Format(order)}, exactly, from " +
                $"section 1's identity\n" +
                $"  the bound    Q = {denominatorBound}, derived from this schedule's final enclosure\n" +
                $"  the gap      the answer's denominator is {answer.Denominator}, so it is not in the candidate\n" +
                $"               set at all and the survivor set would come back EMPTY - a false\n" +
                $"               refutation of a true answer, which section 2 forbids in exactly\n" +
                $"               that direction\n" +
                $"Raise the LAST exponent to at least {ExponentReaching(order)}: that is the claim knob, and here " +
                $"the claim\n" +
                $"is what is short. '{command} {order} {DefaultFirstExponent} {ExponentReaching(order)}' derives a Q " +
                $"that reaches {answer.Denominator}.\n" +
                $"An odd order is not checked this way and cannot be: nobody knows a denominator to\n" +
                $"compare against, which is the question this bench exists to ask.")
            : null;
    }

    /// <summary>The shallowest last exponent whose target guarantees a bound reaching this order's answer.</summary>
    /// <param name="order">The order of zeta. Even, and at least two.</param>
    /// <returns>The exponent, as <c>10^-exponent</c>.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="order"/> has no known answer.</exception>
    /// <remarks>
    /// <para>
    /// <b>Searched, not solved.</b> The exponent is the smallest at or above
    /// <see cref="MinExponent"/> whose target derives a <c>Q</c> that
    /// <see cref="SurvivorSearch.IsReachable"/> accepts, so the rule is asked where it is
    /// implemented and the bound is derived where it is derived. Until <c>halheinrich/Math#64</c>'s
    /// last leg this counted the decimal digits of the answer's denominator squared - an inversion
    /// of the reachability rule, and so a second statement of it in arithmetic, which is the form
    /// the isolation rule went wrong in three times. The two agree at every even order from 2 to
    /// 120, checked in exact rationals by a scratchpad script on 2026-09-10; they part only at a
    /// denominator that is a power of ten, where the digit count named one exponent deeper than
    /// needed, and none of those orders has one.
    /// </para>
    /// <para>
    /// The enclosure each exponent is asked about is the answer at exactly the target's half-width,
    /// and <see cref="SurvivorReport.DerivedBound"/> reads only that half-width. It is a guarantee
    /// on the <i>target</i>, and a run realises something tighter, so the schedule this names
    /// always reaches - usually with a decade to spare. Naming the shallowest exponent that
    /// certainly works beats naming the one that probably does: the caller acts on this and waits
    /// for the answer.
    /// </para>
    /// </remarks>
    public static int ExponentReaching(int order)
    {
        BigRational answer = EvenZetaRatio.Of(order);
        BigInteger BoundAt(int candidate) =>
            SurvivorReport.DerivedBound(Approximation.Create(answer, TargetSchedule.Decade(candidate)));

        int exponent = MinExponent;
        while (!SurvivorSearch.IsReachable(answer, BoundAt(exponent)))
        {
            exponent++;
        }

        return exponent;
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
    /// <para>
    /// <b>A chart walk's guard only.</b> A deep run is never refused for its cost - ruling 5 on
    /// <c>halheinrich/Math#64</c> brings its <c>Q</c> down to what the budget affords instead, and
    /// <see cref="Afford"/> is that computation. The two are different answers to one budget
    /// because the two walks have different knobs: the chart's cost is dominated by its widest
    /// prefix, which the first exponent moves and <c>Q</c> does not, so a chart can be brought
    /// inside the budget without touching the claim; a deep walk costs <c>Q</c> denominators
    /// whatever the first exponent is, so the only thing that can bring it inside is a smaller
    /// <c>Q</c>, and computing that is better than advising it.
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
        RequirePrice(price);

        WalkSize size = Size(enclosures, denominatorBound, SurvivorMode.Chart);

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

    /// <summary>The bound a deep run walks to: the derived one, or less if that is all the budget buys.</summary>
    /// <param name="enclosures">Every enclosure the run will intersect, in order.</param>
    /// <param name="derivedBound">The bound the final enclosure's precision supports.</param>
    /// <param name="price">What a turn of each loop costs here, as <see cref="Calibrate"/> measures a deep walk.</param>
    /// <returns>
    /// Both bounds. The affordable one is the largest <c>q</c> whose deep walk is predicted to fit
    /// in <see cref="BudgetSeconds"/>, found whether or not it binds so that a run can print how
    /// much room it had - and null only when nothing the walk does is priced above zero, which
    /// makes every bound free.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="enclosures"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="enclosures"/> is empty.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Either half of <paramref name="price"/> is negative, or <paramref name="derivedBound"/> is.
    /// </exception>
    /// <remarks>
    /// <para>
    /// <b>Ruling 5 on <c>halheinrich/Math#64</c>.</b> <see cref="SurvivorBound"/> carries why a
    /// smaller <c>Q</c> is sound and what it does to the reading; this is only the arithmetic.
    /// </para>
    /// <para>
    /// <b>Doubling and then bisecting through <see cref="Size"/>, never inverting the cost law.</b>
    /// The prediction a run prints is <c>price.Seconds(Size(..., q, Deep))</c>, so the cap is the
    /// largest <c>q</c> at which exactly that expression is within budget - one computation rather
    /// than a closed form beside it that could drift, the same reason <see cref="SampleBound"/>
    /// doubles. It is monotone in <c>q</c>, since both counts are, and the search is exact: the
    /// result fits and one more does not.
    /// </para>
    /// <para>
    /// A pure function of its arguments, so a test can construct the case where the cap binds.
    /// Nothing in a real run reaches it until about <c>eps = 1e-15</c>, and a test that waited for
    /// a schedule to produce it would be a long run in a test project.
    /// </para>
    /// </remarks>
    public static SurvivorBound Afford(
        IReadOnlyList<Approximation> enclosures, BigInteger derivedBound, WalkPrice price)
    {
        RequireEnclosures(enclosures);
        RequirePrice(price);
        ArgumentOutOfRangeException.ThrowIfNegative(derivedBound);

        BigRational budget = BigRational.FromInteger(BudgetSeconds);
        bool Fits(BigInteger bound) => price.Seconds(Size(enclosures, bound, SurvivorMode.Deep)) <= budget;

        // Size's candidate count grows with q only when the narrowest enclosure has width, so a
        // walk costs something at a large enough q exactly when one of the two loops is priced.
        bool candidatesGrow = enclosures.Min(enclosure => enclosure.MaxError).Sign > 0;
        if (price.PerDenominator.IsZero && (price.PerCandidate.IsZero || !candidatesGrow))
        {
            return new SurvivorBound(derivedBound, null);
        }

        BigInteger high = BigInteger.One;
        while (Fits(high))
        {
            high *= 2;
        }

        // Fits(low) and not Fits(high) throughout. A bound of zero walks nothing and always fits.
        BigInteger low = high / 2;
        while (high - low > 1)
        {
            BigInteger middle = (low + high) / 2;

            if (Fits(middle))
            {
                low = middle;
            }
            else
            {
                high = middle;
            }
        }

        return new SurvivorBound(derivedBound, low);
    }

    /// <summary>The reason a capped control cannot find its own answer, or null when it can.</summary>
    /// <param name="order">The requested order.</param>
    /// <param name="bound">Where the run's bound came from.</param>
    /// <returns>The refusal, or null - always null for an odd order, and for a bound nothing capped.</returns>
    /// <remarks>
    /// <para>
    /// <b>The hole a cap opens in <see cref="RefuseUnreachableControl"/>.</b> That check reads the
    /// derived bound, and ruling 5 lets a deep run walk to less. So a derived bound can reach an
    /// even order's denominator while the budget's does not - and the walk then reports an EMPTY
    /// survivor set, a false refutation of a true answer, which is the one direction
    /// <c>../SPEC-rational-ratio.md</c> § 2 forbids. Both checks are needed, because they fail for
    /// different reasons and are fixed by different things.
    /// </para>
    /// <para>
    /// <b>A message of its own because the other's advice cannot work here.</b> Raising the last
    /// exponent raises the derived bound, which is not what is short; raising the first does not
    /// move a deep walk's cost at all. What is short is the budget, or the machine, and the message
    /// says so rather than naming a knob that is connected to nothing.
    /// </para>
    /// <para>
    /// The decision is <see cref="SurvivorSearch.IsReachable"/>'s, asked of the capped bound, for
    /// the reason <see cref="RefuseUnreachableControl"/> gives: the rule is implemented once, in
    /// <c>RationalApproximation</c>, and the denominator the message quotes only reports it.
    /// </para>
    /// </remarks>
    public static string? RefuseUnaffordableControl(int order, SurvivorBound bound)
    {
        if (!EvenZetaRatio.IsKnown(order) || !bound.IsCapped)
        {
            return null;
        }

        BigRational answer = EvenZetaRatio.Of(order);

        return !SurvivorSearch.IsReachable(answer, bound.Q)
            ? string.Create(CultureInfo.InvariantCulture,
                $"Refusing this run: the budget cannot reach its own answer.\n" +
                $"  the answer   pi^{order}/zeta({order}) = {EvenZetaRatio.Format(order)}, exactly, from " +
                $"section 1's identity\n" +
                $"  the bound    the schedule's precision supports Q = {bound.Derived}, which reaches the\n" +
                $"               answer's denominator {answer.Denominator} - but the budget of {BudgetSeconds} s affords a\n" +
                $"               deep walk only to Q = {bound.Q}, and there the answer is not in the\n" +
                $"               candidate set at all. The survivor set would come back EMPTY - a false\n" +
                $"               refutation of a true answer, which section 2 forbids in exactly that\n" +
                $"               direction\n" +
                $"No schedule change helps. A deep walk costs Q denominators whatever the FIRST exponent\n" +
                $"is, and the LAST only raises the Q the precision supports, which is not what is short.\n" +
                $"The price is this machine's, measured before the walk; a faster one reaches further.")
            : null;
    }

    /// <summary>Refuses a price with a negative half, which no stopwatch could have produced.</summary>
    private static void RequirePrice(WalkPrice price)
    {
        if (price.PerDenominator.Sign < 0 || price.PerCandidate.Sign < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(price), price, NegativePriceMessage);
        }
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
        RequireEnclosures(enclosures);

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

    /// <summary>The smaller of the two denominator bounds the calibration walks to, or a deep walk's one.</summary>
    /// <param name="enclosures">Every enclosure the run will intersect, in order.</param>
    /// <param name="denominatorBound">The derived bound the real walk will run to.</param>
    /// <param name="mode">Which walk the sample stands in for.</param>
    /// <returns>
    /// The least power of two at which the sampled walk reaches <see cref="SampleCandidates"/>, and
    /// never more than <paramref name="denominatorBound"/> - a run smaller than the sample is
    /// sampled by being run. For a chart that walk is the widest enclosure alone, and room is left
    /// for a second sample <see cref="SampleSpread"/> times larger; for a deep run it is the deep
    /// walk itself, sampled once.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="enclosures"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="enclosures"/> is empty.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="mode"/> is not a defined mode.</exception>
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
    public static BigInteger SampleBound(
        IReadOnlyList<Approximation> enclosures, BigInteger denominatorBound, SurvivorMode mode)
    {
        RequireEnclosures(enclosures);

        BigInteger bound = BigInteger.One;

        switch (mode)
        {
            case SurvivorMode.Chart:
                IReadOnlyList<Approximation> widest = Widest(enclosures);

                while (bound * SampleSpread < denominatorBound &&
                       Size(widest, bound, SurvivorMode.Chart).Total < SampleCandidates)
                {
                    bound *= 2;
                }

                break;

            case SurvivorMode.Deep:
                while (bound < denominatorBound &&
                       Size(enclosures, bound, SurvivorMode.Deep).Total < SampleCandidates)
                {
                    bound *= 2;
                }

                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(mode), mode, "Not a survivor mode.");
        }

        return BigInteger.Min(bound, denominatorBound);
    }

    /// <summary>Times short walks over the real enclosures and solves for what each loop costs.</summary>
    /// <param name="enclosures">Every enclosure the run will intersect, in order.</param>
    /// <param name="denominatorBound">The derived bound the real walk will run to.</param>
    /// <param name="mode">
    /// Which walk is being priced. A chart run is priced as the two-sample solve below describes; a
    /// deep run by <see cref="CalibrateDeep"/>, whose remarks say why it is one price and not two.
    /// </param>
    /// <returns>
    /// The two prices, with the bounds they were measured at. Both are zero when there is no
    /// sample to walk, which is a run with nothing to price rather than a free one.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="enclosures"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="enclosures"/> is empty.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="mode"/> is not a defined mode.</exception>
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
    /// <b>What that costs is an assumption, stated here rather than hidden, and it biases this
    /// guard towards admitting.</b> Both prices are measured against the <i>widest</i> enclosure's
    /// endpoints and then applied to every prefix - and a narrower enclosure carries larger
    /// endpoints, so the prefixes the sample never touches cost more than it charges for them.
    /// </para>
    /// <para>
    /// <b>What the prediction promises is a direction of error, not a band around the truth.</b>
    /// The sample is timed in a second and the walk runs for minutes, so the ratio of the two is
    /// set mostly by how the machine's load changes between them - which nothing here controls or
    /// observes. A sample timed under heavier load than the walk later meets over-prices, and the
    /// guard over-refuses: the safe error, seen at 1.8 on 2026-09-10 in a run of
    /// <c>survivors 3 4 11</c> with builds and tests sharing the machine, which predicted 250 s for
    /// a walk that took 143. A sample taken under lighter load under-prices, and the guard
    /// over-admits: the unsafe error, whose worst reading is 0.72 - at which a walk predicted at
    /// the whole <see cref="BudgetSeconds"/> would run about 417 s (300 / 0.72). Load rising partway
    /// through a walk could do worse than that, and nothing bounds it. The widest-enclosure bias
    /// above leans the same way, towards admitting.
    /// </para>
    /// <para>
    /// So this guard stops an accidental hour; it does not hold a run to its budget. The readings
    /// behind that, all of <c>survivors 3 4 11</c> on 2026-09-10: six by two sessions on a machine
    /// under roughly steady load read 0.72, 0.84, 0.85, 0.90, 0.92 and 1.2, with the prediction
    /// between 110 and 130 s and the walk between 122 and 155 s, and the seventh above read 1.8.
    /// Steady-load readings fitted with a range look like a property of the guard and are a
    /// property of the day; two such ranges were written down before the seventh broke them. The
    /// epilogue prints the realised walk beside the prediction for exactly that reason: the guard's
    /// own error is on the screen of every run rather than something a reader has to take on trust.
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
    public static WalkPrice Calibrate(
        IReadOnlyList<Approximation> enclosures, BigInteger denominatorBound, SurvivorMode mode)
    {
        switch (mode)
        {
            case SurvivorMode.Chart:
                break;

            case SurvivorMode.Deep:
                return CalibrateDeep(enclosures, denominatorBound);

            default:
                throw new ArgumentOutOfRangeException(nameof(mode), mode, "Not a survivor mode.");
        }

        IReadOnlyList<Approximation> widest = Widest(enclosures);
        BigInteger small = SampleBound(enclosures, denominatorBound, SurvivorMode.Chart);
        BigInteger large = BigInteger.Min(small * SampleSpread, denominatorBound);

        WalkSize lower = Size(widest, small, SurvivorMode.Chart);
        WalkSize upper = Size(widest, large, SurvivorMode.Chart);

        if (lower.Total.IsZero)
        {
            return new WalkPrice(BigRational.Zero, BigRational.Zero, small, large);
        }

        // The larger walk goes first and runs for at least SampleWarmUpSeconds, because the
        // runtime's optimised tier arrives on a timer as much as on a call count. By the time the
        // smaller walk is timed, everything under both of them is compiled the way the real walk
        // will find it - and the warm-up is not thrown away, it IS the larger measurement.
        BigRational upperSeconds = Fastest(widest, large, SampleWarmUpSeconds, SurvivorMode.Chart);
        BigRational lowerSeconds = Fastest(widest, small, 0, SurvivorMode.Chart);

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

    /// <summary>Times the deep walk itself at a smaller bound, and prices it as the one loop it is.</summary>
    /// <param name="enclosures">Every enclosure the run will intersect, in order.</param>
    /// <param name="denominatorBound">The derived bound the real walk will run to.</param>
    /// <returns>
    /// One price, charged to both loops, with the one bound it was measured at given as both
    /// sample bounds. Zero when there is no sample to walk.
    /// </returns>
    /// <remarks>
    /// <para>
    /// <b>Sampled from the walk it prices, and that is what removes the chart's bias rather than
    /// inheriting it.</b> The chart samples the widest enclosure alone and applies the prices to
    /// every prefix, so it is biased low - a narrower enclosure carries larger endpoints, and
    /// stepping a denominator rounds those endpoints. A deep walk is seeded from the narrowest
    /// enclosure, so a widest-enclosure sample would understate it by exactly the growth that
    /// matters most: over the 3.6 decades measured on <c>halheinrich/Math#64</c> the outer price
    /// rose 4.4-fold and the inner one 2.1-fold, and a deep walk is all outer loop. Here the sample
    /// <i>is</i> the production walk - every enclosure, the same seed - at a smaller bound.
    /// </para>
    /// <para>
    /// <b>One price, because a two-price solve has nothing to solve against.</b> At a derived bound
    /// the walk's inner loop holds about <c>h*Q^2 = 1</c> candidate, and a sample far below that
    /// bound holds fewer still; the chart's solve recovers the inner price as a difference between
    /// two samples' candidate counts, and a difference of one or two candidates is timing noise.
    /// So the sample's time is divided by its size and the result charged to both loops. What the
    /// inner loop is charged is immaterial for the same reason: it is multiplied by about one.
    /// </para>
    /// <para>
    /// The sample is walked for at least <see cref="DeepSampleWarmUpSeconds"/> and the fastest pass
    /// kept. That is longer than the chart's warm-up, and the constant carries the measurement that
    /// made it so; there is no second bound here to warm up behind.
    /// </para>
    /// </remarks>
    private static WalkPrice CalibrateDeep(IReadOnlyList<Approximation> enclosures, BigInteger denominatorBound)
    {
        BigInteger sample = SampleBound(enclosures, denominatorBound, SurvivorMode.Deep);
        WalkSize size = Size(enclosures, sample, SurvivorMode.Deep);

        if (size.Total.IsZero)
        {
            return new WalkPrice(BigRational.Zero, BigRational.Zero, sample, sample);
        }

        BigRational seconds = Fastest(enclosures, sample, DeepSampleWarmUpSeconds, SurvivorMode.Deep);
        BigRational price = seconds / BigRational.FromInteger(size.Total);

        return new WalkPrice(price, price, sample, sample);
    }

    /// <summary>How many candidates the walk considers, both loops together.</summary>
    /// <param name="enclosures">Every enclosure the run will intersect, in order.</param>
    /// <param name="denominatorBound">The derived bound.</param>
    /// <param name="mode">Which walk: every prefix, or the final one alone.</param>
    /// <returns>The estimate, truncated to an integer.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="enclosures"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="enclosures"/> is empty.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="mode"/> is not a defined mode.</exception>
    /// <remarks>
    /// The figure the reports quote, which is <see cref="WalkSize.Total"/>. What it is not is a
    /// price: see <see cref="WalkSize"/> for why the two loops are counted apart.
    /// </remarks>
    public static BigInteger Estimate(
        IReadOnlyList<Approximation> enclosures, BigInteger denominatorBound, SurvivorMode mode) =>
        Size(enclosures, denominatorBound, mode).Total;

    /// <summary>How much work the walk is, counted as its two loops.</summary>
    /// <param name="enclosures">Every enclosure the run will intersect, in order.</param>
    /// <param name="denominatorBound">The derived bound.</param>
    /// <param name="mode">Which walk: every prefix, or the final one alone.</param>
    /// <returns>The two counts, each truncated to an integer.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="enclosures"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="enclosures"/> is empty.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="mode"/> is not a defined mode.</exception>
    /// <remarks>
    /// <para>
    /// <b>A deep walk is the final prefix, and is priced as exactly that.</b> One walk over every
    /// enclosure, seeded from the narrowest, is what the chart's last prefix already is - so it
    /// costs <c>Q</c> turns of the outer loop and <c>h*Q^2</c> of the inner for the narrowest
    /// half-width <c>h</c>, and nothing for the prefixes before it. Pricing the chart's sum instead
    /// would refuse deep runs for the cost of the walk they exist to skip.
    /// </para>
    /// <para>
    /// <b>A chart counts every prefix, not just the first.</b> One prefix steps through the
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
    public static WalkSize Size(
        IReadOnlyList<Approximation> enclosures, BigInteger denominatorBound, SurvivorMode mode)
    {
        RequireEnclosures(enclosures);

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

        return mode switch
        {
            SurvivorMode.Chart => new WalkSize(denominatorBound * enclosures.Count, Truncate(candidates)),
            SurvivorMode.Deep => new WalkSize(denominatorBound, Truncate(narrowest * square)),
            _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "Not a survivor mode."),
        };
    }

    /// <summary>Refuses a list of enclosures that is null or holds none.</summary>
    private static void RequireEnclosures(IReadOnlyList<Approximation> enclosures)
    {
        ArgumentNullException.ThrowIfNull(enclosures);

        if (enclosures.Count == 0)
        {
            throw new ArgumentException(NoEnclosuresMessage, nameof(enclosures));
        }
    }

    /// <summary>A non-negative count, truncated to the integer below it.</summary>
    private static BigInteger Truncate(BigRational count) => count.Numerator / count.Denominator;

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
    /// <param name="mode">Which walk to time - the production one, at a smaller bound.</param>
    private static BigRational Fastest(
        IReadOnlyList<Approximation> enclosures, BigInteger bound, double atLeastSeconds, SurvivorMode mode)
    {
        long fastest = long.MaxValue;
        var spent = Stopwatch.StartNew();
        int pass = 0;

        while (pass < SamplePasses || spent.Elapsed.TotalSeconds < atLeastSeconds)
        {
            var clock = Stopwatch.StartNew();
            _ = mode == SurvivorMode.Deep
                ? SurvivorReport.Deep(enclosures, new SurvivorBound(bound, null), TrackedCap)
                : SurvivorReport.Of(enclosures, bound, TrackedCap);
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

    /// <summary>Where <c>Q</c> came from, in the one line the chart's caption carries.</summary>
    /// <param name="bound">The derived and affordable bounds.</param>
    /// <param name="finalHalfWidth">The final enclosure's half-width, which the derived bound came from.</param>
    /// <returns>The line.</returns>
    /// <remarks>
    /// <para>
    /// <b>On the picture rather than only in the terminal</b>, because a chart travels: the
    /// exploration's second graph misled precisely because the cap it was drawn under was not on
    /// it. So a capped bound says CAPPED and gives both numbers, and an uncapped deep bound still
    /// says what the budget would have afforded - which is how far the run was from its limit.
    /// </para>
    /// <para>
    /// A pure function, so what the caption claims about a cap is held by a test rather than
    /// seen only when a run deep enough to trigger it is paid for.
    /// </para>
    /// </remarks>
    public static string BoundLine(SurvivorBound bound, BigRational finalHalfWidth)
    {
        string depth = string.Create(CultureInfo.InvariantCulture,
            $"DenominatorSweep's generic depth at eps = {Presentation.Magnitude(finalHalfWidth)}, the final " +
            $"enclosure's half-width");

        if (bound.IsCapped)
        {
            return string.Create(CultureInfo.InvariantCulture,
                $"Q = {bound.Q}, CAPPED by the {BudgetSeconds} s budget below floor(eps^(-1/2)) = " +
                $"{bound.Derived}, {depth}");
        }

        string derived = string.Create(CultureInfo.InvariantCulture,
            $"Q = {bound.Q} = floor(eps^(-1/2)), {depth}");

        return bound.Affordable is { } affordable
            ? string.Create(CultureInfo.InvariantCulture,
                $"{derived}; the budget would have afforded {affordable}")
            : derived;
    }

    private static ChartCaption Caption(
        SurvivorRequest request,
        RatioRun run,
        IReadOnlyList<Approximation> enclosures,
        SurvivorBound bound)
    {
        Approximation final = enclosures[^1];

        return new ChartCaption(
            string.Create(CultureInfo.InvariantCulture, $"pi^{request.Order} / zeta({request.Order})"),
            string.Create(CultureInfo.InvariantCulture, $"MachinPi, EulerMaclaurinZeta({request.Order})"),
            request.Mode == SurvivorMode.Deep
                ? "SurvivorSearch, once over every enclosure (deep)"
                : "SurvivorSearch",
            string.Create(CultureInfo.InvariantCulture,
                $"{request.ScheduleLabel}, {run.Iterations.Count} targets, " +
                $"{enclosures.Count} distinct enclosures"),
            BoundLine(bound, final.MaxError));
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

        if (request.Mode == SurvivorMode.Deep)
        {
            notes.WriteLine("  walk        deep - ONE pass over every enclosure. The same survivor set as");
            notes.WriteLine("              survivors, without the collapse chart or the nearest excluded,");
            notes.WriteLine("              traded for reach.");
        }

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
        SurvivorBound bound,
        WalkPrice price,
        SurvivorMode mode)
    {
        WalkSize size = Size(enclosures, bound.Q, mode);

        notes.WriteLine(string.Create(CultureInfo.InvariantCulture,
            $"  {run.Iterations.Count} targets realised {enclosures.Count} distinct enclosures; " +
            $"repeats are dropped, since intersecting an enclosure with itself refutes nothing."));
        notes.WriteLine(string.Create(CultureInfo.InvariantCulture,
            $"  Q = {bound.Derived}, derived as floor(eps^(-1/2)) from the final half-width " +
            $"{Presentation.Magnitude(enclosures[^1].MaxError)} - the depth a generic sweep reaches."));

        if (mode == SurvivorMode.Deep)
        {
            notes.WriteLine(string.Create(CultureInfo.InvariantCulture,
                $"  a sample of the deep walk itself to q = {price.SmallSample} priced a denominator at " +
                $"{Presentation.Roughly(price.PerDenominator * Million)} microseconds -"));
            notes.WriteLine("  one price, since there is almost nothing else in it.");
            notes.WriteLine(string.Create(CultureInfo.InvariantCulture,
                $"  Q = {(bound.Affordable is { } affordable ? affordable.ToString(CultureInfo.InvariantCulture) : "unbounded")}" +
                $" is what the budget of {BudgetSeconds} s affords at that price, so this run walks to"));
            notes.WriteLine(bound.IsCapped
                ? string.Create(CultureInfo.InvariantCulture,
                    $"  Q = {bound.Q} - CAPPED below the derived bound. Ruling 5: claiming less than the precision")
                : string.Create(CultureInfo.InvariantCulture,
                    $"  Q = {bound.Q}, the derived bound, which the budget affords."));

            if (bound.IsCapped)
            {
                notes.WriteLine("  supports is always sound, and the epilogue says what the cap does to the reading.");
            }

            WalkSize chart = Size(enclosures, bound.Q, SurvivorMode.Chart);

            notes.WriteLine(string.Create(CultureInfo.InvariantCulture,
                $"  the walk is ONE pass over all {enclosures.Count} enclosures, seeded from the narrowest: " +
                $"{size.Denominators:N0} denominators"));
            notes.WriteLine(string.Create(CultureInfo.InvariantCulture,
                $"  and {size.Candidates:N0} candidates, where the chart would walk {chart.Denominators:N0} " +
                $"and {chart.Candidates:N0}."));
            notes.WriteLine(string.Create(CultureInfo.InvariantCulture,
                $"  It predicts {Presentation.Roughly(price.Seconds(size))} s. The price is this machine's; " +
                $"the answer is not."));
            notes.WriteLine();
            notes.WriteLine("intersecting, every enclosure at once:");
            return;
        }

        notes.WriteLine(string.Create(CultureInfo.InvariantCulture,
            $"  the walk is {size.Denominators:N0} denominators and {size.Candidates:N0} candidates " +
            $"over all {enclosures.Count} prefixes,"));
        notes.WriteLine(string.Create(CultureInfo.InvariantCulture,
            $"  of which the opening step is {Estimate([enclosures[0]], bound.Q, SurvivorMode.Chart):N0}."));
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

        if (report.Bound.IsCapped)
        {
            // Ruling 5's reading, which SurvivorBound argues: the null falls with the cap.
            notes.WriteLine(string.Create(CultureInfo.InvariantCulture,
                $"  Under the whole bound the figure is {Presentation.Roughly(report.ExpectedUnderBound.Value)} - " +
                $"BELOW the 6/pi^2 = 0.61 a derived"));
            notes.WriteLine(string.Create(CultureInfo.InvariantCulture,
                $"  bound gives at every precision, because the budget capped Q at {report.Bound.Q} under the"));
            notes.WriteLine(string.Create(CultureInfo.InvariantCulture,
                $"  {report.Bound.Derived} this precision supports. So a survivor here is STRONGER evidence than"));
            notes.WriteLine("  the same survivor under the derived bound: fewer spurious ones were possible.");
        }
        else
        {
            notes.WriteLine(string.Create(CultureInfo.InvariantCulture,
                $"  Under the whole bound the figure is {Presentation.Roughly(report.ExpectedUnderBound.Value)}, " +
                $"and it is that at EVERY precision:"));
            notes.WriteLine("  Q is derived as eps^(-1/2), so the eps and the Q^2 cancel and 6/pi^2 is all");
            notes.WriteLine("  that is left. Running deeper does not thin the spurious survivors - it only");
            notes.WriteLine("  gives them larger denominators. So no depth of run makes a bare count into");
            notes.WriteLine("  evidence.");
        }

        notes.WriteLine();
        notes.WriteLine("  The estimate prices one enclosure where a run intersects several, so it is");
        notes.WriteLine("  an upper bound on the null and errs towards calling a survivor unremarkable.");
    }

    /// <summary>What the run established, what it did not, and how well its cost was predicted.</summary>
    /// <remarks>
    /// The predicted-to-realised line is printed for both walks so the two cost models can be read
    /// side by side from real runs. The chart's prices every prefix at the widest enclosure's
    /// endpoints and has been measured anywhere from 0.6 to 1.2 of its walk; the deep walk's is
    /// sampled from the walk itself, and ran 0.89 to 0.94 across four schedules on 2026-09-10.
    /// </remarks>
    private static void Epilogue(
        TextWriter notes,
        SurvivorReport report,
        int order,
        double seconds,
        BigRational walkSeconds,
        BigRational predictedSeconds)
    {
        notes.WriteLine();
        notes.WriteLine(string.Create(CultureInfo.InvariantCulture,
            $"{report.Enclosures.Count} enclosures, {seconds:F2} s, of which the walk the budget " +
            $"prices was {Presentation.ToDecimal(walkSeconds, 2)} s. The SVG is on stdout - redirect it."));

        if (walkSeconds.Sign > 0)
        {
            notes.WriteLine(string.Create(CultureInfo.InvariantCulture,
                $"The guard predicted {Presentation.Roughly(predictedSeconds)} s for that walk: " +
                $"{Presentation.Roughly(predictedSeconds / walkSeconds)} of what it took."));
        }

        notes.WriteLine();
        notes.WriteLine("WHAT THIS RUN ESTABLISHES");
        notes.WriteLine();

        if (report.SurvivorCount == 0)
        {
            notes.WriteLine(string.Create(CultureInfo.InvariantCulture,
                $"  Nothing of denominator at or below {report.DenominatorBound} survives every enclosure."));
            notes.WriteLine("  That is a refutation, and it is the strongest result this bench produces.");

            if (report.Bound.IsCapped)
            {
                notes.WriteLine(string.Create(CultureInfo.InvariantCulture,
                    $"  But a NARROWER one than this precision supports: the budget capped Q at {report.Bound.Q},"));
                notes.WriteLine(string.Create(CultureInfo.InvariantCulture,
                    $"  so denominators from there to the derived {report.Bound.Derived} were never tried."));
            }
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

        if (report.Bound.IsCapped)
        {
            notes.WriteLine("  largest denominator - and Q is the smaller of two figures: the depth a");
            notes.WriteLine("  generic DenominatorSweep would have reached at this precision, and what the");
            notes.WriteLine("  budget affords. The budget's was smaller. The first is section 2's sizing");
        }
        else
        {
            notes.WriteLine("  largest denominator - and Q is set to the depth a generic DenominatorSweep");
            notes.WriteLine("  would have reached at this precision. That is section 2's sizing law for");
        }

        notes.WriteLine(report.Bound.IsCapped
            ? "  law for that searcher, not a sweep this run performed: it performs none, since a"
            : "  that searcher, not a sweep this run performed: it performs none, since a");
        notes.WriteLine("  survivor set is what decides and the trend matrix is presentation.");
        notes.WriteLine();
        notes.WriteLine("WHAT IT DOES NOT");
        notes.WriteLine();
        notes.WriteLine("  Numerics refute a rational relation and bound the height of one. Nothing");
        notes.WriteLine("  finite establishes one. A surviving candidate poses a conjecture and is not");
        notes.WriteLine("  evidence; a deeper run refutes it and offers another.");

        if (report.Omitted.Count > 0)
        {
            notes.WriteLine();
            notes.WriteLine("  Nor does it draw everything survivors would. This run walked once, with every");
            notes.WriteLine("  enclosure, and the SVG says the same of each panel it lacks:");

            foreach (SurvivorPanel panel in report.Omitted)
            {
                notes.WriteLine();
                notes.WriteLine("    " + SurvivorChart.PanelName(panel).ToUpperInvariant() + " - not drawn.");

                foreach (string line in SurvivorChart.WhyNotDrawn(panel))
                {
                    notes.WriteLine("    " + line);
                }
            }
        }

        if (order % 2 == 0)
        {
            notes.WriteLine();
            notes.WriteLine("  This order is even, so the answer is known and the set above is a control:");
            notes.WriteLine("  it says the machinery agrees with section 1, not that the machinery found");
            notes.WriteLine("  something. The odd orders are the question.");
        }
    }
}
