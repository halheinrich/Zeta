using System.Diagnostics;
using System.Globalization;

namespace HalHeinrich.Numerics.Experiments;

/// <summary>
/// The zeta(2) walk: <c>pi^2 / zeta(2) = 6</c> driven one provider step at a time, with pi, pi^2
/// and zeta(2) each shown at its own step with its own bound, the composed ratio, the blame
/// split, and a ladder of rival rationals being eliminated beside it.
/// </summary>
/// <remarks>
/// <para>
/// <b>This is the presentation's control, and it is here rather than in <c>Zeta.Tests</c> for a
/// reason worth stating.</b> Everything else this runner prints is <c>pi^3/zeta(3)</c>, whose
/// answer nobody knows, so nothing about that output can be checked by reading it. The walk's
/// answer is known - it is 6 - so a reader who can see the enclosure close on 6 and the rivals
/// fall away can trust the same columns when they are pointed at a target with no answer. It is
/// not a test: it prints a table and depends on wall-clock time, and
/// <c>../AGENTS.md</c> § Exactness discipline says a long run printing a table must not
/// masquerade as one. The controls that do gate CI live in <c>Zeta.Tests</c>.
/// </para>
/// <para>
/// <b>The search is the one thing this walk does not exercise, and that is said here rather than
/// left for a reader to infer.</b> The answer 6 is enclosed from the first column, so a search
/// ends at it immediately and refutes nothing. <see cref="HeightSweep"/> is used rather than the
/// default <see cref="DenominatorSweep"/> for exactly that reason: the sweep proposes <c>6/1</c>
/// at denominator 1 and stops after one candidate, where height order tries
/// <c>1/1 2/1 3/1 4/1 5/1 6/1</c>, which turns "are we trying other constants" into something
/// visible. Even so, six candidates ending at the right one is not a search being tested. The
/// rival ladder is what supplies the missing half - rationals the search never proposed, watched
/// as the enclosure refutes them.
/// </para>
/// <para>
/// <b>Two things this walk shows that are easy to mistake for defects.</b> Adjacent schedule
/// entries can produce identical rows, because the realised bound has already passed the next
/// target and the refiner correctly does nothing - the advance columns show <c>+0 +0</c> where
/// that happens. And <c>Pow</c> costs a decimal digit: pi pinned to 1e-9.00 gives pi^2 pinned to
/// 1e-8.20, which is why the two are separate columns rather than a remark.
/// </para>
/// </remarks>
internal static class RatioWalk
{
    /// <summary>The first exponent of the walk's schedule.</summary>
    public const int FirstExponent = 2;

    /// <summary>The last exponent of the walk's schedule.</summary>
    /// <remarks>
    /// Eleven columns, and the whole walk costs about 0.02 s: the answer is an exact rational of
    /// height 6, so every column's search ends after six candidates and the entire price is
    /// provider refinement. Depth is not a hazard here and no guard is needed - unlike the
    /// pi^3/zeta(3) run, whose sweep depth is set by a target nobody knows the structure of.
    /// </remarks>
    public const int LastExponent = 12;

    /// <summary>How many decimal places a value is rendered to before it is truncated with an ellipsis.</summary>
    private const int Places = 14;

    /// <summary>Runs the walk and prints it.</summary>
    /// <returns>Zero. Nothing here has a pass or a fail.</returns>
    public static int Walk()
    {
        IReadOnlyList<BigRational> schedule = TargetSchedule.Decades(FirstExponent, LastExponent);

        var clock = Stopwatch.StartNew();
        RatioRun run = RatioRun.Execute(
            new MachinPi(), 2, new EulerMaclaurinZeta(2), schedule, new HeightSweep());
        clock.Stop();

        TextWriter data = Console.Out;
        TextWriter notes = Console.Error;

        Preamble(notes, run, clock.Elapsed.TotalSeconds);
        WriteColumns(data, run);

        notes.WriteLine();
        notes.WriteLine(string.Create(CultureInfo.InvariantCulture,
            $"every candidate, admitted (+) or refuted (.) by each column. The first rows are the"));
        notes.WriteLine("search's own trail; the rest is a planted ladder of rivals, 6 + 1e-k for");
        notes.WriteLine(string.Create(CultureInfo.InvariantCulture,
            $"k = {FirstExponent}..{LastExponent}, which no search here proposed. Those reach the matrix through"));
        notes.WriteLine("RatioRun.Matrix's documented route for watching a candidate of one's own.");
        notes.WriteLine();

        TrendMatrix rivals = WithRivals(run);
        MatrixReport.WriteAdmissions(data, rivals, Places);

        Epilogue(notes, run, rivals);
        return 0;
    }

