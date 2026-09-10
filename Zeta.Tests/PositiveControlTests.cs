using System.Globalization;
using System.Numerics;

namespace HalHeinrich.Numerics.Tests;

/// <summary>
/// The positive controls of <c>SPEC-rational-ratio.md</c> section 4, driven through the whole
/// pipeline: <c>pi^2/zeta(2) = 6</c>, <c>pi^4/zeta(4) = 90</c> and <c>pi^6/zeta(6) = 945</c>,
/// enclosed tightly enough that no rival of low denominator stands beside them.
/// </summary>
/// <remarks>
/// <para>
/// <b>Three of a set that no longer has a last member, and section 4's criterion is asserted
/// elsewhere.</b> That row was widened on 2026-09-08 to the even n = 2...16 and again on 2026-09-09
/// to <see cref="EvenZetaRatio"/>'s generated set, which runs to any even order without limit -
/// <c>halheinrich/Math#68</c> - and it now reads as a survivor set: under a bound fixed in
/// advance, exactly the answer. <see cref="SurvivorSetControlTests"/> asserts that at every even
/// order from 2 to 16, including the denominators 691, 2 and 3617 that alone can tell a
/// denominator claim from a height claim. The tests here assert on the trend matrix, which section
/// 2 keeps as presentation, and widening <i>them</i> stays held under <c>halheinrich/Math#65</c>.
/// What they add is the tie between the generated value and the pipeline's own answer, and the
/// halting rule seen driving two providers to different depths.
/// </para>
/// <para>
/// <b>These are not the controls in <c>RealConstants.Tests/EvenZetaControlTests</c>, and neither
/// makes the other redundant.</b> That one pins the <i>premise</i> at two fixed depths - one
/// <c>Pow</c>, one divide, nothing else - so that a failure here means the pipeline is broken
/// rather than the identity misremembered. These ones run the pipeline: the refiner choosing two
/// different depths from the propagated error, the sweep, and the trend matrix. Collapsing them
/// would give up exactly the distinction that makes a red test here diagnosable.
/// </para>
/// <para>
/// <b>The zeta provider must not touch pi, and that is why this is a control at all.</b>
/// zeta(2n) is a rational multiple of pi^(2n), so a provider computing zeta(2) as pi^2/6 would
/// make every assertion below pass by construction, on any value of pi whatever.
/// <see cref="EulerMaclaurinZeta"/> reaches zeta(s) from reciprocal powers and Bernoulli numbers
/// and never forms pi at all. Any future control added here inherits that constraint.
/// </para>
/// <para>
/// <b>zeta(6) carries double duty, stated rather than left to be noticed.</b> The
/// central-binomial family stops at s = 4 - measured, zeta(6) over its sum is 2.02385..., no
/// rational coefficient - so <see cref="EulerMaclaurinZeta"/> at order 6 has no independent
/// partner, and section 4 names it as one of the two constants with none. For orders 2 and 4 a
/// failure here is disambiguated by the cross-checks in <c>RealConstants.Tests</c>; for order 6
/// it is not, and a red row at 945 alone cannot say whether the pipeline or the provider is at
/// fault. Pairing providers is <c>RealConstants</c>' business and is not rebuilt here.
/// </para>
/// <para>
/// <b>Nothing here can tell <c>Pow</c> from repeated multiplication, and no assertion should be
/// added claiming otherwise.</b> Measured: replacing the <c>Pow</c> in
/// <see cref="RatioEnclosure"/> with a chain of multiplications leaves all seventeen control
/// assertions green and shows up only as extra provider steps. That is not a hole - the refiner
/// drives to a target on the <i>realised</i> error, so a wider propagation is absorbed by
/// refining further, and the difference the dependency problem makes is structurally invisible
/// downstream of an adaptive halt. It is visible at a fixed depth, which is where it is tested:
/// the same mutation reddens <c>RatioEnclosureTests</c> and
/// <c>EvenZetaControlTests.PowIsNotRepeatedMultiplication</c>.
/// </para>
/// <para>
/// <b>Cost.</b> The true ratio is an exact rational of tiny height, so the sweep terminates at
/// denominator 1 on its first candidate and the run's whole price is provider refinement -
/// measured on this bench at 80-160 ms per order in Release and 91-158 ms in Debug for the
/// schedule below. Each theory case re-runs the pipeline rather than sharing a cached run;
/// at this price that is the cheaper thing to read.
/// </para>
/// </remarks>
public sealed class PositiveControlTests
{
    /// <summary>
    /// The schedule. Five columns, each target a wide step below the last, so an apparent plateau
    /// would have to survive four order-of-magnitude tightenings rather than one.
    /// </summary>
    /// <remarks>
    /// Both providers here converge smoothly enough that the realised bound tracks the target to
    /// within a decade - measured 1e-5, 1e-8, 1e-16, 1e-32 and 1e-64 against the five targets.
    /// That is a property of these providers and is assumed nowhere else: see
    /// <see cref="NegativeControlTests"/>, whose provider overshoots its target by seven orders of
    /// magnitude and whose schedule is bounded for that reason.
    /// <para>
    /// <b>Written out rather than built by <see cref="TargetSchedule.Decades"/>, deliberately.</b>
    /// The exponents double, so no fixed step reaches them, and the overload that would - one
    /// taking an arbitrary list - cannot be valid by construction, since the caller chooses the
    /// order. It would therefore have to reject a list that does not descend, which is the
    /// ordering rule <see cref="RatioRun"/> owns written down a second time. Listing them here
    /// and letting <see cref="RatioRun.Execute"/> validate once is the arrangement that keeps
    /// that rule in one place.
    /// </para>
    /// </remarks>
    private static readonly BigRational[] Targets =
    [
        TargetSchedule.Decade(4),
        TargetSchedule.Decade(8),
        TargetSchedule.Decade(16),
        TargetSchedule.Decade(32),
        TargetSchedule.Decade(64),
    ];

