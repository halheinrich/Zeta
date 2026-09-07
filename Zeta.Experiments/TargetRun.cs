using System.Diagnostics;
using System.Globalization;

namespace HalHeinrich.Numerics.Experiments;

/// <summary>
/// The target: <c>pi^3 / zeta(3)</c>, whose answer nobody knows. Run to a schedule of error
/// targets, swept, and reported as <c>../SPEC-rational-ratio.md</c> § 1's denominator bound.
/// </summary>
/// <remarks>
/// <para>
/// <b>No pass, no fail.</b> This is why the project exists: § 4 puts controls in
/// <c>Zeta.Tests</c> because their answers are known, and puts this here because its answer is
/// not. The exit code says only that the run completed.
/// </para>
/// <para>
/// <b>The search is <see cref="DenominatorSweep"/> and not <see cref="HeightSweep"/>, deliberately
/// and for the deliverable's sake.</b> § 1 asks for a denominator bound - every rational of
/// denominator at or below the last one searched misses the enclosure, <i>for any numerator</i> -
/// and says that restating it as a height bound is sound but throws the numerator-unbounded part
/// away. The sweep produces the denominator bound directly. Height order would produce a height
/// bound and cost a factor of the target's magnitude more, about 25.8x here, since the two
/// searches end at the same rational with one having reached its numerator and the other its
/// denominator. Height order earns its place in the zeta(2) walk, where the point is watching a
/// trail rather than proving a bound.
/// </para>
/// <para>
/// <b>Cost has two regimes and no law sizes a guard.</b> A generic target costs about
/// <c>eps^(-1/2)</c>; a target pinned just outside a low-height rational <c>p/q0</c> costs about
/// <c>1/(2*q0*eps)</c>, and at 1e-18 those differ by some 2.4e8. Which regime this target is in
/// cannot be known in advance, because it is the question being asked. So the ceiling below is a
/// hard number taken from a measurement, never a bound computed from the schedule - a computed
/// bound would have to trust the same extrapolation it exists to protect against.
/// </para>
/// </remarks>
internal static class TargetRun
{
    /// <summary>The first exponent of the schedule.</summary>
    public const int FirstExponent = 2;

    /// <summary>The last exponent when none is given.</summary>
    /// <remarks>
    /// Measured on this bench in Release: the whole run to here takes about 35.6 s, its last
    /// column sweeping to denominator 3,939,833. The column before it costs about 10.1 s at
    /// denominator 1,921,154, so the price roughly doubles per decade at this depth.
    /// </remarks>
    public const int DefaultLastExponent = 13;

    /// <summary>The deepest schedule this command will run.</summary>
    /// <remarks>
    /// <para>
    /// <b>A budget, not a correctness device.</b> Both searches provably terminate - an
    /// enclosure's value is a <see cref="BigRational"/> <c>n/d</c>, and at denominator <c>d</c>
    /// the candidate is the centre itself, which the enclosure contains - so nothing here is
    /// preventing a wrong answer. It is preventing a run that would finish long after anyone
    /// stopped waiting.
    /// </para>
    /// <para>
    /// Where the number comes from: 1e-13 is measured at 23.6 s for its column. One decade
    /// further is <i>extrapolated</i> from the generic law at about 1.4e7 denominators and 90 s,
    /// and 1e-16 at about 1.9e8 and twenty minutes - which is the region an umbrella probe of
    /// this target had to kill at 600 s. Fourteen is the last exponent whose extrapolated cost a
    /// person would sit through. The refusal quotes those figures rather than a formula.
    /// </para>
    /// </remarks>
    public const int MaxLastExponent = 14;

