namespace HalHeinrich.Numerics;

/// <summary>
/// Builds the schedules of error targets a <see cref="RatioRun"/> is driven to: a run of powers
/// of ten, evenly spaced in the exponent.
/// </summary>
/// <remarks>
/// <para>
/// Every consumer of this pipeline wants "run to error tau in k columns", and before this type
/// each of them spelled it out - a private <c>TenToTheMinus</c> helper and a hand-written array
/// literal, once per caller. <c>../AGENTS.md</c> section Writing code calls that a library gap
/// wearing a disguise: repeated consumer glue.
/// </para>
/// <para>
/// <b>This validates its own arguments and deliberately not the shape of the schedule, and that
/// is a decision rather than an omission.</b> What comes out is strictly decreasing with a
/// positive last element <i>by construction</i>, so there is nothing here to check; and checking
/// it anyway would put the rule <see cref="RatioRun"/> already owns in a second place. Step 6e
/// ruled exactly that question and left <c>ConstantRun</c>'s copy of the rule standing rather than
/// publish a validator: exposing one publishes a policy while leaving every caller free to skip
/// it, and the real single-sourcing is a validated schedule type reached through a factory - a
/// change spanning two repositories that nobody has planned. Until that exists, the rule lives in
/// <see cref="RatioRun"/> and this type stays out of its way.
/// </para>
/// <para>
/// <b>That is also why there is no overload taking an arbitrary list of exponents.</b> Such an
/// overload cannot produce a valid schedule by construction - the caller chooses the order - so it
/// would have to reject a list that does not descend, which is the ordering rule written down a
/// second time. A caller wanting exponents that no fixed step reaches, such as the doubling
/// <c>4, 8, 16, 32, 64</c>, writes them out and hands them to <see cref="RatioRun.Execute"/>,
/// which validates them once and in the place that owns the rule.
/// </para>
/// </remarks>
public static class TargetSchedule
{
    private const string StepMessage =
        "The step must be at least 1. It is measured in decades of the exponent, so a step of " +
        "zero repeats one target forever and a negative one ascends, and neither is a schedule.";

    private const string RangeMessage =
        "The last exponent must be at or after the first. Exponents grow as the targets tighten, " +
        "so a last exponent before the first describes a schedule that loosens.";

    private const string TooManyMessage =
        "The exponents span more columns than an array can hold.";

    // A tenth raised to +exponent, rather than ten raised to -exponent. The two name the same
    // number and only one of them is always representable: negating int.MinValue does not fit in
    // an int, and BigRational.Pow already carries that one case by taking the reciprocal and
    // holding the magnitude in a long, which a negation performed here would undo.
    //
    // Unobservable, and said so rather than implied. At that exponent either spelling asks for a
    // number with 2^31 decimal digits, so both fail by exhausting memory and neither returns a
    // wrong answer quickly. No test can exhibit the difference; this is the correct spelling
    // because it has no negation to get wrong, not because a suite caught the other one.
    private static readonly BigRational OneTenth = new(1, 10);

    /// <summary>
    /// The powers of ten from <c>10^-firstExponent</c> through <c>10^-lastExponent</c>, taking
    /// <paramref name="step"/> decades at a time.
    /// </summary>
    /// <param name="firstExponent">The first target's exponent, as <c>10^-firstExponent</c>.</param>
    /// <param name="lastExponent">
    /// The exponent not to pass. The last target is the deepest one <paramref name="step"/>
    /// reaches at or before it, so a step that does not divide the span stops short rather than
    /// overshooting.
    /// </param>
    /// <param name="step">How many decades each column tightens by. At least 1.</param>
    /// <returns>
    /// The schedule, strictly decreasing and all strictly positive, so
    /// <see cref="RatioRun.Execute"/> accepts it. Never empty: a first equal to a last gives one
    /// column.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="step"/> is below 1, <paramref name="lastExponent"/> is before
    /// <paramref name="firstExponent"/>, or the span is too long to hold.
    /// </exception>
    /// <remarks>
    /// <para>
    /// The exponents are unrestricted in sign, and negative ones are honest rather than merely
    /// tolerated: <c>10^-(-1)</c> is 10, which is a positive target and a perfectly well-formed
    /// first column for a run that starts loose. What the sign cannot do is break the ordering,
    /// since the exponents ascend whatever their sign and so the targets descend.
    /// </para>
    /// <para>
    /// The span is computed in <see cref="long"/> because the two exponents may sit at opposite
    /// ends of <see cref="int"/>, where their difference does not fit in one. That is arithmetic
    /// on this method's own arguments, and it is checked here for the same reason the two rules
    /// above are not: it is nobody else's rule.
    /// </para>
    /// </remarks>
    public static IReadOnlyList<BigRational> Decades(int firstExponent, int lastExponent, int step = 1)
    {
        if (step < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(step), step, StepMessage);
        }

        if (lastExponent < firstExponent)
        {
            throw new ArgumentOutOfRangeException(nameof(lastExponent), lastExponent, RangeMessage);
        }

        long span = (long)lastExponent - firstExponent;
        long count = (span / step) + 1;

        if (count > int.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(lastExponent), lastExponent, TooManyMessage);
        }

        var targets = new BigRational[count];
        for (int column = 0; column < targets.Length; column++)
        {
            // In long, then narrowed: the running exponent stays inside int because it never
            // passes lastExponent, but the addition that produces it can leave the range.
            long exponent = firstExponent + ((long)column * step);
            targets[column] = BigRational.Pow(OneTenth, (int)exponent);
        }

        return targets;
    }
}