    /// <summary>The three answers, so that each control can be asked to refute the other two.</summary>
    private static readonly int[] Answers = [6, 90, 945];

    private static string Inv(FormattableString message) => message.ToString(CultureInfo.InvariantCulture);

    private static RatioRun Control(int order) =>
        RatioRun.Execute(new MachinPi(), order, new EulerMaclaurinZeta(order), Targets);

    [Theory]
    [InlineData(2, 6)]
    [InlineData(4, 90)]
    [InlineData(6, 945)]
    public void TheAnswerIsTheOnlyRationalTheSearchEverProposes(int order, int answer)
    {
        // The weaker half of the control, and it is worth being explicit that it is the weaker
        // half: a pipeline that returned its first candidate at every depth, refining nothing,
        // would satisfy every assertion in this method. What makes finding 6/1 mean anything is
        // the enclosure closing around it, which is the next test's job.
        RatioRun run = Control(order);
        BigRational expected = BigRational.FromInteger(answer);

        Assert.Equal(Targets.Length, run.Iterations.Count);

        foreach (RatioIteration iteration in run.Iterations)
        {
            Assert.Single(iteration.Candidates);
            Assert.Equal(expected, iteration.Simplest.Value);
            Assert.True(iteration.Simplest.IsEnclosed, "The answer must lie inside its own iteration's enclosure.");

            // Denominator 1, so section 1's height is the numerator rather than the sweep index.
            Assert.Equal(BigInteger.One, iteration.Simplest.Value.Denominator);
            Assert.Equal(new BigInteger(answer), iteration.Simplest.Height);
        }

        // One candidate, ever, across the whole run: the matrix has exactly one row.
        TrendRow row = Assert.Single(run.Matrix.Rows);
        Assert.Equal(expected, row.Candidate);
        Assert.Equal(0, row.FirstSeenAt);
    }

    [Theory]
    [InlineData(2, 6)]
    [InlineData(4, 90)]
    [InlineData(6, 945)]
    public void TheEnclosureClosesAroundTheAnswer_WhichIsWhatFindingItIsWorth(int order, int answer)
    {
        // Section 4 asks that the survivor set under a bound fixed in advance be *exactly* the
        // target, and "exactly" is two claims. That the target is in the set is the previous
        // test's, and a stuck pipeline gets it for nothing. That nothing else is in the set is
        // this one's, and it is the half a stuck pipeline fails: the set is a singleton only once
        // the enclosure is narrow enough to exclude every rival of denominator at or below the
        // bound, which section 2 sizes at eps < 1/(2Q) for exactly the rational-target case these
        // controls live in. An enclosure that stops narrowing leaves rivals standing however long
        // the run goes on. So what is asserted here is that narrowing: the row's cells are the
        // exact |answer - x_k|, and they fall by sixty orders of magnitude while staying inside an
        // enclosure that falls with them. The survivor set itself is SurvivorSetControlTests'
        // assertion, at every even order from 2 to 16.
        RatioRun run = Control(order);
        TrendRow row = Assert.Single(run.Matrix.Rows);

        for (int column = 1; column < run.Iterations.Count; column++)
        {
            Assert.True(
                run.Matrix.Ratios[column].MaxError < run.Matrix.Ratios[column - 1].MaxError,
                Inv($"Column {column}'s enclosure was no tighter than column {column - 1}'s."));

            Assert.True(
                row.Distances[column] < row.Distances[column - 1],
                Inv($"The distance to {answer} did not fall between columns {column - 1} and {column}."));
        }

        Assert.True(
            row.Distances.All(distance => distance.Sign > 0),
            "A distance of exactly zero would mean the ratio had been computed exactly, which no truncation does.");

        Assert.True(
            row.Distances[^1] < TargetSchedule.Decade(64),
            Inv($"The final distance to {answer} was {row.Distances[^1]}."));

        Assert.True(
            run.Matrix.Ratios[^1].MaxError < TargetSchedule.Decade(64),
            Inv($"The final enclosure was {run.Matrix.Ratios[^1].MaxError} wide."));
    }