    /// <summary>Runs the target and prints it.</summary>
    /// <param name="arguments">Zero or one argument: the last exponent of the schedule.</param>
    /// <returns>Zero when the run completed, 2 when the arguments were refused.</returns>
    public static int Run(string[] arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        TextWriter notes = Console.Error;

        if (arguments.Length > 1)
        {
            notes.WriteLine("target takes at most one argument, the last exponent of the schedule.");
            return 2;
        }

        int last = DefaultLastExponent;
        if (arguments.Length == 1 &&
            !int.TryParse(arguments[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out last))
        {
            notes.WriteLine(string.Create(CultureInfo.InvariantCulture,
                $"'{arguments[0]}' is not an exponent. Give a whole number, as in 'target 12'."));
            return 2;
        }

        string? refusal = Refuse(last);
        if (refusal is not null)
        {
            notes.WriteLine(refusal);
            return 2;
        }

        IReadOnlyList<BigRational> schedule = TargetSchedule.Decades(FirstExponent, last);
        Preamble(notes, schedule, last);

        var clock = Stopwatch.StartNew();
        RatioRun run = RatioRun.Execute(new MachinPi(), 3, new BorweinZetaThree(), schedule);
        clock.Stop();

        TextWriter data = Console.Out;
        WriteColumns(data, run);

        notes.WriteLine();
        notes.WriteLine("the trend matrix - rows are candidates, columns iterations, cells the exact");
        notes.WriteLine("distance |a/b - x_k|. A row falling towards zero is the candidate the");
        notes.WriteLine("evidence favours; every other row settles at its true distance.");
        notes.WriteLine();

        MatrixReport.WriteDistances(data, run.Matrix);
        Epilogue(notes, run, clock.Elapsed.TotalSeconds);

        return 0;
    }

    /// <summary>The reason this schedule will not be run, or null when it will.</summary>
    /// <remarks>
    /// A pure function of the requested exponent, which is what lets it be held against a test
    /// without paying for a run. The refusal is legible on purpose: <c>#57</c>'s answer is that
    /// the sweep side of a composed target is not plannable from eps at all, so what a caller
    /// gets instead is a measured depth and a refusal that says what it would have cost.
    /// </remarks>
    public static string? Refuse(int lastExponent)
    {
        if (lastExponent <= FirstExponent)
        {
            return string.Create(CultureInfo.InvariantCulture,
                $"The schedule starts at 1e-{FirstExponent}, so the last exponent must be past " +
                $"{FirstExponent}. A single-column run shows no trend at all.");
        }

        if (lastExponent > MaxLastExponent)
        {
            return string.Create(CultureInfo.InvariantCulture,
                $"Refusing 1e-{lastExponent}: this command runs no deeper than 1e-{MaxLastExponent}.\n" +
                $"  measured on this bench   1e-12 swept 1,921,154 denominators in about 10.1 s\n" +
                $"                           1e-13 swept 3,939,833 in about 23.6 s\n" +
                $"  extrapolated beyond      1e-14 about 1.4e7 and 90 s; 1e-16 about 1.9e8 and\n" +
                $"                           twenty minutes, which is where a probe of this target\n" +
                $"                           had to be killed at 600 s\n" +
                $"The ceiling is a measured number rather than a computed bound, because sweep depth\n" +
                $"is set by the continued-fraction structure of a value this run exists to ask about.\n" +
                $"Raise MaxLastExponent deliberately, having decided what you will wait for.");
        }

        return null;
    }

    private static void WriteColumns(TextWriter data, RatioRun run)
    {
        data.WriteLine(
            "  c  target   base  div  ratio                    ratio bnd  prop bnd   " +
            "owns          depth       simplest                  height     tried");

        for (int column = 0; column < run.Iterations.Count; column++)
        {
            RatioIteration iteration = run.Iterations[column];
            RatioEnclosure enclosure = iteration.Enclosure;
            (BigRational power, BigRational divisor) = Presentation.BlameSplit(enclosure);
            BigRational value = iteration.Simplest.Value;

            string owns = power > divisor ? "pi^3" : power < divisor ? "zeta(3)" : "tie";

            data.WriteLine(string.Create(CultureInfo.InvariantCulture,
                $"{column,3}  1e{Presentation.DecimalExponent(iteration.TargetError),-5:F0}  " +
                $"{iteration.BaseStep,4}  {iteration.DivisorStep,3}  " +
                $"{Presentation.Earned(enclosure.Ratio, 16),-23}  " +
                $"{Presentation.Magnitude(enclosure.Ratio.MaxError),-9}  " +
                $"{Presentation.Magnitude(enclosure.PropagatedError),-9}  " +
                $"{owns + " " + Presentation.Percent(divisor > power ? divisor : power),-13} " +
                $"{value.Denominator,10}  {value.Numerator + "/" + value.Denominator,-24}  " +
                $"{iteration.Simplest.Height,-10}  " +
                $"{iteration.Candidates.Count,5}"));
        }
    }

    private static void Preamble(TextWriter notes, IReadOnlyList<BigRational> schedule, int last)
    {
        notes.WriteLine("pi^3 / zeta(3) - the target. Nobody knows whether it is rational.");
        notes.WriteLine();
        notes.WriteLine("  providers   MachinPi, BorweinZetaThree   search  DenominatorSweep");
        notes.WriteLine(string.Create(CultureInfo.InvariantCulture,
            $"  schedule    1e-{FirstExponent} .. 1e-{last}, {schedule.Count} columns"));
        notes.WriteLine();
        notes.WriteLine("  depth       the denominator the sweep reached, which is the bound this run");
        notes.WriteLine("              reports; owns is the share of the propagated error, and the");
        notes.WriteLine("              divisor owning most of it is what drives the two depths apart");
        notes.WriteLine();
        notes.WriteLine("running - the last column carries almost all the cost.");
    }

    private static void Epilogue(TextWriter notes, RatioRun run, double seconds)
    {
        RatioIteration last = run.Iterations[^1];
        BigRational value = last.Simplest.Value;

        notes.WriteLine();
        notes.WriteLine(string.Create(CultureInfo.InvariantCulture,
            $"{run.Iterations.Count} columns, {run.Matrix.Rows.Count} rows, {seconds:F2} s."));
        notes.WriteLine();
        notes.WriteLine("WHAT THIS RUN ESTABLISHES");
        notes.WriteLine();
        notes.WriteLine(string.Create(CultureInfo.InvariantCulture,
            $"  Every rational of denominator at or below {value.Denominator - 1} misses the final"));
        notes.WriteLine("  enclosure, for any numerator. That is the result, and it is a refutation.");
        notes.WriteLine();
        notes.WriteLine(string.Create(CultureInfo.InvariantCulture,
            $"  {value.Numerator}/{value.Denominator} survives, at height {last.Simplest.Height}. It is the simplest rational"));
        notes.WriteLine("  the evidence still permits and it is not evidence of anything: a run one");
        notes.WriteLine("  column deeper refutes it and offers another.");
        notes.WriteLine();
        notes.WriteLine("WHAT IT DOES NOT");
        notes.WriteLine();
        notes.WriteLine("  Nothing finite can show that a real number IS rational. A row falling");
        notes.WriteLine("  towards zero poses a conjecture and nothing stronger - and it is not even a");
        notes.WriteLine("  reliable sign of one: a near-miss, a target sitting just outside a simple");
        notes.WriteLine("  rational, produces a row that falls exactly as a genuine find would and");
        notes.WriteLine("  floors only below any precision this bench can reach. Nothing in the matrix");
        notes.WriteLine("  above distinguishes the two cases. Read the denominator bound, not the row.");
    }
}
