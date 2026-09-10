using System.Globalization;
using System.Numerics;

namespace HalHeinrich.Numerics.Tests;

/// <summary>
/// The positive controls of <c>SPEC-rational-ratio.md</c> section 4 in the form that section now
/// states them: under a denominator bound fixed in advance, the survivor set of <c>pi^n/zeta(n)</c>
/// is exactly the known answer - at every even order from 2 to 16, through real providers, the
/// refiner's halting rule and <see cref="SurvivorSearch"/>.
/// </summary>
/// <remarks>
/// <para>
/// <b>What is asserted, and what is not.</b> Section 2 made the survivor set the result on
/// 2026-09-08 and the trend matrix presentation, and until these controls no control asserted
/// that criterion at any order. What they test is the pipeline: that the enclosures it actually drives
/// the providers to leave the answer standing, and nothing beside it. They do <i>not</i> test the
/// rules that say when that must happen. Soundness in the worst case, tightness at an integer
/// target, tightness at a bound chosen from the right residue class, and the predicate against
/// the search itself are all pinned in <c>RationalApproximation.Tests/SurvivorSearchTests</c>,
/// beside the members they govern; a second copy here would be the rule tested in two places.
/// This file uses those members only to size itself.
/// </para>
/// <para>
/// <b>Sized through the library, by looping on the predicate.</b> <see cref="DenominatorBound"/>
/// is checked with <see cref="SurvivorSearch.IsReachable"/> and then the refiner is driven until
/// <see cref="SurvivorSearch.IsIsolated"/> holds of the half-width it actually holds. It is not
/// driven to <see cref="SurvivorSearch.ExclusiveIsolationBound"/>, because
/// <see cref="RatioRefiner.RefineTo"/> stops at or below its target and reads the
/// <i>coarsened</i> bound, which <see cref="Approximation.Coarsen"/> rounds up to a power of two -
/// so where the isolation bound is itself a power of two, an enclosure can land exactly on it. That
/// enclosure still leaves the answer alone in fact, but no longer proves it, and a control asserts
/// a proof. It is not hypothetical: measured on 2026-09-10 in a scratchpad probe at this bound, the
/// realised half-width landed exactly on the isolation bound at n = 2 and at n = 14, and the loop
/// took one step more. The final enclosure's isolation is asserted as well as looped on, so the
/// sizing is checked rather than assumed - and that assertion is the one that carries weight:
/// sizing by refining to the exclusive bound instead turned exactly n = 2 and n = 14 red, both
/// at that assertion, when tried the same day.
/// </para>
/// <para>
/// <b>Why <c>Q = 4096</c>, one bound for every order.</b> <c>Q = den X</c> would be the weakest
/// claim that still isolates, and at n = 2 to 10 it is <c>Q = 1</c>: a claim about integers alone,
/// which a walk of one denominator settles. A single bound instead makes every control the same
/// claim, and 4096 is the least power of two at or above every denominator gated here - 3617, at
/// n = 16. That buys two things. Each singleton refutes the other seven answers too, since the
/// bound reaches every one of them, so a pipeline returning some control's answer
/// at every order fails here without a separate assertion saying so. And being a power of two, it
/// makes the isolation bound a power of two wherever the answer's denominator is 1 or 2, which is
/// exactly the coarsening case above - so the loop that guards it is exercised by real providers
/// and not only by the library's own fixtures.
/// </para>
/// <para>
/// <b>Why 16, and why this is not <c>MaxOrder</c> returning.</b> <c>MaxOrder = 16</c> refused to
/// run anything higher, on a ground <c>halheinrich/Math#68</c> showed false; everything above 16
/// still runs, as the <c>survivors</c> and <c>deep</c> experiments, and is checked there against
/// <see cref="EvenZetaRatio"/>'s generated answer. What stops at 16 here is only what gates CI, and
/// the reason is cost. n = 18's denominator is 43867, so no bound below that reaches its answer,
/// and the survivor walk alone - 43867 denominators against enclosure endpoints some 1800 digits
/// long - took 4.3 to 5.0 s in Release in the same probe at that bound, where every case below
/// takes a fraction of a second. <c>../AGENTS.md</c> § Exactness discipline keeps a run that long out of a test
/// project.
/// </para>
/// <para>
/// <b>The zeta provider must not touch pi, and that is why this is a control at all.</b>
/// zeta(2n) is a rational multiple of pi^(2n), so a provider forming zeta(2) as pi^2/6 would make
/// every assertion below pass by construction, on any value of pi whatever.
/// <see cref="EulerMaclaurinZeta"/> reaches zeta(s) from reciprocal powers and Bernoulli numbers and
/// never forms pi, and any control added here inherits that constraint.
/// </para>
/// <para>
/// <b>Added beside <see cref="PositiveControlTests"/>, not in place of it.</b> Those assert on the
/// trend matrix and on each iteration's simplest candidate, which section 2 now keeps as
/// presentation, and they stay: they watch the halting rule drive two providers to different
/// depths, which nothing here looks at. This file is the criterion section 4 names.
/// </para>
/// <para>
/// <b>Cost.</b> Provider refinement is a few milliseconds at every order; the price is the walk
/// over 4096 denominators, and it rises with the order because each step rounds the enclosure's
/// endpoints against the next denominator - endpoints that ran from under a hundred digits at
/// n = 2 to some 1300 at n = 16 in the probe. Measured on this bench on 2026-09-10 with this class
/// run alone: 20 to 405 ms a case, n = 16 the dearest, and about 1.2 s for the eight, in Debug and
/// Release alike - presumably because the price is <see cref="BigInteger"/> arithmetic in the
/// runtime rather than code this build compiles. Inside the whole suite, with other classes running
/// beside it, no case exceeded 452 ms.
/// </para>
/// </remarks>
public sealed class SurvivorSetControlTests
{
    /// <summary>The denominator bound every control claims, fixed before any run.</summary>
    /// <remarks>The class remarks carry why this value and why one value for every order.</remarks>
    private static readonly BigInteger DenominatorBound = 4096;

