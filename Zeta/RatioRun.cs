namespace HalHeinrich.Numerics;

/// <summary>
/// A complete run of the method in <c>SPEC-rational-ratio.md</c> section 2: enclose two constants,
/// divide with propagation, sweep for rationals, iterate at successively tighter targets, and
/// assemble the trend matrix.
/// </summary>
/// <remarks>
/// <para>
/// The composition, and nothing else. Every contract wired here belongs to
/// <c>RationalApproximation</c> - <see cref="Approximation"/> and its arithmetic,
/// <see cref="IRealConstant"/>, <see cref="IRationalApproximator"/> and
/// <see cref="TrendMatrix"/> - and every constant to <c>RealConstants</c>. This type introduces no
/// enclosure, no search and no constant of its own.
/// </para>
/// <para>
/// <b>The run does not stop early, and there is no property here that says it should.</b> It is
/// driven to the fixed sequence of targets it was given, and the whole matrix is read afterwards.
/// Why a candidate holding steady is not evidence - including the measured runs that defeat an
/// "unchanged for k rounds" rule - is argued once, on <see cref="TrendMatrix"/> and in
/// <c>SPEC-rational-ratio.md</c> § 2, "Why survivors rather than a trend".
/// </para>
/// <para>
/// <b>What the answer means, and what decides it.</b> Not the rows of <see cref="Matrix"/>:
/// <see cref="SurvivorSearch"/> decides. Why refutation is a proof where a falling row is an
/// inference finite precision cannot support is argued once, on <see cref="SurvivorSearch"/> and
/// <see cref="TrendMatrix"/> and in that same section. What is local to this type is the
/// projection: that search takes enclosures of the unknown, and this run's are the
/// <see cref="RatioEnclosure.Ratio"/> of <see cref="RatioIteration.Enclosure"/> from each of
/// <see cref="Iterations"/>, searched under a denominator bound fixed in advance.
/// </para>
/// <para>
/// <b>A bound reported from a run must name the searcher that produced it.</b> The axis follows
/// the searcher, and which searcher bounds which axis - so why a bound that does not name one is
/// not checkable - is stated once in <c>SPEC-rational-ratio.md</c> § 1 and on
/// <see cref="HeightSweep"/>. What this type adds is that <see cref="Execute"/> takes the searcher
/// as an argument and defaults it to <see cref="DenominatorSweep"/>, so a reporting site that did
/// not choose one has a denominator bound to report.
/// </para>
/// </remarks>
public sealed class RatioRun
{
    private const string TargetsMustDecreaseMessage =
        "Target errors must be strictly decreasing. A repeated target produces a duplicate column " +
        "that is not fresh evidence, and a larger one cannot be honoured at all, since a bound " +
        "already proven tighter is not un-proven.";

    private const string LastTargetMustBePositiveMessage =
        "The final target error must be strictly positive. A propagated bound tends to zero " +
        "without reaching it, so a target of zero or less would never be met.";

    private readonly RatioIteration[] iterations;

    private RatioRun(int exponent, RatioIteration[] iterations, TrendMatrix matrix)
    {
        Exponent = exponent;
        this.iterations = iterations;
        Matrix = matrix;
    }

    /// <summary>Gets the power the base provider was raised to.</summary>
    public int Exponent { get; }

    /// <summary>Gets the run's iterations, in order. The matrix's columns, with their bookkeeping.</summary>
    public IReadOnlyList<RatioIteration> Iterations => iterations;

    /// <summary>Gets the trend matrix over the whole run: rows are candidates, columns iterations.</summary>
    /// <remarks>
    /// Built from every candidate every iteration surfaced. A caller wanting to watch a rational
    /// no search produced - a positive control such as 6, 90 or 945 - builds its own matrix from
    /// <see cref="Iterations"/>, adding that candidate to any one iteration's contribution; the
    /// matrix fills its row for every column regardless.
    /// </remarks>
    public TrendMatrix Matrix { get; }

    /// <summary>Runs the pipeline to each target error in turn.</summary>
    /// <param name="powerBase">The provider raised to the power - pi, in this bench.</param>
    /// <param name="exponent">The power to raise it to. At least 1.</param>
    /// <param name="divisor">The provider divided by - zeta(n), in this bench.</param>
    /// <param name="targetErrors">
    /// The target error for each iteration, strictly decreasing, the last strictly positive. May
    /// be empty, which is an honestly empty run rather than an error.
    /// </param>
    /// <param name="approximator">
    /// The search to run against each iteration's enclosure. Defaults to
    /// <see cref="DenominatorSweep"/>, the reference implementation.
    /// </param>
    /// <returns>The completed run.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="powerBase"/>, <paramref name="divisor"/> or <paramref name="targetErrors"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="exponent"/> is less than 1.</exception>
    /// <exception cref="ArgumentException"><paramref name="targetErrors"/> is not strictly decreasing, or its last element is not positive.</exception>
    /// <remarks>
    /// <para>
    /// The schedule of targets is the caller's, deliberately. How far a run should go, and in how
    /// many columns, is a property of the question being asked rather than of the pipeline;
    /// section 2 fixes only that the run is driven to a <i>fixed</i> target rather than stopped on
    /// what the output looks like.
    /// </para>
    /// <para>
    /// Both providers are refined incrementally across the whole run, so the cost of the last
    /// iteration is the depth it reaches and not that depth times the number of iterations.
    /// </para>
    /// </remarks>
    public static RatioRun Execute(
        IRealConstant powerBase,
        int exponent,
        IRealConstant divisor,
        IEnumerable<BigRational> targetErrors,
        IRationalApproximator? approximator = null)
    {
        ArgumentNullException.ThrowIfNull(targetErrors);

        BigRational[] targets = [.. targetErrors];
        string? complaint = FaultInTargets(targets);
        if (complaint is not null)
        {
            throw new ArgumentException(complaint, nameof(targetErrors));
        }

        IRationalApproximator search = approximator ?? new DenominatorSweep();
        var completed = new RatioIteration[targets.Length];

        using (var refiner = new RatioRefiner(powerBase, exponent, divisor))
        {
            for (int index = 0; index < targets.Length; index++)
            {
                refiner.RefineTo(targets[index]);

                RatioEnclosure enclosure = refiner.Current;
                TrendIteration trend = TrendIteration.Of(enclosure.Ratio, search.Search(enclosure.Ratio));

                completed[index] = new RatioIteration(
                    targets[index], refiner.BaseStep, refiner.DivisorStep, enclosure, trend);
            }
        }

        TrendMatrix matrix = TrendMatrix.Build(completed.Select(iteration => iteration.Trend));
        return new RatioRun(exponent, completed, matrix);
    }

    // A property of the sequence, not a restatement of the per-target rule RefineTo enforces:
    // strictly decreasing with a positive last element makes every element positive, and checking
    // it here means an ill-formed schedule fails before any refinement is paid for. Returns the
    // complaint rather than throwing so the caller can name its own parameter.
    private static string? FaultInTargets(BigRational[] targets)
    {
        if (targets.Length == 0)
        {
            return null;
        }

        for (int index = 1; index < targets.Length; index++)
        {
            if (targets[index] >= targets[index - 1])
            {
                return TargetsMustDecreaseMessage;
            }
        }

        return targets[^1].Sign <= 0 ? LastTargetMustBePositiveMessage : null;
    }
}
