namespace HalHeinrich.Numerics;

/// <summary>
/// One evaluation of <c>base^exponent / divisor</c> as an enclosure, together with the split of
/// the propagated error between the two operands that produced it.
/// </summary>
/// <remarks>
/// <para>
/// This is step 2 of the method in <c>SPEC-rational-ratio.md</c>: enclose, then divide
/// <i>propagating the bound</i>. Every piece of arithmetic here belongs to
/// <see cref="Approximation"/>; what this type adds is the composition and the answer to the
/// question the halting rule needs - <b>which of the two providers is responsible for the error
/// the ratio still has</b>.
/// </para>
/// <para>
/// <b>The power goes through <see cref="Approximation.Pow"/>, never through repeated
/// multiplication.</b> <c>a * a</c> treats its operands as independent unknowns, so squaring
/// <c>0 +/- 1</c> yields <c>[-1, 1]</c> - containing negatives no square can take - while
/// <c>Pow(2)</c> re-centres from the endpoints and yields <c>[0, 1]</c>. That is interval
/// arithmetic's dependency problem, and pi^n is exactly where it bites.
/// </para>
/// <para>
/// Nothing here multiplies two enclosures at all, so multiplication's load-bearing second-order
/// term never enters this member. It is <see cref="Approximation.Multiply"/>'s to carry, and a
/// future edit that reached for <c>*</c> to build a power would be reintroducing the paragraph
/// above rather than saving a call.
/// </para>
/// <para>
/// <see cref="Ratio"/> is the enclosure a search is run against and it is <b>coarsened</b>: its
/// <see cref="Approximation.MaxError"/> is <see cref="PropagatedError"/> rounded up to the next
/// power of two. Widening a bound is always sound, it discards only digits nobody reads, and it
/// stops the bound accumulating height as fast as the value does. The uncoarsened figure is kept
/// as <see cref="PropagatedError"/> because it, not the rounded one, is what the two shares
/// explain.
/// </para>
/// </remarks>
public sealed class RatioEnclosure
{
    private const string ExponentMessage =
        "The exponent must be at least 1. This composition is base^n / divisor for a positive n; " +
        "an exponent of zero drops the base from the result entirely and a negative one inverts " +
        "it, and neither is a shape this pipeline is asked for.";

    private RatioEnclosure(
        Approximation power,
        Approximation divisor,
        Approximation ratio,
        BigRational propagatedError,
        BigRational powerShare,
        BigRational divisorShare)
    {
        Power = power;
        Divisor = divisor;
        Ratio = ratio;
        PropagatedError = propagatedError;
        PowerShare = powerShare;
        DivisorShare = divisorShare;
    }

    /// <summary>Gets the enclosure of <c>base^exponent</c>, as <see cref="Approximation.Pow"/> produced it.</summary>
    /// <remarks>
    /// Because <see cref="Approximation.Pow"/> re-centres on the exact image of the input
    /// interval, this enclosure's <see cref="Approximation.Value"/> is generally <b>not</b> the
    /// base's value raised to the exponent.
    /// </remarks>
    public Approximation Power { get; }

    /// <summary>Gets the enclosure that was divided by.</summary>
    public Approximation Divisor { get; }

    /// <summary>
    /// Gets the enclosure of the ratio, with its error coarsened up to the next power of two.
    /// This is the enclosure a search runs against.
    /// </summary>
    public Approximation Ratio { get; }

    /// <summary>
    /// Gets the propagated error before coarsening: the exact bound
    /// <see cref="Approximation.Divide"/> derived for this quotient.
    /// </summary>
    /// <remarks>
    /// At or below <c>Ratio.MaxError</c>, and equal to it only when it was already a power of two
    /// or zero. <see cref="PowerShare"/> and <see cref="DivisorShare"/> sum to this, not to
    /// <c>Ratio.MaxError</c>.
    /// </remarks>
    public BigRational PropagatedError { get; }

