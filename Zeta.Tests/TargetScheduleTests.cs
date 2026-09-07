using System.Globalization;
using System.Numerics;

namespace HalHeinrich.Numerics.Tests;

/// <summary>
/// The schedule builder: that it produces the exponents it says it does, and that what it
/// produces is a schedule <see cref="RatioRun"/> accepts without the builder ever checking that
/// rule itself.
/// </summary>
/// <remarks>
/// The last of those is the point of the type and is asserted by <b>running</b> a schedule through
/// <see cref="RatioRun.Execute"/> rather than by restating the ordering rule here. Valid by
/// construction is a claim about what the builder cannot emit, and only the validator that owns
/// the rule can settle it.
/// </remarks>
public sealed class TargetScheduleTests
{
    private static string Inv(FormattableString message) => message.ToString(CultureInfo.InvariantCulture);

    /// <summary>
    /// The oracle: <c>10^-power</c>, built from integers rather than from
    /// <see cref="TargetSchedule.Decade"/>.
    /// </summary>
    /// <remarks>
    /// <b>This is the one copy of that expression that must not be retired into
    /// <see cref="TargetSchedule"/>, and it is a deliberate second spelling rather than the
    /// duplication step 6c removed elsewhere.</b> Every other private copy was a caller restating
    /// the library; this one is what the library is checked against, and an oracle written in
    /// terms of the thing under test cannot fail when that thing is wrong. Stated here because a
    /// later reader sweeping for the retired helper will find this and be right to ask.
    /// </remarks>
    private static BigRational TenToTheMinus(int power) =>
        new(BigInteger.One, BigInteger.Pow(10, power));

