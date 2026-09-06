using System.Globalization;
using System.Numerics;

namespace HalHeinrich.Numerics.Tests;

/// <summary>
/// The composition and its error split, against hand-written enclosures whose propagated bound is
/// worked out independently here rather than read back from the type under test.
/// </summary>
public sealed class RatioEnclosureTests
{
    private static BigRational Ratio(BigInteger numerator, BigInteger denominator) =>
        new(numerator, denominator);

    private static string Inv(FormattableString message) => message.ToString(CultureInfo.InvariantCulture);

    /// <summary>The specified propagation, computed here from the operands rather than by the type under test.</summary>
    private static BigRational SpecifiedBound(Approximation left, Approximation right)
    {
        BigRational a = left.Value;
        BigRational alpha = left.MaxError;
        BigRational b = right.Value;
        BigRational beta = right.MaxError;
        BigRational absB = BigRational.Abs(b);

        return ((absB * alpha) + (BigRational.Abs(a) * beta)) / ((absB - beta) * absB);
    }

    // ---------- the power ----------

    [Fact]
    public void Of_TakesThePowerThroughPow_NotThroughRepeatedMultiplication()
    {
        // The case that separates them. Squaring 0 +/- 1 has image [0, 1]; treating the two
        // operands as independent yields [-1, 1], which contains negatives no square can take.
        Approximation straddlingZero = Approximation.Create(BigRational.Zero, BigRational.One);
        Approximation divisor = Approximation.Create(BigRational.One, Ratio(1, 1000));

        RatioEnclosure enclosure = RatioEnclosure.Of(straddlingZero, 2, divisor);

        Assert.Equal(BigRational.Zero, enclosure.Power.Lower);
        Assert.Equal(BigRational.One, enclosure.Power.Upper);
        Assert.Equal(Ratio(1, 2), enclosure.Power.Value);

        // And the trap it avoids, stated as a fact about the alternative rather than as a remark.
        Approximation multiplied = straddlingZero * straddlingZero;
        Assert.Equal(BigRational.MinusOne, multiplied.Lower);
        Assert.True(
            enclosure.Power.Lower > multiplied.Lower,
            "Pow must exclude the negatives that repeated multiplication admits.");
    }

    [Fact]
    public void Of_AtExponentThree_TakesTheExactImageOfTheInterval()
    {
        // [2, 4] cubed is [8, 64], so the enclosure is 36 +/- 28 - not 27 +/- anything.
        Approximation cube = RatioEnclosure
            .Of(Approximation.Create(BigRational.FromInteger(3), BigRational.One), 3, Approximation.Exact(BigRational.One))
            .Power;

        Assert.Equal(BigRational.FromInteger(8), cube.Lower);
        Assert.Equal(BigRational.FromInteger(64), cube.Upper);
        Assert.Equal(BigRational.FromInteger(36), cube.Value);
        Assert.Equal(BigRational.FromInteger(28), cube.MaxError);
    }

    // ---------- the propagated bound ----------

    [Fact]
    public void Of_ReportsTheSpecifiedPropagatedBound()
    {
        // 3 +/- 1/10 over 2 +/- 1/100. The bound is (2*(1/10) + 3*(1/100)) / ((2 - 1/100)*2),
        // which is (23/100) / (398/100), which is 23/398.
        Approximation numerator = Approximation.Create(BigRational.FromInteger(3), Ratio(1, 10));
        Approximation divisor = Approximation.Create(BigRational.FromInteger(2), Ratio(1, 100));

        RatioEnclosure enclosure = RatioEnclosure.Of(numerator, 1, divisor);

        Assert.Equal(Ratio(3, 2), enclosure.Ratio.Value);
        Assert.Equal(Ratio(23, 398), enclosure.PropagatedError);
    }

    [Fact]
    public void Of_ReportsTheSpecifiedPropagatedBound_AcrossWideAndNarrowFixtures()
    {
        foreach ((Approximation numerator, Approximation divisor) in PropagationFixtures())
        {
            RatioEnclosure enclosure = RatioEnclosure.Of(numerator, 1, divisor);

            Assert.Equal(SpecifiedBound(numerator, divisor), enclosure.PropagatedError);
            Assert.Equal(numerator.Value / divisor.Value, enclosure.Ratio.Value);
        }
    }

    [Fact]
    public void Of_TheEnclosureContainsEveryQuotientTheOperandsPermit()
    {
        // A bound is tested by trying to violate it. Every quotient of a point in the numerator's
        // interval by a point in the divisor's must lie inside the result.
        foreach ((Approximation numerator, Approximation divisor) in PropagationFixtures())
        {
            RatioEnclosure enclosure = RatioEnclosure.Of(numerator, 1, divisor);

            foreach (BigRational x in new[] { numerator.Lower, numerator.Value, numerator.Upper })
            {
                foreach (BigRational y in new[] { divisor.Lower, divisor.Value, divisor.Upper })
                {
                    Assert.True(
                        enclosure.Ratio.Contains(x / y),
                        Inv($"The enclosure lost the quotient {x} / {y}."));
                }
            }
        }
    }

