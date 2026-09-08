using System.Globalization;

namespace HalHeinrich.Numerics.Tests;

/// <summary>
/// The negative control of <c>SPEC-rational-ratio.md</c> section 4: <c>sqrt(2)/sqrt(3)</c>, two
/// independently enclosed irrationals, where each iteration's enclosure must exclude the previous
/// iteration's simplest candidate.
/// </summary>
/// <remarks>
/// <para>
/// <b>What is asserted, and why it cannot pass on a detector that has stopped detecting.</b> The
/// tempting assertion - "the simplest candidate was unstable for k rounds" - is unusable here.
/// Section 2 records a candidate holding steady for two consecutive iterations, twice, on a run
/// whose answer was genuinely irrational, so a test built on that criterion at k = 2 goes green
/// on a pipeline that has stopped narrowing. What is asserted instead is structural and decidable
/// one iteration at a time: <b>each iteration's enclosure excludes the previous iteration's
/// simplest candidate</b>. That is precisely what "no rational fits" means, it assumes nothing
/// about the sweep's monotonicity, and it cannot be satisfied by a broken pipeline - a run that
/// stopped narrowing, or narrowed around the wrong value, would keep returning the same simplest
/// candidate, and the next enclosure would contain it. It equally cannot be satisfied by a run
/// that had found a rational answer: that answer would be found early and stay found.
/// </para>
/// <para>
/// <b>Nothing here asserts on the survivor set yet, and that is a gap rather than a ruling.</b>
/// Section 2's 2026-09-08 amendment makes the survivor set the criterion, so the sharpest form
/// this control could take is the direct one: under a denominator bound <c>Q</c>, the survivors of
/// these enclosures are <i>none</i>, the ratio being irrational. That is strictly stronger than
/// the exclusion property above, which only ever looks at one candidate per iteration. It is not
/// written because it cannot be written honestly yet - nothing in <c>Zeta</c> reaches
/// <c>SurvivorSearch</c>, and the pipeline that would is the reshape held under
/// <c>halheinrich/Math#65</c>. Choosing <c>Q</c> is also a measurement rather than a guess: this
/// is already the expensive control, realising about 1.8e-12 and sweeping to denominator 576180,
/// and section 2's sizing makes an emptiness claim at a <c>Q</c> worth asserting a cost that has
/// to be measured before it is committed to. Add it with that pipeline, against the shape that
/// gets kept.
/// </para>
/// <para>
/// <b>The shared engine is not a violation of section 4's independence ruling, and should not be
/// "fixed".</b> Both constants come from <see cref="NewtonSquareRoot"/> - one engine, two
/// radicands. That ruling forbids a shared engine for <i>cross-checks</i>, where a single defect
/// displacing both values would produce agreement at exactly the moment agreement is worthless. A
/// negative control has no such failure mode: a shared engine cannot manufacture a plateau, and
/// the run is refuting rationals rather than corroborating a value.
/// </para>
/// <para>
/// <b>Cost, and the target this stops at.</b> This is the expensive control - sweep depth runs as
/// the inverse square root of the realised error, so the price is set by how tight the enclosure
/// actually came out. Measured on this bench for the schedule below: 280-522 ms in Release,
/// 454-776 ms in Debug, sweeping to denominator 576180 at the last column. The reported result is
/// section 1's denominator bound: every rational of denominator at or below that misses the final
/// enclosure, for any numerator.
/// </para>
/// <para>
/// <b>Why the schedule stops where it does.</b> <see cref="NewtonSquareRoot"/> converges
/// quadratically, so its bound descends in a staircase and the <i>realised</i> error overshoots
/// the target by as much as seven orders of magnitude. Every target from 1e-9 to 1e-11 lands on
/// the same pair of provider steps and realises about 1.8e-12; the next step down realises about
/// 1e-18, whose sweep is some 1e9 denominators deep and would not finish. Moving the last target
/// one notch therefore hangs CI rather than reddening it, which is why
/// <see cref="TheScheduleStopsShortOfTheStepThatWouldMakeTheSweepUnrunnable"/> exists: it pays
/// for the refinement alone, no sweep, and turns that hang into a red test.
/// </para>
/// </remarks>
public sealed class NegativeControlTests
{
    /// <summary>
    /// The schedule: 1e-3, 1e-5, 1e-7, 1e-9. Four columns, each realising a distinct enclosure -
    /// about 1e-5, 1e-6, 1e-9 and 1e-12 - and stopping one provider step short of the sweep that
    /// cannot be run.
    /// </summary>
    private static readonly IReadOnlyList<BigRational> Targets = TargetSchedule.Decades(3, 9, 2);

