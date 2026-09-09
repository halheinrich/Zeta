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
/// <b>The schedule is fixed, and the reason is a cost law rather than taste.</b> The expensive
/// step is the collapse chart's first point, which counts every rational of denominator at or
/// below <c>Q</c> inside the <i>widest</i> enclosure - about <c>h/eps</c> candidates, where
/// <c>h</c> is the first target and <c>eps</c> the last. So deepening the schedule by one decade
/// costs ten times as much, where <c>target</c>'s own schedule costs roughly double per decade.
/// A knob whose every notch is ten times the last is not a knob a caller should turn casually, so
/// the span is a constant here with its price stated, and <see cref="Refuse"/> checks the estimate
/// against a budget before spending it.
/// </para>
/// <para>
/// <b>No pass, no fail.</b> § Exactness discipline: a target with an unknown answer belongs in a
/// runnable project and its output is data. The exit code says only that the run completed.
/// </para>
/// </remarks>
internal static class SurvivorRun
{
    /// <summary>The first exponent of the schedule.</summary>
    public const int FirstExponent = 2;

    /// <summary>The last exponent of the schedule.</summary>
    public const int LastExponent = 8;

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
    /// </remarks>
    public const long Budget = 60_000_000;

    /// <summary>Runs the survivor report and writes it.</summary>
    /// <param name="arguments">Zero or one argument: the zeta order.</param>
    /// <returns>Zero when the run completed, 2 when the arguments or the estimated cost were refused.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="arguments"/> is null.</exception>
    public static int Run(string[] arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        TextWriter notes = Console.Error;

        if (arguments.Length > 1)
        {
            notes.WriteLine("survivors takes at most one argument, the order of zeta.");
            return 2;
        }

        int order = DefaultOrder;
        if (arguments.Length == 1 &&
            !int.TryParse(arguments[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out order))
        {
            notes.WriteLine(string.Create(CultureInfo.InvariantCulture,
                $"'{arguments[0]}' is not an order. Give a whole number, as in 'survivors 3'."));
            return 2;
        }

        string? refusal = RefuseOrder(order);
        if (refusal is not null)
        {
            notes.WriteLine(refusal);
            return 2;
        }

        IReadOnlyList<BigRational> schedule = TargetSchedule.Decades(FirstExponent, LastExponent);
        Preamble(notes, order, schedule.Count);

        var clock = Stopwatch.StartNew();
        RatioRun run = RatioRun.Execute(new MachinPi(), order, new EulerMaclaurinZeta(order), schedule);
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

        SurvivorChart.Write(Console.Out, report, Caption(order, run, enclosures, bound));
        Epilogue(notes, report, order, clock.Elapsed.TotalSeconds);

        return 0;
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

    private static ChartCaption Caption(
        int order,
        RatioRun run,
        IReadOnlyList<Approximation> enclosures,
        BigInteger bound)
    {
        Approximation final = enclosures[^1];

        return new ChartCaption(
            string.Create(CultureInfo.InvariantCulture, $"pi^{order} / zeta({order})"),
            string.Create(CultureInfo.InvariantCulture, $"MachinPi, EulerMaclaurinZeta({order})"),
            "DenominatorSweep",
            string.Create(CultureInfo.InvariantCulture,
                $"1e-{FirstExponent} .. 1e-{LastExponent}, {run.Iterations.Count} targets, " +
                $"{enclosures.Count} distinct enclosures"),
            string.Create(CultureInfo.InvariantCulture,
                $"Q = {bound} = floor(eps^(-1/2)), eps = {Presentation.Magnitude(final.MaxError)} " +
                $"the final enclosure's half-width"));
    }

    private static void Preamble(TextWriter notes, int order, int columns)
    {
        notes.WriteLine(string.Create(CultureInfo.InvariantCulture,
            $"pi^{order} / zeta({order}) - the survivor set, which is what section 2 step 6 says a run reports."));
        notes.WriteLine();
        notes.WriteLine(string.Create(CultureInfo.InvariantCulture,
            $"  providers   MachinPi, EulerMaclaurinZeta({order})   search  DenominatorSweep"));
        notes.WriteLine(string.Create(CultureInfo.InvariantCulture,
            $"  schedule    1e-{FirstExponent} .. 1e-{LastExponent}, {columns} targets"));
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
            notes.WriteLine("      " + string.Join("  ", report.Survivors.Select(
                survivor => string.Create(CultureInfo.InvariantCulture,
                    $"{survivor.Numerator}/{survivor.Denominator}"))));

            if (report.SurvivorCount > report.Survivors.Count)
            {
                notes.WriteLine(string.Create(CultureInfo.InvariantCulture,
                    $"      ... and {report.SurvivorCount - report.Survivors.Count:N0} more, not listed."));
            }
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