    /// <summary>Gets the part of <see cref="PropagatedError"/> attributable to <see cref="Power"/>'s error.</summary>
    /// <remarks>
    /// <para>
    /// The propagated bound is <c>(|b|*alpha + |a|*beta) / ((|b| - beta)*|b|)</c>, one numerator
    /// term per operand over a denominator both share. The shares are
    /// <see cref="PropagatedError"/> divided between them in the proportion of those two terms,
    /// which is exact in <see cref="BigRational"/> and makes this share equal
    /// <c>alpha / (|b| - beta)</c>.
    /// </para>
    /// <para>
    /// Derived from the total rather than recomputed from the formula, deliberately: the
    /// propagation rule lives in <see cref="Approximation.Divide"/> and must live in exactly one
    /// place. What is written here is the weaker, separate fact that the numerator has one term
    /// per operand.
    /// </para>
    /// <para>
    /// This is the quantity a halting rule reads. <c>SPEC-rational-ratio.md</c> section 2 step 4
    /// makes the point with pi^3 / zeta(3), where the propagation is about
    /// <c>0.83*alpha + 21.5*beta</c>: the two providers contribute on wildly different scales, so
    /// halting on either component's own error - rather than on the propagated total - picks a
    /// search depth off by orders of magnitude.
    /// </para>
    /// </remarks>
    public BigRational PowerShare { get; }

    /// <summary>Gets the part of <see cref="PropagatedError"/> attributable to <see cref="Divisor"/>'s error.</summary>
    /// <remarks>See <see cref="PowerShare"/>; the two are the same split from the other side.</remarks>
    public BigRational DivisorShare { get; }

    /// <summary>Encloses <c>base^exponent / divisor</c>, propagating both operands' bounds.</summary>
    /// <param name="powerBase">The enclosure to raise to the power - pi, in this bench.</param>
    /// <param name="exponent">The power to raise it to. At least 1.</param>
    /// <param name="divisor">The enclosure to divide by - zeta(n), in this bench.</param>
    /// <returns>The composed enclosure and its error split.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="exponent"/> is less than 1.</exception>
    /// <exception cref="DivideByZeroException">
    /// <paramref name="divisor"/>'s enclosure contains zero, even if its
    /// <see cref="Approximation.Value"/> is non-zero. Such a divisor has not been computed
    /// accurately enough to divide by: <b>refine it first</b> rather than catching this and
    /// carrying on. <see cref="RatioRefiner"/> is what does that refining.
    /// </exception>
    public static RatioEnclosure Of(Approximation powerBase, int exponent, Approximation divisor)
    {
        ValidateExponent(exponent);

        Approximation power = powerBase.Pow(exponent);
        Approximation propagated = power / divisor;
        Approximation ratio = propagated.Coarsen();

        // One numerator term per operand, over a denominator they share. Their ratio is
        // therefore the ratio of the two contributions, whatever that denominator is.
        BigRational powerWeight = BigRational.Abs(divisor.Value) * power.MaxError;
        BigRational divisorWeight = BigRational.Abs(power.Value) * divisor.MaxError;
        BigRational weight = powerWeight + divisorWeight;

        // The weight vanishes exactly when the propagated error does, since the error is the
        // weight over a positive denominator. Both shares are then zero.
        BigRational powerShare = weight.IsZero
            ? BigRational.Zero
            : propagated.MaxError * powerWeight / weight;
        BigRational divisorShare = propagated.MaxError - powerShare;

        return new RatioEnclosure(power, divisor, ratio, propagated.MaxError, powerShare, divisorShare);
    }

    /// <summary>Rejects an exponent this composition is not defined for.</summary>
    /// <param name="exponent">The exponent to check.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="exponent"/> is less than 1.</exception>
    /// <remarks>
    /// Shared with <see cref="RatioRefiner"/>, which needs the rejection <i>before</i> it pays for
    /// a refinement from each provider, so that the rule is stated once rather than in both.
    /// </remarks>
    internal static void ValidateExponent(int exponent)
    {
        if (exponent < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(exponent), exponent, ExponentMessage);
        }
    }
}