    /// <summary>
    /// The one run these tests read, computed once.
    /// </summary>
    /// <remarks>
    /// A <see cref="RatioRun"/> is an immutable snapshot of a deterministic computation, so
    /// sharing one across the class is safe; the alternative is paying half a second four times
    /// for identical numbers. The positive controls re-run per case instead, because there the
    /// price does not justify the indirection.
    /// </remarks>
    private static readonly Lazy<RatioRun> SharedRun = new(
        () => RatioRun.Execute(new NewtonSquareRoot(2), 1, new NewtonSquareRoot(3), Targets));

    private static string Inv(FormattableString message) => message.ToString(CultureInfo.InvariantCulture);

    [Fact]
    public void EachEnclosureRefutesThePreviousIterationsSimplestCandidate()
    {
        // The assertion the control turns on; see the class remarks for why it is this one and
        // not a stability count.
        RatioRun run = SharedRun.Value;

        Assert.Equal(Targets.Count, run.Iterations.Count);

        for (int column = 1; column < run.Iterations.Count; column++)
        {
            BigRational refuted = run.Iterations[column - 1].Simplest.Value;

            Assert.False(
                run.Iterations[column].Enclosure.Ratio.Contains(refuted),
                Inv($"Column {column} still admitted {refuted.Numerator}/{refuted.Denominator}, column {column - 1}'s simplest candidate."));
        }
    }

    [Fact]
    public void TheDenominatorsGrow_SoEachRefutationCoversAWiderRangeThanTheLast()
    {
        // Section 1's deliverable is a denominator bound, and it is only worth reporting if it
        // rises. Every candidate is already in lowest terms with denominator equal to its sweep
        // index, so this is the depth the sweep reached.
        RatioRun run = SharedRun.Value;

        for (int column = 1; column < run.Iterations.Count; column++)
        {
            Assert.True(
                run.Iterations[column].Simplest.Value.Denominator > run.Iterations[column - 1].Simplest.Value.Denominator,
                Inv($"Column {column} searched no deeper than column {column - 1}."));

            Assert.True(
                run.Matrix.Ratios[column].MaxError < run.Matrix.Ratios[column - 1].MaxError,
                Inv($"Column {column}'s enclosure was no tighter than column {column - 1}'s."));
        }

        // The value below 1, so section 1's height is the denominator here rather than the
        // numerator - the opposite of the positive controls, and the reason both are worth having.
        RationalCandidate last = run.Iterations[^1].Simplest;
        Assert.Equal(last.Value.Denominator, last.Height);
    }