    /// <summary>
    /// The run's matrix with a ladder of rivals added, through the route
    /// <see cref="RatioRun.Matrix"/> documents.
    /// </summary>
    /// <param name="run">The completed run.</param>
    /// <returns>The matrix, one row per candidate the search found plus one per rung.</returns>
    /// <remarks>
    /// <para>
    /// A caller wanting to watch a rational no search produced adds it to any one iteration's
    /// contribution and rebuilds; the matrix fills that row for every column regardless, because
    /// a row is dense. The rungs go onto the first iteration, so their <c>FirstSeenAt</c> is 0 and
    /// the panel reads as a fixed ladder present from the start rather than as candidates
    /// arriving late.
    /// </para>
    /// <para>
    /// The enclosure each rung is measured against is the first column's. That choice is
    /// invisible in the output - <see cref="RationalCandidate"/> carries an enclosure only to
    /// answer <c>IsEnclosed</c> and its own distance bounds, and the panel asks
    /// <see cref="TrendMatrix.Ratios"/> column by column instead - but it has to be some
    /// enclosure, and the first is the one that admits the most.
    /// </para>
    /// </remarks>
    public static TrendMatrix WithRivals(RatioRun run)
    {
        ArgumentNullException.ThrowIfNull(run);

        Approximation first = run.Iterations[0].Enclosure.Ratio;

        RationalCandidate[] rungs =
        [
            .. Rivals().Select(rung => RationalCandidate.Against(rung, first)),
        ];

        TrendIteration[] withRungs =
        [
            .. run.Iterations.Select((iteration, column) => column == 0
                ? TrendIteration.Of(iteration.Enclosure.Ratio, [.. iteration.Candidates, .. rungs])
                : iteration.Trend),
        ];

        return TrendMatrix.Build(withRungs);
    }

    /// <summary>The ladder: <c>6 + 1e-k</c> for k across the schedule's own span.</summary>
    /// <remarks>
    /// Rationals near the answer that no search here proposes, so that the panel shows
    /// hypotheses being eliminated rather than the arithmetic converging. Held apart from
    /// <see cref="WithRivals"/> because a count of survivors is only meaningful with the set it
    /// counts named, and the matrix also holds every candidate the search found.
    /// </remarks>
    public static IReadOnlyList<BigRational> Rivals()
    {
        BigRational six = BigRational.FromInteger(6);

        return [.. Enumerable
            .Range(FirstExponent, LastExponent - FirstExponent + 1)
            .Select(k => six + TargetSchedule.Decade(k))];
    }

    private static void WriteColumns(TextWriter data, RatioRun run)
    {
        data.WriteLine(
            "  c  target   st  pi                st  zeta(2)           pi^2              " +
            "ratio             pi bound  pi^2 bnd  Pow   z bound   ratio bnd  prop bnd   " +
            "owns          next        simplest  tried");

        for (int column = 0; column < run.Iterations.Count; column++)
        {
            RatioIteration iteration = run.Iterations[column];
            RatioEnclosure enclosure = iteration.Enclosure;
            (BigRational power, BigRational divisor) = Presentation.BlameSplit(enclosure);

            // The owner's own share, not the base's. Labelling one operand and printing the
            // other's percentage beside it is the kind of mistake a reader corrects silently
            // and then stops trusting the column.
            string owns = power > divisor ? "pi" : power < divisor ? "zeta(2)" : "tie";
            BigRational owned = power >= divisor ? power : divisor;

            data.WriteLine(string.Create(CultureInfo.InvariantCulture,
                $"{column,3}  {Target(iteration),-7}  {iteration.BaseStep,2}  " +
                $"{Presentation.Earned(enclosure.PowerBase, Places),-16}  {iteration.DivisorStep,2}  " +
                $"{Presentation.Earned(enclosure.Divisor, Places),-16}  " +
                $"{Presentation.Earned(enclosure.Power, Places),-16}  " +
                $"{Presentation.Earned(enclosure.Ratio, Places),-16}  " +
                $"{Presentation.Magnitude(enclosure.PowerBase.MaxError),-8}  " +
                $"{Presentation.Magnitude(enclosure.Power.MaxError),-8}  " +
                $"{Presentation.PowCost(enclosure),-4}  " +
                $"{Presentation.Magnitude(enclosure.Divisor.MaxError),-8}  " +
                $"{Presentation.Magnitude(enclosure.Ratio.MaxError),-9}  " +
                $"{Presentation.Magnitude(enclosure.PropagatedError),-9}  " +
                $"{owns + " " + Presentation.Percent(owned),-13} " +
                $"{Advance(run, column),-11} " +
                $"{Simplest(iteration),-8}  {iteration.Candidates.Count,5}"));
        }
    }

