using System.Numerics;

namespace HalHeinrich.Numerics.Experiments;

/// <summary>
/// Where a survivor run's denominator bound came from: the depth its precision supports, and the
/// depth its budget affords.
/// </summary>
/// <param name="Derived">
/// <c>floor(eps^(-1/2))</c> from the final enclosure - the depth a generic sweep reaches at this
/// precision, and the bound a chart run always walks to.
/// </param>
/// <param name="Affordable">
/// The largest bound whose deep walk the budget buys, or null when nothing caps it: a chart run,
/// which refuses on cost rather than capping, or a price of zero, at which every bound is free.
/// </param>
/// <remarks>
/// <para>
/// <b>Ruling 5 on <c>halheinrich/Math#64</c>: a deep run walks to the smaller of the two, and
/// reports both.</b> <c>../SPEC-rational-ratio.md</c> § 2 states <c>eps &lt; H^-2</c> as a
/// sufficient condition on the error for a claim about <c>H</c>, not as a formula for <c>Q</c>, so
/// claiming less than the error would support is always sound. What would not be sound is a bound
/// somebody picked: the caller never chooses <c>Q</c> here any more than in the chart, since both
/// candidates for it are computed - one from the precision, one from the price.
/// </para>
/// <para>
/// <b>Both figures travel with the result, because the cap changes what the result means.</b> The
/// null <c>6*eps*Q^2/pi^2</c> falls with <c>Q</c>, so under a cap it is below the <c>6/pi^2</c>
/// that holds at every derived precision: a survivor found under a capped <c>Q</c> is stronger
/// evidence than the same survivor under the derived one, while an empty set refutes only to the
/// capped <c>Q</c> and is a narrower statement than the precision would support. A report that
/// printed <c>Q</c> alone would leave a reader unable to tell which of those they were reading.
/// </para>
/// </remarks>
internal readonly record struct SurvivorBound(BigInteger Derived, BigInteger? Affordable)
{
    /// <summary>Gets the bound the run walks to: the derived one, unless the budget affords less.</summary>
    public BigInteger Q => Affordable is { } affordable && affordable < Derived ? affordable : Derived;

    /// <summary>Gets whether the budget, rather than the precision, set <see cref="Q"/>.</summary>
    public bool IsCapped => Affordable is { } affordable && affordable < Derived;
}