    // ---------- the split ----------

    [Fact]
    public void Of_SplitsThePropagatedErrorInTheClosedFormEachOperandContributes()
    {
        // The power's share is alpha / (|b| - beta) and the divisor's is |a|*beta / ((|b| -
        // beta)*|b|). Computed here from the operands; the type derives them from the total by
        // proportion, so agreement is a real check rather than the same line run twice.
        foreach ((Approximation numerator, Approximation divisor) in PropagationFixtures())
        {
            RatioEnclosure enclosure = RatioEnclosure.Of(numerator, 1, divisor);

            BigRational absB = BigRational.Abs(divisor.Value);
            BigRational expectedPower = numerator.MaxError / (absB - divisor.MaxError);
            BigRational expectedDivisor =
                BigRational.Abs(numerator.Value) * divisor.MaxError / ((absB - divisor.MaxError) * absB);

            Assert.Equal(expectedPower, enclosure.PowerShare);
            Assert.Equal(expectedDivisor, enclosure.DivisorShare);
            Assert.Equal(enclosure.PropagatedError, enclosure.PowerShare + enclosure.DivisorShare);
        }
    }

    [Fact]
    public void Of_WithAnExactDivisor_GivesTheDivisorNoShareOfTheError()
    {
        RatioEnclosure enclosure = RatioEnclosure.Of(
            Approximation.Create(BigRational.FromInteger(7), Ratio(1, 8)),
            1,
            Approximation.Exact(BigRational.FromInteger(2)));

        Assert.Equal(BigRational.Zero, enclosure.DivisorShare);
        Assert.Equal(enclosure.PropagatedError, enclosure.PowerShare);
    }

    [Fact]
    public void Of_WithAnExactBase_GivesThePowerNoShareOfTheError()
    {
        RatioEnclosure enclosure = RatioEnclosure.Of(
            Approximation.Exact(BigRational.FromInteger(7)),
            1,
            Approximation.Create(BigRational.FromInteger(2), Ratio(1, 8)));

        Assert.Equal(BigRational.Zero, enclosure.PowerShare);
        Assert.Equal(enclosure.PropagatedError, enclosure.DivisorShare);
    }

    [Fact]
    public void Of_WithBothOperandsExact_ReportsNoErrorAndNoShares()
    {
        RatioEnclosure enclosure = RatioEnclosure.Of(
            Approximation.Exact(BigRational.FromInteger(6)),
            2,
            Approximation.Exact(BigRational.FromInteger(4)));

        Assert.Equal(BigRational.FromInteger(9), enclosure.Ratio.Value);
        Assert.Equal(BigRational.Zero, enclosure.PropagatedError);
        Assert.Equal(BigRational.Zero, enclosure.PowerShare);
        Assert.Equal(BigRational.Zero, enclosure.DivisorShare);
        Assert.True(enclosure.Ratio.IsExact);
    }

    [Fact]
    public void Of_AtTheSpecsWorkedScale_PutsAlmostAllTheErrorOnTheDivisor()
    {
        // SPEC-rational-ratio.md section 2 step 4, in the shape it states: pi^3 enclosed to about
        // 2.99e-8 and zeta(3) to about 2.12e-6, whose propagation it gives as roughly
        // 0.83*alpha + 21.5*beta. Those two coefficients are what the shares recover.
        Approximation piCubed = Approximation.Create(
            Ratio(3100627668, 100000000), new BigRational(299, BigInteger.Pow(10, 10)));
        Approximation zetaThree = Approximation.Create(
            Ratio(120205690, 100000000), new BigRational(212, BigInteger.Pow(10, 8)));

        RatioEnclosure enclosure = RatioEnclosure.Of(piCubed, 1, zetaThree);

        BigRational powerCoefficient = enclosure.PowerShare / piCubed.MaxError;
        BigRational divisorCoefficient = enclosure.DivisorShare / zetaThree.MaxError;

        // In exact rationals, not decimals: nothing upstream of presentation formats a number
        // here. Written as comparisons rather than Assert.InRange, which wants the non-generic
        // IComparable that BigRational does not implement.
        Assert.True(
            powerCoefficient > Ratio(82, 100) && powerCoefficient < Ratio(84, 100),
            Inv($"The power's coefficient was {powerCoefficient}, not near the spec's 0.83."));
        Assert.True(
            divisorCoefficient > Ratio(21, 1) && divisorCoefficient < Ratio(22, 1),
            Inv($"The divisor's coefficient was {divisorCoefficient}, not near the spec's 21.5."));

        // The consequence the halting rule exists for: the divisor owns three orders of magnitude
        // more of the error than the power does, so a target read off the power alone is meaningless.
        Assert.True(
            enclosure.DivisorShare > BigRational.FromInteger(1000) * enclosure.PowerShare,
            Inv($"Expected the divisor to dominate; shares were {enclosure.PowerShare} and {enclosure.DivisorShare}."));
    }

