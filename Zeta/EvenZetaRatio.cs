using System.Globalization;
using System.Numerics;

namespace HalHeinrich.Numerics;

/// <summary>
/// The exact value of <c>pi^n / zeta(n)</c> at even <c>n</c>, generated from
/// <c>SPEC-rational-ratio.md</c> section 1's identity rather than looked up.
/// </summary>
/// <remarks>
/// <para>
/// <b>The identity is the authority and the listed values are illustrations of it</b>, ratified at
/// the umbrella 2026-09-09 under <c>halheinrich/Math#68</c>:
/// </para>
/// <code>
/// pi^(2k) / zeta(2k) = 2 * (2k)! / ( (-1)^(k+1) * B_(2k) * 2^(2k) )
/// </code>
/// <para>
/// which follows from <c>zeta(2k) = (-1)^(k+1) B_(2k) (2pi)^(2k) / (2 * (2k)!)</c>. It is closed,
/// it runs to any <c>k</c>, and it needs only the even-index Bernoulli numbers.
/// </para>
/// <para>
/// <b>Generated because a typed list of eight became a mathematical-looking cap on the exhibit.</b>
/// Section 1 lists 6, 90, 945, 9450, 93555, 638512875/691, 18243225/2 and 325641566250/3617, and
/// <c>Zeta.Experiments</c> carried a <c>MaxOrder = 16</c> whose refusal said that "nothing above it
/// can be checked against a known answer". That is false at every even order without limit - 18 is
/// <c>38979295480125/43867</c> and 20 is <c>1531329465290625/174611</c> - and the cap also refused
/// odd orders for an argument about even ones, which applied consistently would forbid order 3,
/// the project's whole research target. <c>../AGENTS.md</c> § Writing code names a formula
/// transcribed into literals as the defect, and this is what it looks like when the transcription
/// is mistaken for the boundary of what is knowable.
/// </para>
/// <para>
/// <b>The Bernoulli recurrence is written out here, and it should not have to be.</b>
/// <c>EulerMaclaurinZeta</c> already computes and caches the even-index Bernoulli numbers for its
/// own corrections, but does so through a private method taking a caller-supplied cache, so there
/// is no surface to reach. Reimplementing a definition is the duplication § Writing code warns
/// about; what makes it tolerable rather than invisible is that the two are cross-checked by
/// construction - <c>EvenZetaRatioTests</c> holds this against section 1's eight listed values,
/// and <c>PositiveControlTests</c> reaches the same answers through <c>EulerMaclaurinZeta</c> and
/// the whole pipeline, so a fault in either recurrence reddens. A <c>RealConstants</c> change
/// exposing the sequence would let this become a call, and is flagged rather than made here: this
/// arc's brief is <c>Zeta</c>'s alone.
/// </para>
/// <para>
/// <b>No cache, deliberately, and for the reason <c>EulerMaclaurinZeta</c> gives for its own
/// arrangement.</b> A shared static cache would be mutable state on a type nothing else about needs
/// to be stateful, and the recurrence is quadratic in a half-index that is single digits for every
/// order this bench runs. The promise of a pure function is worth more than the recomputation.
/// </para>
/// </remarks>
public static class EvenZetaRatio
{
    private const string OddOrderMessage =
        "Only an even order has a known value. zeta of an odd argument is not a rational multiple " +
        "of the corresponding power of pi as far as anyone knows, which is the question this bench " +
        "exists to probe; there is nothing to generate.";

    /// <summary>Whether this order has a value the identity generates.</summary>
    /// <param name="order">The order of zeta, as in <c>pi^n / zeta(n)</c>.</param>
    /// <returns>True for an even order of at least two.</returns>
    /// <remarks>
    /// A predicate rather than a caller's own <c>order % 2 == 0</c>, because the two-and-above part
    /// is easy to leave out and <c>zeta(0)</c> is not what <see cref="Of"/> would be asked about.
    /// It is also what a guard wants: "is there an answer to check against" is a question, and
    /// catching an exception is not how to ask one.
    /// </remarks>
    public static bool IsKnown(int order) => order >= 2 && order % 2 == 0;

