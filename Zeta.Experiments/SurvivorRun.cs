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
/// law rather than taste.</b> The expensive step is the collapse chart's first point, which counts
/// every rational of denominator at or below <c>Q</c> inside the <i>widest</i> enclosure - about
/// <c>h/eps</c> candidates, where <c>h</c> is the first target and <c>eps</c> the last. So
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
/// The estimate is <c>h*Q^2</c>, so a decade off the last end multiplies <c>Q^2</c> by ten while a
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

    /// <summary>The largest opening enumeration this command will pay for.</summary>
    /// <remarks>
    /// <para>
    /// Measured on this bench rather than derived, exactly as <c>target</c>'s ceiling is: the
    /// per-candidate cost is a greatest-common-divisor and one exact containment test against
    /// numerators of a few hundred digits, which no formula sizes usefully. At order 2 the opening
    /// step is about 1.0e6 candidates and the whole command takes a few seconds.
    /// </para>
    /// <para>
    /// The refusal quotes the estimate rather than a rule, because the estimate is computable
    /// before the money is spent - unlike a sweep's depth, which <c>target</c> cannot know in
    /// advance and so guards with a hard-coded exponent instead.
    /// </para>
    /// <para>
    /// <b>This counts candidates, and a candidate is not a fixed price.</b> Measured at order 3
    /// once the schedule became an argument: 4.6 microseconds each at <c>Q = 32,768</c>, about 20
    /// at <c>Q = 741,455</c> - the gcd and the containment test work on operands that grow with
    /// the denominator. So this number bounds the count and not the wait, and sixty million is a
    /// few seconds at the shallow end of the schedules a caller can now ask for and a long sit at
    /// the deep end. Whether it should be a time instead, or scale with <c>Q</c>, is
    /// <c>halheinrich/Math#64</c> leg 3's to rule on; what changed here is only that the budget
    /// became reachable by argument rather than by editing a constant.
    /// </para>
    /// </remarks>
    public const long Budget = 60_000_000;

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
            new MachinPi(), request.Order, new EulerMaclaurinZeta(request.Order), schedule);
        IReadOnlyList<Approximation> enclosures = SurvivorReport.Distinct(SurvivorReport.EnclosuresOf(run));

        BigInteger bound = SurvivorReport.DerivedBound(enclosures[^1]);
        string? tooDear = Refuse(enclosures[0], bound);
        if (tooDear is not null)
        {
            notes.WriteLine();
            notes.WriteLine(tooDear);
            return 2;
        }

        Sizing(notes, run, enclosures, bound);

        SurvivorReport report = SurvivorReport.Of(
            enclosures,
            bound,
            TrackedCap,
            (index, count) => notes.WriteLine(string.Create(CultureInfo.InvariantCulture,
                $"  enclosure {index}  half-width {Presentation.Magnitude(enclosures[index].MaxError),-9}  " +
                $"still standing {count:N0}")));

        clock.Stop();

        SurvivorChart.Write(Console.Out, report, Caption(request, run, enclosures, bound));
        Epilogue(notes, report, request.Order, clock.Elapsed.TotalSeconds);

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

    /// <summary>The reason this run's opening enumeration will not be paid for, or null when it will.</summary>
    /// <param name="widest">The widest enclosure, which the opening step enumerates.</param>
    /// <param name="denominatorBound">The derived bound.</param>
    /// <returns>The refusal, or null.</returns>
    /// <remarks>
    /// <para>
    /// The estimate is the count of rationals of denominator at or below <c>Q</c> inside an
    /// interval of half-width <c>h</c>: about <c>h*Q^2 + Q</c>, since each denominator contributes
    /// the integers in an interval of width <c>2*h*q</c> and one more for the endpoints. It ignores
    /// the reduction to lowest terms, which removes a constant fraction, so it overstates by
    /// something under a factor of two and never understates.
    /// </para>
    /// <para>
    /// A pure function of two values, which is what lets a test exercise the refusal without a
    /// run behind it.
    /// </para>
    /// </remarks>
    public static string? Refuse(Approximation widest, BigInteger denominatorBound)
    {
        BigInteger estimate = Estimate(widest, denominatorBound);

        return estimate > Budget
            ? string.Create(CultureInfo.InvariantCulture,
                $"Refusing this run: its opening step would enumerate about {estimate:N0} candidates, " +
                $"past the budget of {Budget:N0}.\n" +
                $"  the bound   Q = {denominatorBound}, derived as floor(eps^(-1/2)) from the final enclosure\n" +
                $"  the cost    the first point of the collapse chart counts every rational of\n" +
                $"              denominator at or below Q inside the WIDEST enclosure, which is about\n" +
                $"              h*Q^2 for a half-width h - so one more decade of schedule is ten times\n" +
                $"              this figure, not twice it\n" +
                $"Shorten the schedule rather than lowering Q: a hand-picked bound is what made the\n" +
                $"exploration's second graph misleading, and Q is derived here on purpose.")
            : null;
    }

    /// <summary>About how many candidates the opening enumeration walks.</summary>
    /// <param name="widest">The widest enclosure.</param>
    /// <param name="denominatorBound">The derived bound.</param>
    /// <returns>The estimate, truncated to an integer.</returns>
    /// <remarks>
    /// Exact rational arithmetic truncated at the end, not floating point: <c>Q</c> runs to
    /// millions and its square past a <see cref="double"/>'s integer range, where a figure quoted
    /// in a refusal would start being wrong in its leading digits.
    /// </remarks>
    public static BigInteger Estimate(Approximation widest, BigInteger denominatorBound)
    {
        BigRational bound = BigRational.FromInteger(denominatorBound);
        BigRational candidates = (widest.MaxError * bound * bound) + bound;

        return candidates.Numerator / candidates.Denominator;
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
            "DenominatorSweep",
            string.Create(CultureInfo.InvariantCulture,
                $"{request.ScheduleLabel}, {run.Iterations.Count} targets, " +
                $"{enclosures.Count} distinct enclosures"),
            string.Create(CultureInfo.InvariantCulture,
                $"Q = {bound} = floor(eps^(-1/2)), eps = {Presentation.Magnitude(final.MaxError)} " +
                $"the final enclosure's half-width"));
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
            $"  providers   MachinPi, EulerMaclaurinZeta({order})   search  DenominatorSweep"));
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
        BigInteger bound)
    {
        notes.WriteLine(string.Create(CultureInfo.InvariantCulture,
            $"  {run.Iterations.Count} targets realised {enclosures.Count} distinct enclosures; " +
            $"repeats are dropped, since intersecting an enclosure with itself refutes nothing."));
        notes.WriteLine(string.Create(CultureInfo.InvariantCulture,
            $"  Q = {bound}, derived as floor(eps^(-1/2)) from the final half-width " +
            $"{Presentation.Magnitude(enclosures[^1].MaxError)} - the depth a generic sweep reaches."));
        notes.WriteLine(string.Create(CultureInfo.InvariantCulture,
            $"  the opening step walks about {Estimate(enclosures[0], bound):N0} candidates and is " +
            $"almost all of the cost."));
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

    private static void Epilogue(TextWriter notes, SurvivorReport report, int order, double seconds)
    {
        notes.WriteLine();
        notes.WriteLine(string.Create(CultureInfo.InvariantCulture,
            $"{report.Enclosures.Count} enclosures, {seconds:F2} s. The SVG is on stdout - redirect it."));
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
        notes.WriteLine("  largest denominator - and not the run searcher's, though the two agree here:");
        notes.WriteLine("  Q is derived from DenominatorSweep's generic depth and the run swept with it.");
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
