namespace HalHeinrich.Numerics;

/// <summary>
/// One iteration of a <see cref="RatioRun"/>: the target it was driven to, the depths that took,
/// the enclosure that resulted, and the search run against it.
/// </summary>
/// <remarks>
/// <para>
/// A column of the run's <see cref="TrendMatrix"/>, with the bookkeeping the matrix deliberately
/// does not carry. <see cref="TrendIteration"/> holds a ratio and its candidates because that is
/// all a trend is read from; the depths and the target are what let a reader say <i>how</i> that
/// column was reached, and they belong to the run rather than to the trend.
/// </para>
/// <para>
/// <b>An iteration is not a verdict and does not end a run.</b> A candidate can hold steady
/// across two consecutive iterations and then move on - measured twice, in a positive control and
/// in the real target - so no property here reports stability and none should be added.
/// </para>
/// </remarks>
public sealed class RatioIteration
{
    private const string NoCandidatesMessage =
        "The search yielded no candidates, so there is no simplest one. An IRationalApproximator " +
        "must end with the first candidate the enclosure contains, so an implementation that " +
        "yields nothing is defective.";

    internal RatioIteration(
        BigRational targetError,
        int baseStep,
        int divisorStep,
        RatioEnclosure enclosure,
        TrendIteration trend)
    {
        TargetError = targetError;
        BaseStep = baseStep;
        DivisorStep = divisorStep;
        Enclosure = enclosure;
        Trend = trend;
    }

    /// <summary>Gets the target error this iteration was driven to.</summary>
    /// <remarks>
    /// The realised <c>Enclosure.Ratio.MaxError</c> is at or below this, and generally strictly
    /// below it, because the bound is coarsened to a power of two.
    /// </remarks>
    public BigRational TargetError { get; }

    /// <summary>Gets the base provider's step index at this iteration.</summary>
    public int BaseStep { get; }

    /// <summary>Gets the divisor provider's step index at this iteration.</summary>
    /// <remarks>
    /// Read against <see cref="BaseStep"/>, the gap between the two is the whole point of halting
    /// on the propagated error: the provider contributing more of it is driven further.
    /// </remarks>
    public int DivisorStep { get; }

    /// <summary>Gets the ratio this iteration computed, with its propagated error and that error's split.</summary>
    public RatioEnclosure Enclosure { get; }

    /// <summary>Gets this iteration's contribution to the run's <see cref="TrendMatrix"/>.</summary>
    public TrendIteration Trend { get; }

    /// <summary>Gets every candidate the search yielded, in the order it yielded them.</summary>
    /// <remarks>
    /// All of them, not just the terminating one. Each is a strict improvement on its
    /// predecessor, and the early low-height ones are exactly the rows a plateau is read from
    /// across the whole run - a matrix row is dense, so a candidate first surfaced late still
    /// carries a distance for every earlier column.
    /// </remarks>
    public IReadOnlyList<RationalCandidate> Candidates => Trend.Candidates;

    /// <summary>
    /// Gets the simplest rational this iteration's evidence permits: the last candidate the search
    /// yielded, which is the first one the enclosure contains.
    /// </summary>
    /// <exception cref="InvalidOperationException">The search yielded no candidates at all.</exception>
    /// <remarks>
    /// This is the least-denominator rational the evidence permits unconditionally, and the
    /// least-height one whenever the enclosure's <see cref="Approximation.MaxError"/> is below
    /// <c>1/2</c> - a condition this bench's enclosures are nowhere near violating, but one worth
    /// stating rather than relying on silently.
    /// </remarks>
    public RationalCandidate Simplest =>
        Candidates.Count > 0 ? Candidates[^1] : throw new InvalidOperationException(NoCandidatesMessage);
}
