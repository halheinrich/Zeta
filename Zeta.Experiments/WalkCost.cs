using System.Numerics;

namespace HalHeinrich.Numerics.Experiments;

/// <summary>
/// How much work a survivor walk is, counted as the two loops that produce it: the denominators
/// stepped through, and the candidates their intervals hold.
/// </summary>
/// <param name="Denominators">
/// How many times the outer loop turns. <c>SurvivorSearch</c> walks <c>1..Q</c> for every prefix
/// whatever its interval holds, so this is the prefix count times <c>Q</c> and is a floor on the
/// cost rather than a rounding term.
/// </param>
/// <param name="Candidates">
/// How many numerators the inner loop offers, summed over the prefixes: about <c>h*Q^2</c> for a
/// prefix of half-width <c>h</c>, since each denominator <c>q</c> contributes the integers in an
/// interval of width <c>2*h*q</c>.
/// </param>
/// <remarks>
/// <para>
/// <b>The two are counted apart because they do not cost the same, and not by a little.</b>
/// Measured here 2026-09-09 at order 3: about 16 microseconds a denominator against 1.5 a
/// candidate, a factor of ten. One turn of the outer loop rounds the seed's two endpoints -
/// rationals of a few hundred digits - against the denominator; one turn of the inner loop is a
/// greatest-common-divisor on operands no larger than <c>Q</c> and a containment test. A single
/// blended price is therefore a property of the <i>mix</i> as much as of the machine, and the mix
/// moves with the bound: at <c>Q = 1,024</c> the default schedule's walk is 58% outer loop and at
/// <c>Q = 11,585</c> it is 11%. A sample priced as one number and scaled by a count overstated the
/// real walk by a factor of three for exactly that reason, which is what this split removes.
/// </para>
/// <para>
/// <see cref="Total"/> is still the figure a report quotes, because "about 741,000 candidates" is
/// what a reader can check against the collapse chart. What it is not is a price.
/// </para>
/// </remarks>
internal readonly record struct WalkSize(BigInteger Denominators, BigInteger Candidates)
{
    /// <summary>Gets both loops together, which is the figure the reports quote.</summary>
    public BigInteger Total => Denominators + Candidates;
}

/// <summary>
/// What one turn of each of those loops costs on this machine, at this order, against these
/// enclosures - and the two sample bounds it was measured at.
/// </summary>
/// <param name="PerDenominator">Seconds for one turn of the outer loop. Non-negative.</param>
/// <param name="PerCandidate">Seconds for one turn of the inner loop. Non-negative.</param>
/// <param name="SmallSample">The smaller bound the calibration walked to.</param>
/// <param name="LargeSample">The larger bound the calibration walked to.</param>
/// <remarks>
/// <para>
/// <b>It carries the bounds it was measured at because a measured number without its basis is not
/// a claim</b> - <c>../AGENTS.md</c> § Reliability. The two prices are the solution of two timings
/// against two known walk sizes, and a reader who wants to know how much to trust them wants to
/// know how far the sample sat below the run.
/// </para>
/// <para>
/// <b>These are seconds and they are measured, which is why nothing downstream of a result may
/// touch them.</b> § Exactness discipline bans an empirical number from a computational path. A
/// cost guard is not one: it decides whether to start, and the run it starts reports the same
/// survivor set, the same <c>Q</c> and the same null however fast the machine was.
/// </para>
/// </remarks>
internal readonly record struct WalkPrice(
    BigRational PerDenominator,
    BigRational PerCandidate,
    BigInteger SmallSample,
    BigInteger LargeSample)
{
    /// <summary>How long a walk of this size is predicted to take.</summary>
    /// <param name="size">The walk to price.</param>
    /// <returns>The predicted seconds.</returns>
    public BigRational Seconds(WalkSize size) =>
        (PerDenominator * BigRational.FromInteger(size.Denominators)) +
        (PerCandidate * BigRational.FromInteger(size.Candidates));
}
