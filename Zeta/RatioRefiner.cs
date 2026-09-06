namespace HalHeinrich.Numerics;

/// <summary>
/// Drives two <see cref="IRealConstant"/> providers to whatever depths make the <b>propagated</b>
/// error of <c>base^exponent / divisor</c> meet a target.
/// </summary>
/// <remarks>
/// <para>
/// This is the halting rule of <c>SPEC-rational-ratio.md</c> section 2 step 4, and it is the
/// substance of the pipeline. <b>The target is on the ratio, never on a component.</b> For
/// pi^3 / zeta(3) the propagation is about <c>0.83*alpha + 21.5*beta</c>: at six terms of each,
/// the power contributes around <c>2.99e-8</c> and the divisor around <c>2.12e-6</c> against a
/// ratio error of <c>4.56e-5</c>. Halting when the <i>power's</i> error reached the target would
/// therefore drive the search some three orders of magnitude deeper than the evidence supports,
/// and halting when the divisor's did would stop short. The two providers need <b>different
/// depths</b>, and which depths is not something either of them can answer alone.
/// </para>
/// <para>
/// <b>The rule.</b> While the ratio's error exceeds the target, refine whichever provider owns the
/// larger share of it - <see cref="RatioEnclosure.PowerShare"/> against
/// <see cref="RatioEnclosure.DivisorShare"/>, which are the exact split of the propagated bound.
/// One step at a time, so neither provider is ever taken deeper than the other's error justifies.
/// A tie refines the base, arbitrarily but deterministically.
/// </para>
/// <para>
/// <b>Why it terminates.</b> Suppose it did not. Some provider is then refined infinitely often;
/// its bound tends to zero by the <see cref="IRealConstant"/> contract, so its share does too, and
/// once that share is the smaller one the other provider is refined instead. So <i>both</i> are
/// refined infinitely often, both bounds tend to zero, and the propagated error - a continuous
/// function of the two, bounded away from a vanishing denominator - tends to zero with them,
/// contradicting the supposition.
/// </para>
/// <para>
/// <b>The divisor is refined until its enclosure excludes zero, before any division is
/// attempted.</b> <see cref="Approximation.Divide"/> throws on a divisor whose enclosure contains
/// zero even when its value does not, and the contract's instruction to the caller is to refine
/// first. Catching that exception and continuing would be treating a statement about accuracy as
/// an error to be worked around.
/// </para>
/// <para>
/// Holds a live enumerator over each provider's <see cref="IRealConstant.Refinements"/>, so
/// refinement is incremental across every call: reaching step 40 costs forty steps in total and
/// not forty per target. That is why this is a disposable object rather than a static method.
/// </para>
/// </remarks>
public sealed class RatioRefiner : IDisposable
{
    private const string RefinementsEndedMessage =
        "Refinements() ended. The implementation violates the endlessness obligation of " +
        "IRealConstant.";

    private const string DivisorNeverExcludedZeroMessage =
        "The divisor's enclosure still contained zero after int.MaxValue refinements, so no " +
        "quotient can be formed. A constant whose true value is zero cannot be divided by, and " +
        "no amount of refinement changes that.";

    private const string TargetMustBePositiveMessage =
        "The target error must be strictly positive. A propagated bound tends to zero without " +
        "reaching it, so a target of zero or less would never be met.";

    private readonly IEnumerator<Approximation> baseSteps;
    private readonly IEnumerator<Approximation> divisorSteps;

    private Approximation basePart;
    private Approximation divisorPart;
    private int baseStep = -1;
    private int divisorStep = -1;
    private bool disposed;

