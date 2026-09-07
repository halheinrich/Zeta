using System.Numerics;
using HalHeinrich.Numerics.Experiments;

namespace HalHeinrich.Numerics.Tests;

/// <summary>
/// The experiment runner's presentation decisions, held against hand-computed values.
/// </summary>
/// <remarks>
/// <para>
/// <b>These are here because two of the decisions read plausibly when wrong.</b> Normalising the
/// blame split is the one that earned the seam: <c>PowerShare</c> and <c>DivisorShare</c> are
/// absolute parts of <c>PropagatedError</c> rather than fractions of one, so rendering them
/// straight as percentages prints 0.00% for both at any realistic bound - which an umbrella probe
/// of the walk did first time, and which reads as a pipeline that has stopped rather than as a
/// formatting mistake. A decision reachable only through a printed table is a decision nothing
/// can hold, so it is lifted into a pure function and handed values here instead.
/// </para>
/// <para>
/// Testing a formatter does not make <c>Zeta.Experiments</c> a test project.
/// <c>../AGENTS.md</c> § Exactness discipline separates the two by whether a run has a known
/// answer and whether it depends on wall-clock time; a formatter has one and does not.
/// </para>
/// </remarks>
public sealed class PresentationTests
{
    private static Approximation At(int value, int errorNumerator, int errorDenominator) =>
        Approximation.Create(
            BigRational.FromInteger(value), new BigRational(errorNumerator, errorDenominator));

    // ---------- the blame split ----------

    [Fact]
    public void BlameSplit_NormalisesToFractionsOfOne_NotTheAbsoluteShares()
    {
        // The probe's mistake, as a test. At this scale the raw shares are around 1e-2, so
        // printing them as percentages gives about 1% and 0% where the truth is 97% and 3%.
        RatioEnclosure enclosure = RatioEnclosure.Of(
            Approximation.Create(BigRational.FromInteger(3), new BigRational(1, 100)),
            2,
            Approximation.Create(BigRational.FromInteger(2), new BigRational(1, 100_000)));

        (BigRational power, BigRational divisor) = Presentation.BlameSplit(enclosure);

        Assert.Equal(BigRational.One, power + divisor);
        Assert.True(power > new BigRational(9, 10), "The base owns almost all of this error.");

        // And the normalisation is not the identity: the raw shares are nowhere near summing to
        // one, which is exactly why printing them directly is wrong.
        Assert.True(
            enclosure.PowerShare + enclosure.DivisorShare < new BigRational(1, 10),
            "This fixture wants shares far below 1, or it cannot tell the two renderings apart.");
    }

    [Fact]
    public void BlameSplit_TracksThePropagatedErrorsOwnSplit()
    {
        // Independent of the type under test: the fraction must be the share over the total,
        // computed here from RatioEnclosure's own two properties.
        RatioEnclosure enclosure = RatioEnclosure.Of(
            Approximation.Create(BigRational.FromInteger(3), new BigRational(1, 1000)),
            3,
            Approximation.Create(BigRational.FromInteger(2), new BigRational(1, 400)));

        (BigRational power, BigRational divisor) = Presentation.BlameSplit(enclosure);
        BigRational total = enclosure.PowerShare + enclosure.DivisorShare;

        Assert.Equal(enclosure.PowerShare / total, power);
        Assert.Equal(enclosure.DivisorShare / total, divisor);
    }