    /// <summary>
    /// How many refinements the sizing loop may ask for before it is taken to have stalled.
    /// </summary>
    /// <remarks>
    /// <b>A guard that reddens in milliseconds, where the defect would otherwise hang CI</b> -
    /// <c>../AGENTS.md</c> § Testing discipline. Every pass asks for at most half the half-width
    /// held, so sixty-four passes narrow it by a factor of 2^64 or more; the controls here took
    /// fourteen at most in the probe. A loop reaching this is one whose step stopped narrowing - a
    /// refine to the bound already held, say - and without the cap it would spin for ever rather
    /// than fail.
    /// </remarks>
    private const int MaxRefinements = 64;

    private static string Inv(FormattableString message) => message.ToString(CultureInfo.InvariantCulture);

    [Theory]
    [InlineData(2)]
    [InlineData(4)]
    [InlineData(6)]
    [InlineData(8)]
    [InlineData(10)]
    [InlineData(12)]
    [InlineData(14)]
    [InlineData(16)]
    public void TheSurvivorSetUnderABoundFixedInAdvanceIsExactlyTheAnswer(int order)
    {
        BigRational answer = EvenZetaRatio.Of(order);

        // Reachability first: below the answer's denominator it is not a candidate at all, and the
        // set would come back empty - a false refutation, which IsIsolated refuses to answer about.
        Assert.True(
            SurvivorSearch.IsReachable(answer, DenominatorBound),
            Inv($"Q = {DenominatorBound} cannot reach pi^{order}/zeta({order}) = {answer}."));

        List<Approximation> enclosures = EnclosuresIsolating(order, answer);

        Assert.True(
            SurvivorSearch.IsIsolated(answer, DenominatorBound, enclosures[^1].MaxError),
            Inv($"The final enclosure's half-width {enclosures[^1].MaxError} does not isolate {answer} at Q = {DenominatorBound}."));

        // Every enclosure the run realised, not only the last: an earlier one that excluded the
        // answer would empty the set, and that is the refutation of a true answer this bench
        // exists never to report.
        List<BigRational> survivors = [.. SurvivorSearch.Survivors(enclosures, DenominatorBound)];

        Assert.Equal([answer], survivors);
    }

    /// <summary>
    /// Drives the pipeline for this order until its latest enclosure isolates the answer, and
    /// returns every enclosure it realised on the way, first to last.
    /// </summary>
    /// <param name="order">The order of zeta.</param>
    /// <param name="answer">The value that enclosure must isolate.</param>
    /// <returns>At least one enclosure; the last is the narrowest.</returns>
    /// <remarks>
    /// Each pass asks the refiner for half the half-width it holds, which forces at least one
    /// provider step and costs nothing extra: the refiner is incremental, and it re-forms the ratio
    /// after every step whatever target it was given. The loop ends for any refinement whose
    /// half-width tends to zero - <see cref="SurvivorSearch.ExclusiveIsolationBound"/>'s remarks
    /// carry why - and <see cref="MaxRefinements"/> turns a loop that stopped narrowing into a
    /// failure.
    /// </remarks>
    private static List<Approximation> EnclosuresIsolating(int order, BigRational answer)
    {
        using RatioRefiner refiner = new(new MachinPi(), order, new EulerMaclaurinZeta(order));
        List<Approximation> enclosures = [refiner.Current.Ratio];
        int refinements = 0;

        while (!SurvivorSearch.IsIsolated(answer, DenominatorBound, refiner.Current.Ratio.MaxError))
        {
            if (refinements == MaxRefinements)
            {
                Assert.Fail(Inv($"{MaxRefinements} refinements did not isolate {answer}; the loop has stopped narrowing."));
            }

            refiner.RefineTo(refiner.Current.Ratio.MaxError / 2);
            enclosures.Add(refiner.Current.Ratio);
            refinements++;
        }

        return enclosures;
    }
}