    /// <summary>The exact value of <c>pi^order / zeta(order)</c>.</summary>
    /// <param name="order">The order of zeta. Even, and at least two.</param>
    /// <returns>The value, in lowest terms.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="order"/> is below two or odd - <see cref="IsKnown"/> is the question this
    /// answers by throwing.
    /// </exception>
    /// <remarks>
    /// Exact rational arithmetic throughout, so the result is the value and not an approximation of
    /// it. That is what makes it usable as a control: a survivor set either contains this rational
    /// or it does not, and nothing in the comparison has a tolerance to get wrong.
    /// </remarks>
    public static BigRational Of(int order)
    {
        if (!IsKnown(order))
        {
            throw new ArgumentOutOfRangeException(
                nameof(order),
                order,
                order < 2
                    ? "The order must be at least 2. At s = 1 the series is the harmonic one and " +
                      "does not converge, so there is no zeta(1) to divide by."
                    : OddOrderMessage);
        }

        int half = order / 2;

        // (-1)^(k+1): positive at odd k, negative at even k.
        BigInteger sign = half % 2 == 0 ? BigInteger.MinusOne : BigInteger.One;

        BigRational denominator =
            BigRational.FromInteger(sign) *
            EvenBernoulli(half) *
            BigRational.FromInteger(BigInteger.Pow(2, order));

        return BigRational.FromInteger(2 * Factorial(order)) / denominator;
    }

    /// <summary>The smallest denominator bound under which this order's answer is reachable.</summary>
    /// <param name="order">The order of zeta. Even, and at least two.</param>
    /// <returns>The denominator of <see cref="Of"/>, in lowest terms.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="order"/> is below two or odd.</exception>
    /// <remarks>
    /// <b>A run bounded below this cannot find this answer, and will report an empty survivor set
    /// saying so.</b> That is a false refutation of a true answer, which
    /// <c>SPEC-rational-ratio.md</c> § 2 forbids in exactly that direction - and it is decidable in
    /// advance, which is why this is a member rather than something a caller reads off
    /// <see cref="Of"/> and remembers to compare. The denominators run 1 through n = 10, then 691,
    /// 2, 3617, 43867, 174611 and on; the default schedule reaches <c>Q = 11,585</c>, so order 18
    /// is the first that outruns it.
    /// </remarks>
    public static BigInteger ReachableFrom(int order) => Of(order).Denominator;

    /// <summary><c>B_(2j)</c>, from the defining recurrence.</summary>
    /// <param name="half">The half-index, at least one.</param>
    /// <returns><c>B_(2j)</c>.</returns>
    /// <remarks>
    /// From <c>sum over i = 0..m of C(m+1,i) * B_i = 0</c>, which fixes <c>B_m</c> once the earlier
    /// ones are known. Odd-index values above the first vanish and are not stored, but
    /// <c>B_1 = -1/2</c> is needed inside the sum and is supplied inline. The binomial coefficient
    /// is advanced multiplicatively rather than recomputed, which keeps the whole thing integer
    /// arithmetic over a rational accumulator.
    /// </remarks>
    private static BigRational EvenBernoulli(int half)
    {
        var cache = new List<BigRational>(half);

        while (cache.Count < half)
        {
            int m = 2 * (cache.Count + 1);

            // sum over i < m of C(m+1, i) * B_i, with C advanced multiplicatively.
            BigRational total = BigRational.One;
            BigInteger binomial = m + 1;
            total += binomial * new BigRational(-1, 2);

            for (int i = 2; i < m; i++)
            {
                binomial = binomial * (m + 2 - i) / i;
                if (i % 2 == 0)
                {
                    total += binomial * cache[(i / 2) - 1];
                }
            }

            cache.Add(-total / (m + 1));
        }

        return cache[half - 1];
    }

    /// <summary><c>n!</c>, which the identity needs at the order itself rather than at the half.</summary>
    private static BigInteger Factorial(int n)
    {
        BigInteger product = BigInteger.One;

        for (int factor = 2; factor <= n; factor++)
        {
            product *= factor;
        }

        return product;
    }

    /// <summary>Renders a generated value the way section 1 lists it, for a message.</summary>
    /// <param name="order">The order of zeta. Even, and at least two.</param>
    /// <returns><c>6</c>, <c>638512875/691</c>, and so on.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="order"/> is below two or odd.</exception>
    /// <remarks>
    /// Here rather than at a call site because a refusal naming this value and a test asserting it
    /// must agree on the spelling, and a denominator of 1 is written out by
    /// <see cref="BigRational"/>'s own formatting in a way section 1's list does not.
    /// </remarks>
    public static string Format(int order)
    {
        BigRational value = Of(order);

        return value.Denominator.IsOne
            ? value.Numerator.ToString(CultureInfo.InvariantCulture)
            : string.Create(CultureInfo.InvariantCulture, $"{value.Numerator}/{value.Denominator}");
    }
}
