namespace HalHeinrich.Numerics.Experiments;

/// <summary>
/// A searcher that proposes nothing, for a run whose result is a survivor set and whose trend
/// matrix nobody reads.
/// </summary>
/// <remarks>
/// <para>
/// <b>This exists because <c>survivors</c> was buying a <see cref="DenominatorSweep"/> at every
/// target and throwing the answer away</b> - ruled on <c>halheinrich/Math#64</c> 2026-09-09, after
/// two wrong readings of the same measurement the same day. <see cref="RatioRun.Execute"/> calls
/// the searcher once per target to build the matrix; <see cref="SurvivorReport.EnclosuresOf"/>
/// takes <see cref="RatioIteration.Enclosure"/> and nothing else. <c>../SPEC-rational-ratio.md</c>
/// § 2 step 6 made the survivor set the result and the matrix presentation on 2026-09-08, and the
/// sweep has been bought and discarded ever since.
/// </para>
/// <para>
/// <b>Parity was the tell, and it is exact.</b> <see cref="DenominatorSweep"/> stops at the first
/// rational inside the enclosure. For an even order that rational is the answer itself, at
/// denominator 1; for an odd order nothing of small denominator is inside, so the sweep runs to
/// about <c>eps^(-1/2)</c>. Measured at <c>1e-2 .. 1e-14</c>: orders 3, 5 and 7 took 159 s, over
/// 400 s and over 400 s, while orders 4, 6, 8 and 10 took a second or less - and order 10 reaches
/// the same precision and the same bound that cost order 3 its 159 s. So it was never overshoot
/// and never the order; it was a search nobody had asked for.
/// </para>
/// <para>
/// <b>What this is not is a rational approximator.</b> <see cref="IRationalApproximator"/>'s
/// contract has a sequence end with the first candidate its enclosure contains, and an empty
/// sequence has no such candidate - it satisfies the obligation vacuously rather than honestly.
/// That is the whole of what a null object is, and it is named for the absence rather than dressed
/// up as a search: a caller reaching for this is saying it wants no matrix, not that it wants a
/// cheap one. <see cref="TrendMatrix.Build"/> over these iterations has no rows, which is the
/// truthful matrix for a run that searched nothing.
/// </para>
/// <para>
/// The trend path stays. <c>walk</c> and <c>target</c> both report a matrix and § 2 keeps it as
/// presentation; what changed is only that the command whose output is a survivor set stops paying
/// for one.
/// </para>
/// </remarks>
internal sealed class NoSearch : IRationalApproximator
{
    /// <summary>Proposes nothing, at any cost, for any enclosure.</summary>
    /// <param name="enclosure">Ignored. Nothing here looks at it.</param>
    /// <returns>An empty sequence.</returns>
    public IEnumerable<RationalCandidate> Search(Approximation enclosure) => [];
}