    [Fact]
    public void EveryRowPlateausExceptTheOneTheFinalEnclosureStillAdmits()
    {
        // The matrix-shaped restatement of the criterion, which section 4 admits only with the
        // FirstSeenAt clause below. Section 2 step 6 read off the matrix until the 2026-09-08
        // amendment made membership the criterion; what this test now does is check that the
        // matrix agrees with membership, not read a trend off it. A row's last cell is
        // |candidate - x_last|, so the row is refuted exactly when that exceeds the final
        // enclosure's error - and after a run that has refuted everything it entertained, exactly
        // one candidate is left standing, namely the last column's own.
        RatioRun run = SharedRun.Value;
        BigRational finalError = run.Matrix.Ratios[^1].MaxError;

        TrendRow standing = Assert.Single(run.Matrix.Rows, row => row.Distances[^1] <= finalError);
        Assert.Equal(run.Iterations[^1].Simplest.Value, standing.Candidate);

        // And it is a candidate the run has only just found. Without this clause the assertion
        // above is satisfied by a pipeline that stopped narrowing after its first column - every
        // column then surfaces the same candidate, and exactly one row still stands, trivially.
        // Measured: it passed under that mutation until this line was added.
        Assert.Equal(run.Iterations.Count - 1, standing.FirstSeenAt);

        // And no candidate sits exactly on the ratio. Every one of them - the standing one
        // included - is a rational at a fixed nonzero distance from an irrational, so a zero cell
        // could only mean the pipeline had lost the true value out of its enclosures or surfaced a
        // candidate no search produced. That is a claim about each cell on its own; it says
        // nothing about where a row is heading, which is not what decides anything here.
        Assert.True(
            run.Matrix.Rows.All(row => row.Distances.All(distance => distance.Sign > 0)),
            "No candidate may sit at distance zero from the ratio.");

        Assert.True(run.Matrix.Rows.Count > 1, "A single-row matrix would leave nothing to compare the standing row against.");
    }

    [Fact]
    public void TheRatioSquaredEnclosesTwoThirds_WhichNoSquareRootCodeIsNeededToCheck()
    {
        // The oracle for the target itself, and the reason this control exercises the division.
        // If the composition is right then the ratio r satisfies r^2 = 2/3 exactly, a statement
        // about two small integers that borrows nothing from the provider being tested. A
        // pipeline dividing the wrong pair of enclosures, or losing the true value out of its
        // interval, fails here without any appeal to the digits of a square root.
        RatioRun run = SharedRun.Value;
        var twoThirds = new BigRational(2, 3);

        foreach (RatioIteration iteration in run.Iterations)
        {
            Approximation squared = iteration.Enclosure.Ratio.Pow(2);

            Assert.True(
                squared.Contains(twoThirds),
                Inv($"The square of the enclosure lost 2/3; it was {squared.Value} +- {squared.MaxError}."));
        }

        // Sharp, not merely containing: by the last column the square is pinned to eleven decimal
        // places, so 2/3 is the only rational of any small height it still admits.
        Approximation last = run.Iterations[^1].Enclosure.Ratio.Pow(2);
        Assert.True(last.MaxError < TargetSchedule.Decade(11), Inv($"The final square was {last.MaxError} wide."));
        Assert.False(last.Contains(twoThirds + TargetSchedule.Decade(10)));
        Assert.False(last.Contains(twoThirds - TargetSchedule.Decade(10)));
    }

    [Fact]
    public void TheScheduleStopsShortOfTheStepThatWouldMakeTheSweepUnrunnable()
    {
        // The guard that turns a hang into a failure. This pays for refinement only - no sweep -
        // so it costs milliseconds whatever the answer, and it fails loudly if a change to
        // NewtonSquareRoot moves the staircase under this class's schedule.
        using var refiner = new RatioRefiner(new NewtonSquareRoot(2), 1, new NewtonSquareRoot(3));

        refiner.RefineTo(Targets[^1]);
        BigRational realised = refiner.Current.Ratio.MaxError;

        // Sweep depth goes as the inverse square root of this, so a floor here is a ceiling on
        // the sweep: 1e-13 caps it around three million denominators, which is runnable.
        Assert.True(
            realised > TargetSchedule.Decade(13),
            Inv($"The last target realised {realised}, tighter than this class budgeted for; the sweep it implies may not finish."));

        // One step further down the staircase is the cliff, and it is a cliff rather than a slope.
        refiner.RefineTo(TargetSchedule.Decade(12));
        Assert.True(
            refiner.Current.Ratio.MaxError < TargetSchedule.Decade(17),
            "The provider's next step was expected to overshoot by orders of magnitude; if it no longer does, this class's schedule can be extended.");
    }
}
