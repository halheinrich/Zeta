using System.Globalization;
using System.Numerics;
using System.Text;

namespace HalHeinrich.Numerics.Experiments;

/// <summary>
/// The guard on a <see cref="FareyWalk"/> run: a limit on how many survivors its walks may produce,
/// checked against the expectation before the walk and against the count during it.
/// </summary>
/// <remarks>
/// <para>
/// <b>A count and not a time, ruled on <c>halheinrich/Math#79</c> leg 2.</b>
/// <see cref="FareyWalk"/> costs about <c>log Q</c> plus one step a survivor, so the survivors it
/// finds <i>are</i> its cost, and a count can be predicted exactly where a time can only be
/// sampled: the expectation needs no stopwatch and no calibration, so a run under this guard pays
/// for none. Time may be added later. <see cref="DenominatorWalk"/> keeps
/// <see cref="SurvivorRun.BudgetSeconds"/>, because its cost is <c>Q</c> denominators whatever it
/// finds.
/// </para>
/// <para>
/// <b>Checked twice, because an expectation is not a bound.</b> Before the walk,
/// <see cref="Refuse"/> turns down a run whose expected count passes the limit, which is most of
/// what would pass, before anything is spent. During it, <see cref="SurvivorReport.Of"/> and
/// <see cref="SurvivorReport.Deep"/> stop the moment the real count passes the same limit.
/// </para>
/// </remarks>
internal static class SurvivorCountGuard
{
    /// <summary>The survivor limit when none is given: one hundred million.</summary>
    /// <remarks>
    /// <para>
    /// <b>A policy, not a law, and this is its basis.</b> A scratch probe on 2026-09-18
    /// (<c>halheinrich/Math#79</c>) rebuilt each documented run's enclosures with this bench's
    /// providers and summed <see cref="SurvivorReport.ExpectedSurvivors"/> over the prefixes a
    /// chart walks, each at its narrowest enclosure as <see cref="Expected"/> does. Where it then
    /// walked them with <see cref="FareyWalk"/>, the count matched the sum to three figures:
    /// 401,572 at <c>3 2 8</c> against 4.016e5, 653,615 at <c>2 2 8</c> against 6.577e5,
    /// 3,212,843 at <c>3 2 9</c> against 3.213e6, and 13,090,645 at <c>3 4 11</c> against
    /// 1.309e7.
    /// </para>
    /// <para>
    /// So every chart the runner's usage and the README document is admitted - the default
    /// schedule at orders 2 to 16 expects 3 to 7 hundred thousand, and the largest documented,
    /// <c>3 4 11</c>, 1.31e7, about eight times inside - as is <c>10 4 11</c> at 4.1e7, which the
    /// reference walk's time budget refuses. <c>3 4 13</c>, at 4.2e8, and anything deeper are
    /// refused. Held fixed in that sweep: this bench's two providers and the run's own schedules.
    /// </para>
    /// <para>
    /// <b>What the limit costs in time is set by the report, not by the walk, and it rises with
    /// the order.</b> Measured 2026-09-19 by a scratch probe that timed, on the same enclosures
    /// and back to back, <see cref="FareyWalk"/> enumerating every prefix alone and then
    /// <see cref="SurvivorReport.Of"/> over the same walk - two passes each, at below-normal
    /// priority on a machine under other load, so these are extremes and not a fit:
    /// </para>
    /// <list type="bullet">
    /// <item><description>
    /// order 3, <c>1e-2 .. 1e-9</c>, <c>Q = 32,768</c>, 3,212,843 survivors: the walk 0.26 to
    /// 0.40 microseconds a survivor, the report 2.42 to 2.46;
    /// </description></item>
    /// <item><description>
    /// order 10, the same schedule and <c>Q</c>, 346,273 survivors: the walk 0.32 to 1.93, the
    /// report 21.7 to 32.9;
    /// </description></item>
    /// <item><description>
    /// order 16, <c>1e-2 .. 1e-8</c>, <c>Q = 16,384</c>, 483,866 survivors: the walk 0.59 to
    /// 1.46, the report 38.2 to 38.3.
    /// </description></item>
    /// </list>
    /// <para>
    /// So the report took 6 to 68 times the walk's time on every pass, and its cost a survivor
    /// moved with the order where the walk's did not. Two things the probe did not separate:
    /// which part of the report's work costs it, and the order from the size of the enclosures,
    /// which grows with it - the final centre ran to 148, 738 and 1,402 numerator digits at the
    /// three orders. A full run agrees with the order-10 figure: <c>survivors 10 4 11</c> walked
    /// 40,782,076 survivors in 1,407 s under the same conditions, about 34.5 microseconds each.
    /// At the measured rates a chart that reached this limit would take a few minutes at order 3
    /// and about an hour at order 10 - arithmetic on figures taken under load, not a prediction.
    /// </para>
    /// <para>
    /// None of that is what the limit means: the limit is a count, and a caller who wants a
    /// different policy sets another.
    /// </para>
    /// </remarks>
    public const long DefaultLimit = 100_000_000;