    /// <summary>What the refiner actually did between this column and the next.</summary>
    /// <remarks>
    /// <b>Measured from the two columns rather than predicted from the rule.</b> Which provider
    /// gets advanced is <see cref="RatioRefiner"/>'s decision, and restating its comparison here
    /// would put one rule in two places - the defect <c>../AGENTS.md</c> § Writing code names.
    /// Reading the step counts instead reports what happened, which is also the stronger thing to
    /// show: it makes <c>+0 +0</c> visible where a target was already met, and it lets a reader
    /// check the <c>owns</c> column against the outcome rather than against a second copy of the
    /// code that produced it.
    /// </remarks>
    private static string Advance(RatioRun run, int column)
    {
        if (column + 1 >= run.Iterations.Count)
        {
            return "-";
        }

        RatioIteration here = run.Iterations[column];
        RatioIteration next = run.Iterations[column + 1];

        return string.Create(CultureInfo.InvariantCulture,
            $"pi+{next.BaseStep - here.BaseStep} z+{next.DivisorStep - here.DivisorStep}");
    }

    private static string Target(RatioIteration iteration) =>
        string.Create(CultureInfo.InvariantCulture,
            $"1e{Presentation.DecimalExponent(iteration.TargetError):F0}");

    private static string Simplest(RatioIteration iteration) =>
        string.Create(CultureInfo.InvariantCulture,
            $"{iteration.Simplest.Value.Numerator}/{iteration.Simplest.Value.Denominator}");

    private static void Preamble(TextWriter notes, RatioRun run, double seconds)
    {
        notes.WriteLine("the zeta(2) walk - pi^2 / zeta(2), whose answer is 6.");
        notes.WriteLine();
        notes.WriteLine(string.Create(CultureInfo.InvariantCulture,
            $"  providers   MachinPi, EulerMaclaurinZeta(2)   search  HeightSweep"));
        notes.WriteLine(string.Create(CultureInfo.InvariantCulture,
            $"  schedule    1e-{FirstExponent} .. 1e-{LastExponent}, {run.Iterations.Count} columns, {seconds:F3} s"));
        notes.WriteLine();
        notes.WriteLine("  st          the provider's own step index, base and divisor separately");
        notes.WriteLine("  pi, zeta(2) only the decimal digits that column's enclosure actually pins");
        notes.WriteLine("  pi^2        kept apart from pi because Pow costs a decimal digit - the Pow");
        notes.WriteLine("              column is how many, measured rather than asserted");
        notes.WriteLine("  ratio bnd   the coarsened bound a search is run against; prop bnd is the");
        notes.WriteLine("              exact propagated one the two shares explain");
        notes.WriteLine("  owns        which operand owns the larger share of the propagated error,");
        notes.WriteLine("              and that share as a fraction of the whole. RatioRefiner");
        notes.WriteLine("              advances the owner, a tie going to the base.");
        notes.WriteLine("  next        what it did advance, read from the following column's step");
        notes.WriteLine("              counts. +0 +0 means the target was already met.");
        notes.WriteLine();
        notes.WriteLine("  the ratio column reads ? in every row, and that is correct rather than a");
        notes.WriteLine("  defect. The answer is exactly 6, so every enclosure straddles 6.000000 -");
        notes.WriteLine("  its lower bound begins 5.9 and its upper 6.0 - and no decimal digit is");
        notes.WriteLine("  pinned however tight the interval gets. Watch the bound column instead.");
        notes.WriteLine("  A target that is not an integer, such as pi^3/zeta(3), fills this column");
        notes.WriteLine("  in the ordinary way.");
        notes.WriteLine();
    }