    [Fact]
    public void BlameSplit_WithBothOperandsExact_ApportionsNothing()
    {
        // There is no error to blame, and a normaliser dividing by the total must not divide by
        // zero to discover that.
        RatioEnclosure enclosure = RatioEnclosure.Of(
            Approximation.Exact(BigRational.FromInteger(6)), 2, Approximation.Exact(BigRational.FromInteger(4)));

        (BigRational power, BigRational divisor) = Presentation.BlameSplit(enclosure);

        Assert.Equal(BigRational.Zero, power);
        Assert.Equal(BigRational.Zero, divisor);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void BlameSplit_WithOneOperandExact_GivesTheOtherAllOfIt(bool exactDivisor)
    {
        Approximation sharp = Approximation.Exact(BigRational.FromInteger(2));
        Approximation blunt = Approximation.Create(BigRational.FromInteger(2), new BigRational(1, 8));

        RatioEnclosure enclosure = exactDivisor
            ? RatioEnclosure.Of(blunt, 1, sharp)
            : RatioEnclosure.Of(sharp, 1, blunt);

        (BigRational power, BigRational divisor) = Presentation.BlameSplit(enclosure);

        Assert.Equal(exactDivisor ? BigRational.One : BigRational.Zero, power);
        Assert.Equal(exactDivisor ? BigRational.Zero : BigRational.One, divisor);
    }

    // ---------- Pow's cost, which the walk prints as a measurement ----------

    [Fact]
    public void PowCost_IsPositive_BecauseThePowerIsBluntedRatherThanSharpened()
    {
        // The sign is the whole content: a negative number under a column headed Pow reads as
        // the power being tighter than its base, which no exponent above 1 ever makes it.
        RatioEnclosure enclosure = RatioEnclosure.Of(
            Approximation.Create(BigRational.FromInteger(3), new BigRational(1, 1_000_000)),
            2,
            Approximation.Exact(BigRational.One));

        Assert.True(enclosure.Power.MaxError > enclosure.PowerBase.MaxError, "Squaring must widen this bound.");
        Assert.StartsWith("0.", Presentation.PowCost(enclosure), StringComparison.Ordinal);
    }

    [Fact]
    public void PowCost_WithAnExactBase_SaysNothingRatherThanZero()
    {
        RatioEnclosure enclosure = RatioEnclosure.Of(
            Approximation.Exact(BigRational.FromInteger(3)), 2, Approximation.Exact(BigRational.One));

        Assert.Equal(string.Empty, Presentation.PowCost(enclosure));
    }

    // ---------- decimals ----------

    [Theory]
    [InlineData(1, 2, 3, "0.500")]
    [InlineData(-1, 2, 3, "-0.500")]
    [InlineData(4, 3, 5, "1.33333")]
    [InlineData(-4, 3, 5, "-1.33333")]
    [InlineData(7, 1, 2, "7.00")]
    public void ToDecimal_TruncatesTowardsZero(int numerator, int denominator, int places, string expected)
    {
        Assert.Equal(expected, Presentation.ToDecimal(new BigRational(numerator, denominator), places));
    }

    [Fact]
    public void Magnitude_OfZero_SaysExactRatherThanPrintingAnInfinity()
    {
        Assert.Equal("exact", Presentation.Magnitude(BigRational.Zero));
    }

    // ---------- earned digits ----------

    [Fact]
    public void Earned_GivesOnlyTheDigitsBothBoundsAgreeOn()
    {
        // 1.23 +/- 0.001 spans 1.229 to 1.231, so the hundredths digit is not pinned.
        Approximation enclosure = Approximation.Create(new BigRational(123, 100), new BigRational(1, 1000));

        Assert.Equal("1.2", Presentation.Earned(enclosure, 6));
    }

    [Fact]
    public void Earned_AcrossAnIntegerBoundary_PinsNothing_WhichIsTheTruth()
    {
        // The case the zeta(2) walk lives in for its whole length, so it is pinned rather than
        // explained: an enclosure straddling 6 has a lower bound beginning 5 and an upper
        // beginning 6, so no decimal digit is shared however narrow the interval gets. A digit
        // count derived from the bound would have claimed eleven.
        Approximation enclosure = Approximation.Create(
            BigRational.FromInteger(6), new BigRational(BigInteger.One, BigInteger.Pow(10, 12)));

        Assert.Equal("?", Presentation.Earned(enclosure, 14));
    }

    [Fact]
    public void Earned_OfAnExactValue_RunsOutOfPlacesRatherThanComparingAgainstItself()
    {
        Approximation enclosure = Approximation.Exact(new BigRational(1, 4));

        Assert.Equal("0.2500...", Presentation.Earned(enclosure, 4));
    }
}