    /// <summary>Starts a refinement at each provider's first step.</summary>
    /// <param name="powerBase">The provider raised to the power - pi, in this bench.</param>
    /// <param name="exponent">The power to raise it to. At least 1.</param>
    /// <param name="divisor">The provider divided by - zeta(n), in this bench.</param>
    /// <exception cref="ArgumentNullException"><paramref name="powerBase"/> or <paramref name="divisor"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="exponent"/> is less than 1.</exception>
    /// <exception cref="InvalidOperationException">
    /// A provider's <see cref="IRealConstant.Refinements"/> ended, or the divisor's enclosure
    /// never came to exclude zero.
    /// </exception>
    /// <remarks>
    /// Both providers are advanced to step 0, and the divisor further if step 0 does not yet
    /// exclude zero, so <see cref="Current"/> is a formed quotient from construction onward.
    /// </remarks>
    public RatioRefiner(IRealConstant powerBase, int exponent, IRealConstant divisor)
    {
        ArgumentNullException.ThrowIfNull(powerBase);
        ArgumentNullException.ThrowIfNull(divisor);
        RatioEnclosure.ValidateExponent(exponent);

        Exponent = exponent;
        baseSteps = powerBase.Refinements().GetEnumerator();
        divisorSteps = divisor.Refinements().GetEnumerator();

        AdvanceBase();
        AdvanceDivisor();
        EnsureDivisorExcludesZero();
        Current = RatioEnclosure.Of(basePart, Exponent, divisorPart);
    }

    /// <summary>Gets the power the base is raised to.</summary>
    public int Exponent { get; }

    /// <summary>Gets the zero-based index of the base refinement currently in hand.</summary>
    public int BaseStep => baseStep;

    /// <summary>Gets the zero-based index of the divisor refinement currently in hand.</summary>
    /// <remarks>
    /// Read alongside <see cref="BaseStep"/> this is the answer to "how deep did each provider
    /// have to go", which is the figure a run reports rather than a single shared depth.
    /// </remarks>
    public int DivisorStep => divisorStep;

    /// <summary>Gets the ratio as the two refinements currently in hand enclose it.</summary>
    public RatioEnclosure Current { get; private set; }

    /// <summary>Refines until the ratio's error is at or below the target.</summary>
    /// <param name="targetError">The error to reach on the ratio. Must be strictly positive.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="targetError"/> is zero or negative.</exception>
    /// <exception cref="ObjectDisposedException">This refiner has been disposed.</exception>
    /// <exception cref="InvalidOperationException">
    /// A provider's <see cref="IRealConstant.Refinements"/> ended, or the divisor's enclosure
    /// stopped excluding zero and never came to exclude it again.
    /// </exception>
    /// <remarks>
    /// The target is read against <c>Current.Ratio.MaxError</c> - the coarsened bound, which is
    /// what a search would actually be run against - and not against
    /// <see cref="RatioEnclosure.PropagatedError"/>. Meeting the target therefore means meeting
    /// it in the enclosure that gets used, so targets effectively snap to powers of two.
    /// <para>
    /// A target already met refines nothing and returns. There is no un-refining: a target larger
    /// than one already reached is silently a no-op rather than an error, because a bound that has
    /// been proven tighter does not stop being proven.
    /// </para>
    /// </remarks>
    public void RefineTo(BigRational targetError)
    {
        ObjectDisposedException.ThrowIf(disposed, this);

        if (targetError.Sign <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(targetError), TargetMustBePositiveMessage);
        }

        while (Current.Ratio.MaxError > targetError)
        {
            if (Current.PowerShare >= Current.DivisorShare)
            {
                AdvanceBase();
            }
            else
            {
                AdvanceDivisor();
                EnsureDivisorExcludesZero();
            }

            Current = RatioEnclosure.Of(basePart, Exponent, divisorPart);
        }
    }

    /// <summary>Releases both providers' refinement enumerators.</summary>
    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        baseSteps.Dispose();
        divisorSteps.Dispose();
    }

    private void AdvanceBase()
    {
        if (!baseSteps.MoveNext())
        {
            throw new InvalidOperationException(RefinementsEndedMessage);
        }

        basePart = baseSteps.Current;
        baseStep++;
    }

    private void AdvanceDivisor()
    {
        if (!divisorSteps.MoveNext())
        {
            throw new InvalidOperationException(RefinementsEndedMessage);
        }

        divisorPart = divisorSteps.Current;
        divisorStep++;
    }

    // Refine first, rather than divide and catch. The bound on int.MaxValue matches the one
    // IRealConstant.StepFor imposes on its own search: a provider whose true value is zero can
    // satisfy every obligation in the contract and still never produce a divisible enclosure, and
    // a loop with no exit for that case would hang instead of saying so.
    private void EnsureDivisorExcludesZero()
    {
        while (!divisorPart.ExcludesZero)
        {
            if (divisorStep == int.MaxValue)
            {
                throw new InvalidOperationException(DivisorNeverExcludedZeroMessage);
            }

            AdvanceDivisor();
        }
    }
}
