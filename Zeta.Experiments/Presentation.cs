using System.Globalization;
using System.Numerics;

namespace HalHeinrich.Numerics.Experiments;

/// <summary>
/// Formatting for the experiment tables: exact rationals rendered to decimal, magnitudes rendered
/// as decimal exponents, and the blame split normalised.
/// </summary>
/// <remarks>
/// <para>
/// <b>This is the one place decimals are allowed, and only because it is the last one.</b>
/// <c>../AGENTS.md</c> § Exactness discipline bans floating point in a computational path and
/// permits formatting to decimal at presentation. Nothing here feeds back into a value or a
/// bound: every figure an experiment prints is computed in exact rationals and passes through
/// this file on its way to the console and nowhere else.
/// </para>
/// <para>
/// The decimal conversion is exact truncation of an exact rational, not a floating-point
/// division - <c>numerator * 10^places / denominator</c> in integers, with the point inserted
/// afterwards. The <see cref="double"/> in <see cref="DecimalExponent"/> is the only one in this
/// repository, it estimates a magnitude for a column, and it is never compared against anything.
/// </para>
/// <para>
/// <b>Known duplication, filed rather than fixed.</b> <c>RealConstants.Experiments</c> has its
/// own <c>Presentation</c> with the same three of these - truncation to decimal, the decimal
/// exponent, and the earned prefix - and that is one rule about to exist in two members. The
/// remedy is a published formatting surface, which is a change spanning two repositories that
/// nobody has planned, and <c>../AGENTS.md</c> § Submodule boundary forbids reaching into the
/// other member from here. Recorded so the next reader does not mistake it for an oversight.
/// </para>
/// </remarks>
internal static class Presentation
{
    /// <summary>Renders an exact rational to a fixed number of decimal places, truncated.</summary>
    public static string ToDecimal(BigRational value, int places)
    {
        BigInteger scale = BigInteger.Pow(10, places);
        BigInteger scaled = value.Numerator * scale / value.Denominator;

        bool negative = scaled.Sign < 0;
        BigInteger magnitude = BigInteger.Abs(scaled);

        string digits = (magnitude / scale).ToString(CultureInfo.InvariantCulture);
        string fraction = (magnitude % scale).ToString(CultureInfo.InvariantCulture).PadLeft(places, '0');

        return (negative ? "-" : string.Empty) + digits + "." + fraction;
    }

    /// <summary>The base-ten exponent of a positive rational, to the accuracy of a double.</summary>
    /// <remarks>
    /// The difference of the two <see cref="BigInteger.Log10(BigInteger)"/> values, which is exact
    /// to double precision at any size. Presentation only - a bound is never compared against
    /// this, only printed from it.
    /// </remarks>
    public static double DecimalExponent(BigRational value) =>
        value.Sign <= 0
            ? double.NegativeInfinity
            : BigInteger.Log10(value.Numerator) - BigInteger.Log10(value.Denominator);

    /// <summary>Renders a magnitude as a signed decimal exponent, e.g. <c>1e-12.04</c>.</summary>
    public static string Magnitude(BigRational value) =>
        value.Sign <= 0
            ? "exact"
            : string.Create(CultureInfo.InvariantCulture, $"1e{DecimalExponent(value):F2}");

    /// <summary>
    /// Renders only the decimal digits an enclosure actually pins: the prefix its lower and upper
    /// bounds agree on.
    /// </summary>
    /// <param name="enclosure">The enclosure.</param>
    /// <param name="maxPlaces">How many decimal places to consider before giving up.</param>
    /// <returns>
    /// The shared prefix, <c>?</c> where the two bounds agree on nothing, and the prefix with a
    /// trailing <c>...</c> where it reaches <paramref name="maxPlaces"/>.
    /// </returns>
    /// <remarks>
    /// <para>
    /// <c>../VISION.md</c> § Guiding principles: <i>an unearned digit should be unrepresentable,
    /// not merely discouraged.</i> A walk exists to show what the pipeline is doing, and rendering
    /// every value to a fixed width would present the first column's noise as precision.
    /// </para>
    /// <para>
    /// The prefix is taken character by character from the two <b>truncated</b> renderings, and
    /// truncation is the right rounding at both ends: a digit the two share after truncating is a
    /// digit they share exactly, and a digit truncation separates - <c>1.29999</c> against
    /// <c>1.30000</c> - is one the enclosure genuinely does not pin. Comparing the whole rendering
    /// rather than the fractional part is what makes <c>[9.9, 10.1]</c> come out as nothing
    /// earned: the integer parts differ in width, so no character position agrees, which is the
    /// truth where a digit count derived from the bound would have said one.
    /// </para>
    /// </remarks>
    public static string Earned(Approximation enclosure, int maxPlaces)
    {
        string low = ToDecimal(enclosure.Lower, maxPlaces);
        string high = enclosure.IsExact ? low : ToDecimal(enclosure.Upper, maxPlaces);

        int shared = 0;
        while (shared < low.Length && shared < high.Length && low[shared] == high[shared])
        {
            shared++;
        }

        if (shared == 0)
        {
            return "?";
        }

        return shared == low.Length && shared == high.Length ? low + "..." : low[..shared];
    }