    // ---------- what it produces ----------

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(11)]
    [InlineData(64)]
    public void Decade_IsTenToTheMinusTheExponent(int exponent)
    {
        Assert.Equal(TenToTheMinus(exponent), TargetSchedule.Decade(exponent));
    }

    [Fact]
    public void Decade_WithANegativeExponent_GivesAWholePowerOfTen()
    {
        // The other side of the sign, where the oracle above cannot go: BigInteger.Pow refuses a
        // negative exponent, so this one is written as the integer it should be.
        Assert.Equal(BigRational.FromInteger(1000), TargetSchedule.Decade(-3));
    }

    [Fact]
    public void Decades_WalksEveryPowerOfTenFromTheFirstExponentToTheLast()
    {
        IReadOnlyList<BigRational> schedule = TargetSchedule.Decades(2, 5);

        Assert.Equal(
            [TenToTheMinus(2), TenToTheMinus(3), TenToTheMinus(4), TenToTheMinus(5)],
            schedule);
    }

    [Fact]
    public void Decades_WithAStep_TightensByThatManyDecadesEachColumn()
    {
        // The negative control's schedule, which is what this overload exists to spell.
        IReadOnlyList<BigRational> schedule = TargetSchedule.Decades(3, 9, 2);

        Assert.Equal(
            [TenToTheMinus(3), TenToTheMinus(5), TenToTheMinus(7), TenToTheMinus(9)],
            schedule);
    }

    [Fact]
    public void Decades_WithAStepThatDoesNotDivideTheSpan_StopsShortRatherThanOvershooting()
    {
        // 2, 4, 6 and not 8: the last exponent is a limit not to pass, so a caller asking for
        // every second decade down to 1e-7 gets a schedule that ends above it rather than below.
        IReadOnlyList<BigRational> schedule = TargetSchedule.Decades(2, 7, 2);

        Assert.Equal([TenToTheMinus(2), TenToTheMinus(4), TenToTheMinus(6)], schedule);
    }

    [Fact]
    public void Decades_WithTheSameExponentTwice_GivesOneColumn()
    {
        IReadOnlyList<BigRational> schedule = TargetSchedule.Decades(4, 4);

        Assert.Equal([TenToTheMinus(4)], schedule);
    }

    [Fact]
    public void Decades_WithNegativeExponents_GivesTargetsAboveOne()
    {
        // 10^-(-1) is 10, which is a positive target and a well-formed first column for a run
        // that starts loose. The exponents ascend whatever their sign, so the targets descend.
        IReadOnlyList<BigRational> schedule = TargetSchedule.Decades(-1, 1);

        Assert.Equal(
            [BigRational.FromInteger(10), BigRational.One, new BigRational(1, 10)],
            schedule);
    }

    // ---------- valid by construction ----------

    [Theory]
    [InlineData(1, 6, 1)]
    [InlineData(0, 4, 2)]
    [InlineData(-2, 3, 1)]
    [InlineData(3, 3, 1)]
    [InlineData(2, 9, 3)]
    public void Decades_ProducesAScheduleRatioRunAccepts(int first, int last, int step)
    {
        // The claim the type rests on, put to the validator that owns the rule rather than
        // re-checked here. A stub base and divisor keep this about the schedule: the enclosures
        // are nested and the ratio is exactly 3/2, so the sweep terminates immediately and the
        // only thing under test is whether Execute refused the targets.
        IReadOnlyList<BigRational> schedule = TargetSchedule.Decades(first, last, step);

        RatioRun run = RatioRun.Execute(
            StubConstant.Halving(BigRational.FromInteger(3), BigRational.One),
            1,
            StubConstant.Halving(BigRational.FromInteger(2), new BigRational(1, 4)),
            schedule);

        Assert.Equal(schedule.Count, run.Iterations.Count);
    }

    [Theory]
    [InlineData(1, 6, 1)]
    [InlineData(0, 4, 2)]
    [InlineData(-2, 3, 1)]
    [InlineData(2, 9, 3)]
    public void Decades_DescendsStrictly_AndEndsPositive(int first, int last, int step)
    {
        // The same property from the other side, stated as arithmetic so a failure says which
        // half broke. Execute's refusal above cannot distinguish the two.
        IReadOnlyList<BigRational> schedule = TargetSchedule.Decades(first, last, step);

        Assert.NotEmpty(schedule);
        Assert.True(schedule[^1].Sign > 0, "The last target must be strictly positive.");

        for (int column = 1; column < schedule.Count; column++)
        {
            Assert.True(
                schedule[column] < schedule[column - 1],
                Inv($"Column {column} did not tighten on column {column - 1}."));
        }
    }

    // ---------- guards ----------

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    public void Decades_RejectsAStepBelowOne(int step)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => TargetSchedule.Decades(2, 8, step));
    }

    [Fact]
    public void Decades_RejectsALastExponentBeforeTheFirst()
    {
        // A schedule that loosens, which is the one shape this builder could otherwise emit that
        // RatioRun would then refuse. Caught on the argument rather than on the result.
        Assert.Throws<ArgumentOutOfRangeException>(() => TargetSchedule.Decades(8, 2));
    }

    [Fact]
    public void Decades_RejectsASpanNoArrayCouldHold()
    {
        // The two exponents at opposite ends of int, whose difference does not fit in one. The
        // span is computed in long for that reason, so this fails on its own arithmetic rather
        // than wrapping to a small positive count and silently building the wrong schedule.
        Assert.Throws<ArgumentOutOfRangeException>(
            () => TargetSchedule.Decades(int.MinValue, int.MaxValue));

        // It refuses on the count, before raising ten to anything, which is why this test costs
        // nothing. A version that built the array first would not return at all.
    }

    // No test covers an exponent at int.MinValue, and that is stated rather than left as a gap.
    // The builder raises a tenth to +exponent so that it never has to negate one, but the case
    // that distinguishes that spelling from its mirror cannot be exhibited: either form asks for
    // a number with 2^31 decimal digits and fails by exhausting memory rather than by returning
    // a wrong answer. ../AGENTS.md section Testing discipline - an artefact that cannot exhibit
    // the property claimed about it stops claiming it, and the claim here lives in the code
    // comment as a representability fact, not as a defect this suite caught.
}
