using System.Numerics;

namespace HalHeinrich.Numerics.Tests;

/// <summary>
/// A provider of nothing: an explicitly written list of enclosures, continued endlessly by
/// halving the last bound. It exists so a pipeline can be handed numbers chosen to exercise a
/// decision, rather than numbers a real constant happens to produce.
/// </summary>
/// <remarks>
/// <para>
/// This is scaffolding and says so. It approximates no real number and its values are whatever a
/// test needed them to be; nothing outside these tests should use it, and nothing should treat it
/// as a provider of anything. What it does honour is the <see cref="IRealConstant"/> contract -
/// pure, non-increasing and vanishing bounds, and a lazy, endless, strictly improving,
/// incremental refinement sequence - because a pipeline driven by a stub that broke the contract
/// would be tested against a case it is entitled to assume cannot happen.
/// </para>
/// <para>
/// <b>What is asserted of it is never what it declares.</b> A test that read the stub's numbers
/// back out would prove only that the test compiles. The assertions here are about what the
/// pipeline <i>decides</i> from those numbers - which provider it refines and how far, what
/// enclosure the composition produces, what the search then finds - and a stub cannot fake a
/// decision it does not make.
/// </para>
/// <para>
/// Distinct from <c>RealConstants</c>' <c>DisplacedConstant</c>, which is a decorator that keeps a
/// real provider's bound while moving its value, so that a cross-check can be shown to fail. That
/// one is a knowingly wrong provider of a real constant; this one is not a provider of a constant
/// at all. Neither is a substitute for the other, and this one lives here because the pipeline
/// needs enclosures nobody's series produces.
/// </para>
/// </remarks>
internal sealed class StubConstant : IRealConstant
{
    private static readonly BigRational Half = new(1, 2);

    private readonly Approximation[] declared;

    private StubConstant(Approximation[] declared)
    {
        this.declared = declared;
    }

    /// <summary>Gets the number of enclosures pulled from <see cref="Refinements"/> since construction.</summary>
    /// <remarks>
    /// Counts across every enumerator this instance has handed out, so a consumer that claims to
    /// refine incrementally can be held to it: the count is what the claim is about, and the stub
    /// has no say in what it comes to.
    /// </remarks>
    internal int Pulled { get; private set; }

    /// <summary>Builds a stub from an explicit prefix of enclosures.</summary>
    /// <param name="steps">
    /// The enclosures, in order. Must be non-empty, with strictly decreasing and non-negative
    /// bounds, the last strictly positive so the endless continuation can keep improving.
    /// </param>
    /// <returns>The stub.</returns>
    internal static StubConstant Of(params Approximation[] steps)
    {
        Assert.NotNull(steps);
        Assert.NotEmpty(steps);
        Assert.True(steps[^1].MaxError.Sign > 0, "The last declared bound must be positive.");

        for (int index = 1; index < steps.Length; index++)
        {
            Assert.True(
                steps[index].MaxError < steps[index - 1].MaxError,
                $"Declared bounds must strictly improve; step {index} did not.");
        }

        return new StubConstant([.. steps]);
    }

    /// <summary>Builds a stub of one fixed value whose bound starts at <paramref name="first"/> and halves.</summary>
    /// <param name="value">The value every enclosure is centred on.</param>
    /// <param name="first">The bound at step 0. Must be positive.</param>
    /// <returns>The stub.</returns>
    /// <remarks>
    /// A fixed value keeps the enclosures nested, so every one of them encloses whatever the
    /// earlier ones did and the sequence is coherent as an approximation of something.
    /// </remarks>
    internal static StubConstant Halving(BigRational value, BigRational first) =>
        Of(Approximation.Create(value, first));

    /// <summary>Gets the declared bound at a step, or the halved continuation beyond the prefix.</summary>
    /// <param name="step">The zero-based step index.</param>
    /// <returns>The enclosure at that step.</returns>
    /// <remarks>Pure, and free of the pull counter: reading a bound is not taking a refinement.</remarks>
    internal Approximation StepAt(int step)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(step);

        if (step < declared.Length)
        {
            return declared[step];
        }

        Approximation last = declared[^1];
        BigRational shrink = BigRational.Pow(Half, step - declared.Length + 1);
        return Approximation.Create(last.Value, last.MaxError * shrink);
    }

    /// <summary>Gets the bound at a step without computing the step.</summary>
    /// <param name="step">The zero-based step index.</param>
    /// <returns>The declared or continued bound.</returns>
    public BigRational ErrorBoundAt(int step) => StepAt(step).MaxError;

    /// <summary>Gets the endless sequence of declared and continued enclosures.</summary>
    /// <returns>A lazy, endless, strictly improving sequence.</returns>
    public IEnumerable<Approximation> Refinements()
    {
        for (int step = 0; ; step++)
        {
            Pulled++;
            yield return StepAt(step);
        }
    }

    /// <summary>Builds an enclosure from integer parts, for readability at a call site.</summary>
    /// <param name="value">The centre.</param>
    /// <param name="errorNumerator">The bound's numerator.</param>
    /// <param name="errorDenominator">The bound's denominator.</param>
    /// <returns>The enclosure.</returns>
    internal static Approximation At(BigRational value, BigInteger errorNumerator, BigInteger errorDenominator) =>
        Approximation.Create(value, new BigRational(errorNumerator, errorDenominator));
}