    private static void Epilogue(TextWriter notes, RatioRun run, TrendMatrix rivals)
    {
        RatioIteration last = run.Iterations[^1];

        notes.WriteLine();
        notes.WriteLine(string.Create(CultureInfo.InvariantCulture,
            $"the search proposed the same trail in every column: {Trail(run)}"));
        notes.WriteLine("that column is constant BY CONSTRUCTION and is not evidence. 6/1 is enclosed");
        notes.WriteLine("from the first column, so the search ends at it immediately and refutes");
        notes.WriteLine("nothing. This walk exercises composition and halting, never the search.");
        IReadOnlyList<BigRational> rungs = Rivals();
        IReadOnlyList<int> perTarget = MatrixReport.AdmissionCounts(rivals, rungs);

        notes.WriteLine();
        notes.WriteLine("RIVALS STILL ADMITTED - and the count depends on what is counted, so each");
        notes.WriteLine("sequence below says. All three read the same run.");
        notes.WriteLine();
        notes.WriteLine(string.Create(CultureInfo.InvariantCulture,
            $"  the 11 rungs, one count per target (1e-{FirstExponent} .. 1e-{LastExponent})"));
        notes.WriteLine(string.Create(CultureInfo.InvariantCulture,
            $"      {string.Join(" ", perTarget)}"));
        notes.WriteLine("  the same, one count per DISTINCT enclosure - adjacent targets that realise");
        notes.WriteLine("  the same bound collapse into one column");
        notes.WriteLine(string.Create(CultureInfo.InvariantCulture,
            $"      {string.Join(" ", Distinct(rivals, perTarget))}"));
        notes.WriteLine("  the same, over the sub-schedule 1e-2 1e-3 1e-4 1e-5 1e-6 1e-8 1e-10 1e-12");
        notes.WriteLine(string.Create(CultureInfo.InvariantCulture,
            $"      {string.Join(" ", Sampled(perTarget, [2, 3, 4, 5, 6, 8, 10, 12]))}"));
        notes.WriteLine();
        notes.WriteLine("  two of those have eight entries, which is exactly what would make them");
        notes.WriteLine("  look like the same measurement disagreeing. They are three readings of one");
        notes.WriteLine("  run, and a sequence quoted without its basis cannot be checked.");
        notes.WriteLine();
        notes.WriteLine("  Counting every row of the panel instead gives one more at each column, the");
        notes.WriteLine("  extra being 6/1 - which the search found and no enclosure ever refutes.");
        notes.WriteLine();
        notes.WriteLine("that sequence is the half the search cannot show: hypotheses being");
        notes.WriteLine("eliminated rather than arithmetic converging.");
        notes.WriteLine();

        // Which index the search enumerated decides what the terminating candidate PROVES, and
        // HeightSweep exposes it for exactly that reason. Above 1 it varies numerators, so the
        // bound is on height; below 1 it delegates to the denominator sweep and the bound is on
        // the denominator. Reporting the wrong one is not a wording slip - "every rational of
        // denominator at or below 1" is false here, since 6/1 is inside the enclosure.
        Approximation finalRatio = last.Enclosure.Ratio;
        bool byNumerator = HeightSweep.SearchesNumerators(finalRatio);
        System.Numerics.BigInteger reached = last.Simplest.Height;

        notes.WriteLine(byNumerator
            ? string.Create(CultureInfo.InvariantCulture,
                $"the search enumerated NUMERATORS, the ratio exceeding 1, and ended at height {reached}.")
            : string.Create(CultureInfo.InvariantCulture,
                $"the search enumerated DENOMINATORS and ended at {last.Simplest.Value.Denominator}."));
        notes.WriteLine(byNumerator
            ? "So every rational of height below that misses the final enclosure - which is a"
            : "So every rational of denominator below that misses the final enclosure - which is a");
        notes.WriteLine("refutation, and the only kind of result this bench produces. Here it is a");
        notes.WriteLine("weak one because the answer is small and was found at once; against a real");
        notes.WriteLine("target the bound is all there is.");
    }

    /// <summary>One count per distinct enclosure, adjacent duplicates collapsed.</summary>
    private static List<int> Distinct(TrendMatrix rivals, IReadOnlyList<int> perTarget)
    {
        var kept = new List<int>();

        for (int column = 0; column < perTarget.Count; column++)
        {
            if (column == 0 || rivals.Ratios[column] != rivals.Ratios[column - 1])
            {
                kept.Add(perTarget[column]);
            }
        }

        return kept;
    }

    /// <summary>The counts at the named exponents, for comparison with a sequence recorded on that basis.</summary>
    private static IReadOnlyList<int> Sampled(IReadOnlyList<int> perTarget, IReadOnlyList<int> exponents) =>
        [.. exponents.Select(exponent => perTarget[exponent - FirstExponent])];

    private static string Trail(RatioRun run) =>
        string.Join(" ", run.Iterations[0].Candidates.Select(
            candidate => string.Create(CultureInfo.InvariantCulture,
                $"{candidate.Value.Numerator}/{candidate.Value.Denominator}")));
}