    // ---------- coarsening ----------

    [Fact]
    public void Of_CoarsensTheRatioErrorUpToTheNextPowerOfTwo()
    {
        foreach ((Approximation numerator, Approximation divisor) in PropagationFixtures())
        {
            RatioEnclosure enclosure = RatioEnclosure.Of(numerator, 1, divisor);
            BigRational coarsened = enclosure.Ratio.MaxError;

            Assert.True(
                coarsened >= enclosure.PropagatedError,
                Inv($"Coarsening narrowed {enclosure.PropagatedError} to {coarsened}."));
            Assert.True(IsPowerOfTwo(coarsened), Inv($"{coarsened} is not a power of two."));
            Assert.True(
                coarsened / BigRational.FromInteger(2) < enclosure.PropagatedError,
                Inv($"{coarsened} overshot: half of it still bounds {enclosure.PropagatedError}."));
        }
    }

    [Fact]
    public void Of_CoarseningLeavesTheValueAlone()
    {
        Approximation numerator = Approximation.Create(BigRational.FromInteger(3), Ratio(1, 10));
        Approximation divisor = Approximation.Create(BigRational.FromInteger(2), Ratio(1, 100));

        RatioEnclosure enclosure = RatioEnclosure.Of(numerator, 1, divisor);

        Assert.Equal(Ratio(3, 2), enclosure.Ratio.Value);
        Assert.True(enclosure.Ratio.MaxError > enclosure.PropagatedError);
    }

    // ---------- guards ----------

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    public void Of_RejectsAnExponentBelowOne(int exponent)
    {
        Approximation one = Approximation.Exact(BigRational.One);
        Approximation two = Approximation.Exact(BigRational.FromInteger(2));

        Assert.Throws<ArgumentOutOfRangeException>(() => RatioEnclosure.Of(one, exponent, two));
    }

    [Fact]
    public void Of_ThrowsWhenTheDivisorEnclosesZero_EvenThoughItsValueDoesNot()
    {
        // 1/10 +/- 1/5 has a non-zero value and an interval spanning zero. The contract is that
        // such a divisor has not been computed accurately enough to divide by.
        Approximation divisor = Approximation.Create(Ratio(1, 10), Ratio(1, 5));
        Assert.False(divisor.Value.IsZero);
        Assert.True(divisor.Contains(BigRational.Zero));

        Assert.Throws<DivideByZeroException>(
            () => RatioEnclosure.Of(Approximation.Exact(BigRational.One), 1, divisor));
    }

    [Fact]
    public void Of_ThrowsWhenTheDivisorsIntervalTouchesZeroAtAnEndpoint()
    {
        // The condition is |Value| > MaxError, so an interval whose endpoint is exactly zero is
        // still out of bounds. Stated as a test because a strict-versus-weak slip here is silent.
        Approximation divisor = Approximation.Create(BigRational.One, BigRational.One);

        Assert.Throws<DivideByZeroException>(
            () => RatioEnclosure.Of(Approximation.Exact(BigRational.One), 1, divisor));
    }

    private static bool IsPowerOfTwo(BigRational value)
    {
        if (value.Sign <= 0)
        {
            return false;
        }

        return IsOneOrPowerOfTwo(value.Numerator) && IsOneOrPowerOfTwo(value.Denominator);

        static bool IsOneOrPowerOfTwo(BigInteger part) => (part & (part - BigInteger.One)).IsZero;
    }

    private static IEnumerable<(Approximation Numerator, Approximation Divisor)> PropagationFixtures()
    {
        // Deliberately wide as well as narrow. A narrow enclosure hides a wrong bound: it makes
        // second-order effects negligible, so a fixture set that is all narrow cannot tell a
        // sound propagation from an unsound one.
        yield return (Approximation.Create(BigRational.FromInteger(3), Ratio(1, 10)),
                      Approximation.Create(BigRational.FromInteger(2), Ratio(1, 100)));
        yield return (Approximation.Create(BigRational.FromInteger(3), BigRational.FromInteger(2)),
                      Approximation.Create(BigRational.FromInteger(5), BigRational.FromInteger(2)));
        yield return (Approximation.Create(Ratio(-7, 3), Ratio(4, 5)),
                      Approximation.Create(Ratio(9, 4), Ratio(1, 2)));
        yield return (Approximation.Create(Ratio(1, 1000), Ratio(1, 10000)),
                      Approximation.Create(Ratio(-3, 7), Ratio(1, 100)));
        yield return (Approximation.Exact(Ratio(22, 7)),
                      Approximation.Create(Ratio(6, 5), Ratio(1, 1000)));
        yield return (Approximation.Create(BigRational.Zero, Ratio(1, 4)),
                      Approximation.Create(BigRational.FromInteger(2), Ratio(1, 8)));
    }
}
