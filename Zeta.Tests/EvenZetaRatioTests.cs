using System.Globalization;
using System.Numerics;

namespace HalHeinrich.Numerics.Tests;

/// <summary>
/// The generating identity of <c>SPEC-rational-ratio.md</c> section 1, held against the eight
/// values that section lists and then past the end of them.
/// </summary>
/// <remarks>
/// <para>
/// <b>This is the cross-check that matters</b> - <c>halheinrich/Math#68</c>. Section 1 was amended
/// on 2026-09-09 to make the identity the authority and the eight values illustrations of it, and
/// what that ratification rests on is that the two agree. Every one of the eight is asserted here,
/// exactly, in rationals; then 18 and 20, which no list in this project contains and which are the
/// whole point of generating rather than typing.
/// </para>
/// <para>
/// <b>The Bernoulli numbers underneath are a second implementation of what
/// <c>EulerMaclaurinZeta</c> computes privately</b>, and the two are held together rather than
/// merely coexisting: <see cref="PositiveControlTests"/> asserts that the value generated here is
/// the one the pipeline drives its enclosure around, through a provider that reaches zeta from
/// reciprocal powers and never forms pi at all. <c>../AGENTS.md</c> § Testing discipline calls that
/// the strongest correctness test available, and it is what stops a duplicated recurrence from
/// being a duplicated mistake.
/// </para>
/// </remarks>
public sealed class EvenZetaRatioTests
{
    private static BigRational Value(string numerator, string denominator) =>
        new(BigInteger.Parse(numerator, CultureInfo.InvariantCulture),
            BigInteger.Parse(denominator, CultureInfo.InvariantCulture));

    [Theory]
    [InlineData(2, "6", "1")]
    [InlineData(4, "90", "1")]
    [InlineData(6, "945", "1")]
    [InlineData(8, "9450", "1")]
    [InlineData(10, "93555", "1")]
    [InlineData(12, "638512875", "691")]
    [InlineData(14, "18243225", "2")]
    [InlineData(16, "325641566250", "3617")]
    public void Of_ReproducesEveryValueSectionOneLists(int order, string numerator, string denominator) =>
        Assert.Equal(Value(numerator, denominator), EvenZetaRatio.Of(order));

    [Theory]
    [InlineData(18, "38979295480125", "43867")]
    [InlineData(20, "1531329465290625", "174611")]
    public void Of_KeepsProducingPastTheEndOfThatList(int order, string numerator, string denominator)
    {
        // The two orders the cap refused, and the reason it was wrong: "nothing above 16 can be
        // checked against a known answer" is false here and at every even order without limit.
        // Both were verified in exact rationals against an independent implementation before the
        // amendment that put the identity in section 1.
        Assert.Equal(Value(numerator, denominator), EvenZetaRatio.Of(order));
    }

    [Fact]
    public void Of_IsNotABoundOrAnApproximationButTheValue()
    {
        // Worth pinning that this is exact rather than enclosed, because everything else this
        // bench produces is enclosed. A control works only if the comparison against a survivor
        // set has no tolerance in it: the set either contains this rational or it does not.
        BigRational twelve = EvenZetaRatio.Of(12);

        Assert.Equal(BigInteger.Parse("638512875", CultureInfo.InvariantCulture), twelve.Numerator);
        Assert.Equal(new BigInteger(691), twelve.Denominator);
    }

    [Theory]
    [InlineData(2, 1)]
    [InlineData(10, 1)]
    [InlineData(12, 691)]
    [InlineData(14, 2)]
    [InlineData(16, 3617)]
    [InlineData(18, 43867)]
    [InlineData(20, 174611)]
    public void ReachableFrom_IsTheDenominatorARunMustReach(int order, int denominator)
    {
        // Not monotone, which is exactly why the cap's other justification - that 16 is "where the
        // even orders stop having small denominators" - was unsupported too. The digits run 3, 1,
        // 4, 5, 6 across n = 12 to 20 and there is no cliff at 16.
        Assert.Equal(new BigInteger(denominator), EvenZetaRatio.ReachableFrom(order));
    }

    [Theory]
    [InlineData(2)]
    [InlineData(16)]
    [InlineData(100)]
    public void IsKnown_AcceptsEveryEvenOrderWithoutLimit(int order) =>
        Assert.True(EvenZetaRatio.IsKnown(order));

    [Theory]
    [InlineData(3)]
    [InlineData(17)]
    [InlineData(1)]
    [InlineData(0)]
    [InlineData(-2)]
    public void IsKnown_RejectsWhatTheIdentityDoesNotReach(int order) =>
        Assert.False(EvenZetaRatio.IsKnown(order));

    [Theory]
    [InlineData(3)]
    [InlineData(5)]
    [InlineData(17)]
    public void Of_RefusesAnOddOrderRatherThanInventingAValueForIt(int order)
    {
        // Whether pi^n/zeta(n) is rational at odd n is the open question this whole bench exists
        // to probe. A generator that returned something here would be answering it.
        ArgumentOutOfRangeException thrown =
            Assert.Throws<ArgumentOutOfRangeException>(() => EvenZetaRatio.Of(order));

        Assert.Contains("as far as anyone knows", thrown.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(0)]
    [InlineData(int.MinValue)]
    public void Of_RefusesAnOrderZetaHasNoValueFor(int order) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => EvenZetaRatio.Of(order));

    [Theory]
    [InlineData(2, "6")]
    [InlineData(12, "638512875/691")]
    [InlineData(18, "38979295480125/43867")]
    public void Format_WritesTheValueTheWaySectionOneListsIt(int order, string expected) =>
        Assert.Equal(expected, EvenZetaRatio.Format(order));

    [Fact]
    public void Of_AgreesWithItselfAtEveryEvenOrderInSectionOnesRange()
    {
        // The recurrence is a loop building a cache from scratch on each call, so a fault in the
        // cache indexing would show as an order whose value depends on which orders were asked for
        // before it. Asking for the range twice, in both directions, is what would catch it.
        BigRational[] ascending = [.. Enumerable.Range(1, 10).Select(half => EvenZetaRatio.Of(2 * half))];
        BigRational[] descending = [.. Enumerable.Range(1, 10).Reverse().Select(half => EvenZetaRatio.Of(2 * half))];

        Assert.Equal(ascending, descending.Reverse());
    }
}