    [Theory]
    [InlineData(2, 6)]
    [InlineData(4, 90)]
    [InlineData(6, 945)]
    public void TheAnswerTheseControlsUseIsTheOneSectionOnesIdentityGenerates(int order, int answer)
    {
        // The two Bernoulli implementations in this umbrella, held against each other through the
        // only thing that can compare them: the value they imply for pi^n/zeta(n). EvenZetaRatio
        // runs the recurrence directly; EulerMaclaurinZeta runs its own privately, for corrections
        // to a sum of reciprocal powers, and the enclosure below is what that produces after a
        // Pow, a divide and five refinements. Nothing shares code between them.
        //
        // ../AGENTS.md section Testing discipline calls cross-checking independent implementations
        // the strongest correctness test available here, and it is what stops a recurrence written
        // out twice - which halheinrich/Math#68 forced, RealConstants exposing no surface to reuse -
        // from being a mistake written out twice.
        Assert.Equal(BigRational.FromInteger(answer), EvenZetaRatio.Of(order));
        Assert.True(
            Control(order).Iterations[^1].Enclosure.Ratio.Contains(EvenZetaRatio.Of(order)),
            Inv($"The generated value for order {order} fell outside the pipeline's final enclosure."));
    }

    [Theory]
    [InlineData(2, 6)]
    [InlineData(4, 90)]
    [InlineData(6, 945)]
    public void TheFinalEnclosureRefutesEveryNeighbourOfTheAnswer(int order, int answer)
    {
        // A control that merely contained its answer would be satisfied by an enclosure a
        // thousand wide. This is the demand EvenZetaControlTests makes of the premise, made here
        // of what the pipeline actually drove the providers to, and it is what a run narrowing
        // around the wrong value would fail.
        RatioRun run = Control(order);
        Approximation final = run.Iterations[^1].Enclosure.Ratio;
        BigRational expected = BigRational.FromInteger(answer);

        Assert.True(final.Contains(expected), Inv($"The final enclosure lost {answer}."));

        Assert.False(final.Contains(expected - BigRational.One));
        Assert.False(final.Contains(expected + BigRational.One));
        Assert.False(final.Contains(expected - TargetSchedule.Decade(40)));
        Assert.False(final.Contains(expected + TargetSchedule.Decade(40)));

        // And the three answers are not interchangeable. Without this, a pipeline that produced
        // some integer for every order would read as healthy as one producing the right integer.
        foreach (int other in Answers.Where(candidate => candidate != answer))
        {
            Assert.False(
                final.Contains(BigRational.FromInteger(other)),
                Inv($"pi^{order}/zeta({order}) contained the wrong answer {other}."));
        }
    }

    [Theory]
    [InlineData(2)]
    [InlineData(4)]
    [InlineData(6)]
    public void TheTwoProvidersAreDrivenToDifferentDepths_WhichIsTheHaltingRuleVisible(int order)
    {
        // Section 2 step 4, in the one place a control can watch it work. The target is on the
        // ratio, so the refiner spends its steps where the propagated error is: pi is raised to a
        // power, which amplifies its share, and Machin gains fewer digits per step than
        // Euler-Maclaurin does. Measured, pi runs about twice as deep. A pipeline halting on a
        // single shared depth, or on either component's own error, would not produce this gap.
        RatioRun run = Control(order);

        for (int column = 1; column < run.Iterations.Count; column++)
        {
            Assert.True(run.Iterations[column].BaseStep > run.Iterations[column - 1].BaseStep);
            Assert.True(run.Iterations[column].DivisorStep > run.Iterations[column - 1].DivisorStep);
        }

        RatioIteration last = run.Iterations[^1];
        Assert.True(
            last.BaseStep > last.DivisorStep,
            Inv($"Expected pi to be driven deeper; got pi {last.BaseStep}, zeta {last.DivisorStep}."));
    }
}