    /// <summary>
    /// The two shares of the propagated error as fractions of one, in the order
    /// <c>(power, divisor)</c>.
    /// </summary>
    /// <param name="enclosure">The composed enclosure.</param>
    /// <returns>
    /// Two non-negative rationals summing to one, or two zeroes when the propagated error is
    /// zero - which happens only when both operands are exact, so there is no blame to apportion.
    /// </returns>
    /// <remarks>
    /// <para>
    /// <b>The shares are absolute parts of the propagated error, not fractions of one</b>, as
    /// <see cref="RatioEnclosure.PowerShare"/> documents directly. Printing them as percentages
    /// without this step gives 0.00% for both at any realistic bound, which reads as a pipeline
    /// that has stopped rather than as a formatting mistake - an umbrella probe of this walk did
    /// exactly that first time, and the code reads plausibly either way.
    /// </para>
    /// <para>
    /// Taking the enclosure rather than two loose rationals is the point: the denominator is not
    /// the caller's to choose, and a signature that let it be chosen is the same defect one step
    /// removed. The sum is used rather than <see cref="RatioEnclosure.PropagatedError"/> so the
    /// two returned values provably total one, which they would not if the split and the total
    /// ever disagreed.
    /// </para>
    /// </remarks>
    public static (BigRational Power, BigRational Divisor) BlameSplit(RatioEnclosure enclosure)
    {
        ArgumentNullException.ThrowIfNull(enclosure);

        BigRational total = enclosure.PowerShare + enclosure.DivisorShare;

        if (total.IsZero)
        {
            return (BigRational.Zero, BigRational.Zero);
        }

        BigRational power = enclosure.PowerShare / total;
        return (power, BigRational.One - power);
    }

    /// <summary>Renders a fraction of one as a percentage to one decimal place.</summary>
    public static string Percent(BigRational fraction) =>
        string.Create(CultureInfo.InvariantCulture, $"{ToDecimal(fraction * BigRational.FromInteger(100), 1)}%");

    /// <summary>
    /// How many decimal digits <see cref="Approximation.Pow"/> cost, as the drop in decimal
    /// exponent between a base's bound and its power's.
    /// </summary>
    /// <param name="enclosure">The composed enclosure.</param>
    /// <returns>The cost in decimal digits, or an empty string where either bound is not positive.</returns>
    /// <remarks>
    /// Printed because it is the reason the walk keeps pi and pi^n in separate columns rather
    /// than describing the relationship in prose. Measured on this bench at 0.80 digits for
    /// pi^2 - the derivative of x^2 at pi is 2*pi, and log10(2*pi) is 0.798 - so a base pinned to
    /// 1e-9.00 gives a square pinned to 1e-8.20.
    /// </remarks>
    public static string PowCost(RatioEnclosure enclosure)
    {
        ArgumentNullException.ThrowIfNull(enclosure);

        if (enclosure.PowerBase.MaxError.Sign <= 0 || enclosure.Power.MaxError.Sign <= 0)
        {
            return string.Empty;
        }

        // Digits LOST, so the power's exponent less the base's: -8.20 against -8.99 is +0.80.
        // The other order gives the same magnitude with the wrong sign, and a negative cost
        // printed under a column headed Pow reads as the power being sharper than its base.
        double cost = DecimalExponent(enclosure.Power.MaxError) - DecimalExponent(enclosure.PowerBase.MaxError);
        return string.Create(CultureInfo.InvariantCulture, $"{cost:F2}");
    }
}