    /// <summary>How many survivors the walks are expected to produce between them.</summary>
    /// <param name="enclosures">Every enclosure the run will intersect, in order.</param>
    /// <param name="denominatorBound">The bound the walks run to.</param>
    /// <param name="mode">Which walk: every prefix, or the final one alone.</param>
    /// <returns>
    /// The upper end of <see cref="SurvivorReport.ExpectedSurvivors"/>'s enclosure, summed over the
    /// walks - every prefix for a chart, the whole list once for a deep run.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="enclosures"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="enclosures"/> is empty.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="denominatorBound"/> is negative, or <paramref name="mode"/> is not a defined mode.
    /// </exception>
    /// <remarks>
    /// <para>
    /// <b>Each walk is sized at its narrowest enclosure</b>, as <see cref="SurvivorRun.Size"/>
    /// sizes the reference walk. A walk's survivors lie inside every enclosure it is given, so
    /// they number no more than its narrowest one admits: sizing at that one can only overstate,
    /// which is the safe direction for a guard. The running minimum is what makes that true of a
    /// list a test hands over in any order.
    /// </para>
    /// <para>
    /// <b>At a derived bound a deep run expects about 0.61, whatever the precision.</b> Its one
    /// walk is sized at the narrowest enclosure, whose half-width is the <c>eps</c> that
    /// <c>Q = floor(eps^(-1/2))</c> came from, so this is <see cref="SurvivorReport.ExpectedUnderBound"/>:
    /// <c>6/pi^2</c> or just below it. So the check before the walk never turns a deep run down at
    /// a derived bound, and the check during it is the one that can.
    /// </para>
    /// <para>
    /// The upper end rather than the value, because <c>pi^2</c> is enclosed and a guard reads the
    /// side that refuses.
    /// </para>
    /// </remarks>
    public static BigRational Expected(
        IReadOnlyList<Approximation> enclosures, BigInteger denominatorBound, SurvivorMode mode)
    {
        ArgumentNullException.ThrowIfNull(enclosures);

        if (enclosures.Count == 0)
        {
            throw new ArgumentException("A run with no enclosures walks nothing and expects nothing.", nameof(enclosures));
        }

        BigRational narrowest = enclosures[0].MaxError;
        BigRational sum = BigRational.Zero;

        foreach (Approximation enclosure in enclosures)
        {
            if (enclosure.MaxError < narrowest)
            {
                narrowest = enclosure.MaxError;
            }

            sum += SurvivorReport.ExpectedSurvivors(narrowest, denominatorBound).Upper;
        }

        return mode switch
        {
            SurvivorMode.Chart => sum,
            SurvivorMode.Deep => SurvivorReport.ExpectedSurvivors(narrowest, denominatorBound).Upper,
            _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "Not a survivor mode."),
        };
    }

    /// <summary>The refusal of a run expected to pass its limit, or null when it may walk.</summary>
    /// <param name="enclosures">Every enclosure the run will intersect, in order.</param>
    /// <param name="denominatorBound">The bound the walks run to.</param>
    /// <param name="mode">Which walk: every prefix, or the final one alone.</param>
    /// <param name="limit">The limit. <see cref="SurvivorLimit.None"/> refuses nothing.</param>
    /// <returns>The refusal, or null.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="enclosures"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="enclosures"/> is empty.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="denominatorBound"/> is negative, or <paramref name="mode"/> is not a defined mode.
    /// </exception>
    /// <remarks>
    /// A pure function of the enclosures, the bound and the limit, so a test holds the refusal
    /// without a walk or a clock. Refused when the expectation is strictly above the limit, the
    /// same comparison <see cref="SurvivorLimit.IsPassedBy"/> makes of a real count.
    /// </remarks>
    public static SurvivorCountRefusal? Refuse(
        IReadOnlyList<Approximation> enclosures, BigInteger denominatorBound, SurvivorMode mode, SurvivorLimit limit)
    {
        BigRational expected = Expected(enclosures, denominatorBound, mode);

        return limit.Count is { } most && expected > BigRational.FromInteger(most)
            ? SurvivorCountRefusal.BeforeTheWalk(
                expected, most, denominatorBound, mode == SurvivorMode.Chart ? enclosures.Count : 1, mode)
            : null;
    }

    /// <summary>A count refusal as the command prints it.</summary>
    /// <param name="refusal">The refusal.</param>
    /// <param name="request">What was asked for, which names the walk and the invocations to offer.</param>
    /// <returns>The message.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="refusal"/> is null.</exception>
    /// <remarks>
    /// <para>
    /// A pure function of two values: <see cref="SurvivorRun.Run"/> prints, this only renders, so
    /// what a refusal says is held by a test that never walks.
    /// </para>
    /// <para>
    /// <b>The advice depends on the mode, because the knobs do.</b> A chart's count is dominated by
    /// its widest prefix, which the <i>first</i> exponent moves and <c>Q</c> does not - the same
    /// knob <see cref="SurvivorRefusal"/> names for the reference walk's time. A deep walk's count
    /// is its final enclosure's, which no schedule change moves from about 0.61; a deep walk
    /// passing its limit is a target sitting among far more rationals than a generic one would,
    /// and the honest advice is the reference walk, or a higher limit.
    /// </para>
    /// </remarks>
    public static string Describe(SurvivorCountRefusal refusal, SurvivorRequest request)
    {
        ArgumentNullException.ThrowIfNull(refusal);

        string walk = request.Walk.Name;
        string count = Presentation.Roughly(refusal.Count);
        var text = new StringBuilder();

        text.Append(refusal.Basis == SurvivorCountBasis.Expected
            ? string.Create(CultureInfo.InvariantCulture,
                $"Refusing this run: {walk} is expected to find about {count} survivors over its " +
                $"{refusal.Prefixes} walk{(refusal.Prefixes == 1 ? "" : "s")}, past the limit of {refusal.Limit:N0}.\n")
            : string.Create(CultureInfo.InvariantCulture,
                $"Refused mid-walk: {walk} passed the limit of {refusal.Limit:N0} survivors" +
                $"{(refusal.PassedAt is { } at ? $" in prefix {at} of {refusal.Prefixes}" : "")}, so there is " +
                $"no report.\n" +
                $"  The expectation checked before the walk did not predict it. An expectation is not a\n" +
                $"  bound, which is why the count is checked as it happens.\n"));

        text.Append(string.Create(CultureInfo.InvariantCulture,
            $"  the bound   Q = {refusal.DenominatorBound}, derived as floor(eps^(-1/2)) from the final enclosure\n" +
            $"  the count   {walk} costs about log Q plus one step a survivor, so its survivors are its\n" +
            $"              cost. The limit counts them across every walk the run makes.\n"));

        if (refusal.Basis == SurvivorCountBasis.Expected)
        {
            text.Append(
                "              Expected as 6*h*Q^2/pi^2 for each walk, sized at its narrowest\n" +
                "              enclosure - an overstatement, which is the safe direction for a guard.\n");
        }

        text.Append('\n');
        text.Append(refusal.Mode == SurvivorMode.Chart
            ? "Raise the FIRST exponent: it leaves Q where it is and shrinks the widest prefix, which\n" +
              "  holds most of the count. What it costs is collapse points; the survivor set is unchanged.\n"
            : "No schedule change helps a deep walk: its one walk expects about 0.61 at a derived Q\n" +
              "  whatever the precision, so passing the limit means the target sits among far more\n" +
              "  rationals than a generic one would.\n");
        text.Append(LimitAdvice(refusal, request));
        text.Append(string.Create(CultureInfo.InvariantCulture,
            $"Or walk with {SurvivorWalkChoice.Denominator.Name}, which is guarded by time instead: " +
            $"'{request.WithWalk(SurvivorWalkChoice.Denominator).Invocation(request.FirstExponent, request.LastExponent)}'."));

        return text.ToString();
    }

    /// <summary>The smallest round limit - one, two or five times a power of ten - at or above a count.</summary>
    /// <param name="count">The count to admit. Non-negative.</param>
    /// <returns>The limit.</returns>
    /// <remarks>
    /// What a refusal suggests when it offers a higher limit: a figure a person would type, and
    /// the smallest such figure that admits the expectation. Exact in integers; the ceiling of the
    /// count is the least whole number of survivors that admits it.
    /// </remarks>
    public static BigInteger RoundLimitAtOrAbove(BigRational count)
    {
        // BigRational keeps a positive denominator, and a count is non-negative, so this is the ceiling.
        BigInteger needed = BigInteger.Max(
            BigInteger.One, (count.Numerator + count.Denominator - BigInteger.One) / count.Denominator);
        BigInteger power = BigInteger.One;

        while (true)
        {
            foreach (int mantissa in new[] { 1, 2, 5 })
            {
                BigInteger candidate = mantissa * power;

                if (candidate >= needed)
                {
                    return candidate;
                }
            }

            power *= 10;
        }
    }

    /// <summary>The line offering a higher limit, with the invocation that sets one.</summary>
    private static string LimitAdvice(SurvivorCountRefusal refusal, SurvivorRequest request)
    {
        if (refusal.Basis == SurvivorCountBasis.Found)
        {
            return string.Create(CultureInfo.InvariantCulture,
                $"Or raise the limit, which is the number after '{request.Walk.Argument}': the walk passed " +
                $"{refusal.Limit:N0}, and how far past is not known, since it stopped there.\n");
        }

        BigInteger suggested = RoundLimitAtOrAbove(refusal.Count);

        return suggested > long.MaxValue
            ? "No survivor limit a run can be given admits this; the count is past any a walk could finish.\n"
            : string.Create(CultureInfo.InvariantCulture,
                $"Or raise the limit, if the count is one you mean to pay for: " +
                $"'{(request with { Limit = SurvivorLimit.At((long)suggested) }).Invocation(request.FirstExponent, request.LastExponent)}'.\n");
    }
}
