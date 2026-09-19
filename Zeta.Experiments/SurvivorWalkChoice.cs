using System.Globalization;
using System.Numerics;

namespace HalHeinrich.Numerics.Experiments;

/// <summary>
/// A survivor walk a run can be asked for, paired once with everything that follows from choosing
/// it: the word a caller types, the name every report prints, the walk itself, and the guard that
/// decides whether and how far it may run.
/// </summary>
/// <remarks>
/// <para>
/// <b>The walk is the user's choice, ruled on <c>halheinrich/Math#79</c> leg 2:</b>
/// <see cref="FareyWalk"/> by default, <see cref="DenominatorWalk"/> by naming it. Each walk has
/// its own cost model - the reference walk's is a calibrated time, the fast walk's a survivor
/// count - so choosing a walk chooses a guard, and this type is where the two are joined. Nothing
/// else in the runner asks which walk it holds.
/// </para>
/// <para>
/// <b>An abstract base with one sealed subclass per walk, closed to this assembly</b>, and not an
/// enumeration with a <c>switch</c> at each site that cares. Three reasons:
/// </para>
/// <list type="bullet">
/// <item><description>
/// The walks differ in <i>behaviour</i>, not only in data: one calibrates a stopwatch and may cap
/// <c>Q</c>, the other predicts a count and never caps. A record of fields could carry the name and
/// the walk, but not what admitting a run means.
/// </description></item>
/// <item><description>
/// <see cref="Admit"/> is abstract, so the compiler checks that every walk supplies every part. A
/// switch repeated at each site can be missed at one of them and still compile.
/// </description></item>
/// <item><description>
/// The set is closed - the constructor is <c>private protected</c> - so <see cref="All"/> is every
/// walk there is, and a test that runs over it covers every walk the runner offers. It is the
/// same shape <see cref="SurvivorSearch"/> takes for the same reason.
/// </description></item>
/// </list>
/// <para>
/// <b><see cref="Admit"/> returns a value and prints nothing</b> - the user's amendment to the
/// plan. The decision is the value; the text is built from it by pure functions, and
/// <see cref="SurvivorRun.Run"/> does the printing. That is the seam <c>../CLAUDE.md</c> § Shell
/// records: whatever a run decides is something a test can hold without running one.
/// </para>
/// </remarks>
internal abstract class SurvivorWalkChoice
{
    /// <summary>Admits implementations from this assembly only, so <see cref="All"/> is every walk.</summary>
    /// <param name="argument">The word a caller types for it.</param>
    /// <param name="search">The walk.</param>
    private protected SurvivorWalkChoice(string argument, SurvivorSearch search)
    {
        Argument = argument;
        Name = search.GetType().Name;
        Walk = search.Survivors;
    }

    /// <summary>Gets <see cref="FareyWalk"/>: about <c>log Q</c> plus the survivors, guarded by a count. The default.</summary>
    public static SurvivorWalkChoice Farey { get; } = new FareyChoice();

    /// <summary>Gets <see cref="DenominatorWalk"/>: the reference, linear in <c>Q</c>, guarded by a calibrated time.</summary>
    public static SurvivorWalkChoice Denominator { get; } = new DenominatorChoice();

    /// <summary>Gets the walk a run uses when none is named: <see cref="Farey"/>.</summary>
    public static SurvivorWalkChoice Default => Farey;

    /// <summary>Gets every walk a run can be asked for, the default first.</summary>
    public static IReadOnlyList<SurvivorWalkChoice> All { get; } = [Farey, Denominator];

    /// <summary>Gets the word a caller types to choose this walk: <c>farey</c> or <c>denominator</c>.</summary>
    /// <remarks>The type's name without <c>Walk</c>, lower case, so the argument and the label are the same word.</remarks>
    public string Argument { get; }

    /// <summary>Gets the walk's name as every report prints it: <c>FareyWalk</c> or <c>DenominatorWalk</c>.</summary>
    /// <remarks>Read off the walk's own type rather than typed, so a label cannot name a walk that did not run.</remarks>
    public string Name { get; }

    /// <summary>Gets the walk, as the report methods take it.</summary>
    public SurvivorWalk Walk { get; }

    /// <summary>Gets whether this is the walk a run uses when none is named.</summary>
    public bool IsDefault => ReferenceEquals(this, Default);

    /// <summary>
    /// Gets the survivor limit this walk runs under when the caller gives none:
    /// <see cref="SurvivorCountGuard.DefaultLimit"/> for <see cref="FareyWalk"/>, and
    /// <see cref="SurvivorLimit.None"/> for <see cref="DenominatorWalk"/>.
    /// </summary>
    public abstract SurvivorLimit DefaultLimit { get; }

    /// <summary>Gets whether a caller may set this walk's survivor limit.</summary>
    /// <remarks>
    /// <b>The one statement of which walks take a limit.</b> <see cref="SurvivorRun.Interpret"/>
    /// asks it to refuse a limit given beside a walk that would ignore it - the reference walk is
    /// bounded by time, and a limit silently dropped would mislead whoever set it - and
    /// <see cref="Admit"/> asks it to refuse a request built past the grammar.
    /// </remarks>
    public bool TakesSurvivorLimit => DefaultLimit.Count is not null;

    /// <summary>The walk a caller named, or null when the word names none.</summary>
    /// <param name="argument">The word as typed. Matched without regard to case, as the commands are.</param>
    /// <returns>The walk, or null.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="argument"/> is null.</exception>
    public static SurvivorWalkChoice? Named(string argument)
    {
        ArgumentNullException.ThrowIfNull(argument);

        return All.FirstOrDefault(choice => string.Equals(choice.Argument, argument, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Decides whether this walk may run over these enclosures, and how far.</summary>
    /// <param name="enclosures">Every distinct enclosure the run realised, in order.</param>
    /// <param name="derived">The bound the final enclosure's precision supports.</param>
    /// <param name="request">What was asked for.</param>
    /// <returns>
    /// A <see cref="RefusedWalk"/>, or an <see cref="AdmittedWalk"/> carrying the bound to walk to,
    /// the survivor limit to walk under, and what its sizing and epilogue lines are built from.
    /// </returns>
    /// <remarks>
    /// Prints nothing. The reference walk's admission takes a timing - its cost model is a
    /// calibrated price - but the timing decides only what to spend, and what comes back is still
    /// a value.
    /// </remarks>
    public abstract SurvivorAdmission Admit(
        IReadOnlyList<Approximation> enclosures, BigInteger derived, SurvivorRequest request);

    /// <summary>What guards this walk, as a label prints it.</summary>
    /// <param name="limit">The survivor limit the run was asked for.</param>
    /// <returns><c>survivor limit 100,000,000</c>, or <c>time budget 300 s</c>.</returns>
    public abstract string DescribeGuard(SurvivorLimit limit);

    /// <inheritdoc/>
    public override string ToString() => Name;

    /// <summary>Refuses a request whose limit does not fit this walk, which only a hand-built one can carry.</summary>
    /// <param name="request">The request being admitted.</param>
    /// <exception cref="ArgumentException">
    /// The request names this walk and a limit it does not take, or takes a limit and names none.
    /// </exception>
    /// <remarks>
    /// An exception rather than a refusal because no argument a caller can type reaches it:
    /// <see cref="SurvivorRun.Interpret"/> refuses the one spelling that would. So it is a defect in
    /// whoever built the request, and a run that ignored the mismatch would walk under a guard
    /// nobody asked for.
    /// </remarks>
    private protected void RequireFittingLimit(SurvivorRequest request)
    {
        if (!ReferenceEquals(request.Walk, this) || TakesSurvivorLimit != (request.Limit.Count is not null))
        {
            throw new ArgumentException(
                "This request pairs a walk with a survivor limit it does not take, or omits the one it " +
                "does. SurvivorRun.Interpret never builds one; a caller building a request by hand " +
                "takes the walk's DefaultLimit.",
                nameof(request));
        }
    }

    /// <summary>
    /// <see cref="FareyWalk"/>, guarded by <see cref="SurvivorCountGuard"/>: no calibration, no
    /// cap, and a refusal when the expected survivors pass the limit.
    /// </summary>
    private sealed class FareyChoice() : SurvivorWalkChoice("farey", new FareyWalk())
    {
        /// <inheritdoc/>
        public override SurvivorLimit DefaultLimit { get; } = SurvivorLimit.At(SurvivorCountGuard.DefaultLimit);

        /// <inheritdoc/>
        public override string DescribeGuard(SurvivorLimit limit) =>
            string.Create(CultureInfo.InvariantCulture, $"survivor limit {limit.Count:N0}");

        /// <inheritdoc/>
        /// <remarks>
        /// Walks to the derived bound, which nothing caps: a count guard refuses rather than
        /// shrinking <c>Q</c>, and <see cref="SurvivorRun.Afford"/> is the reference walk's.
        /// </remarks>
        public override SurvivorAdmission Admit(
            IReadOnlyList<Approximation> enclosures, BigInteger derived, SurvivorRequest request)
        {
            RequireFittingLimit(request);

            SurvivorLimit limit = request.Limit;
            SurvivorCountRefusal? refusal = SurvivorCountGuard.Refuse(enclosures, derived, request.Mode, limit);

            return refusal is not null
                ? new RefusedWalk(SurvivorCountGuard.Describe(refusal, request))
                : new CountedWalk(
                    new SurvivorBound(derived, null),
                    limit,
                    SurvivorCountGuard.Expected(enclosures, derived, request.Mode));
        }
    }

    /// <summary>
    /// <see cref="DenominatorWalk"/>, guarded by the calibrated time budget exactly as before a walk
    /// could be chosen: <see cref="SurvivorRun.Calibrate"/>, then <see cref="SurvivorRun.Afford"/>
    /// capping a deep walk or <see cref="SurvivorRun.Refuse"/> turning a chart down.
    /// </summary>
    private sealed class DenominatorChoice() : SurvivorWalkChoice("denominator", new DenominatorWalk())
    {
        /// <inheritdoc/>
        public override SurvivorLimit DefaultLimit => SurvivorLimit.None;

        /// <inheritdoc/>
        public override string DescribeGuard(SurvivorLimit limit) =>
            string.Create(CultureInfo.InvariantCulture, $"time budget {SurvivorRun.BudgetSeconds} s");

        /// <inheritdoc/>
        /// <remarks>
        /// Unchanged from when this was the only walk - the user's ruling on
        /// <c>halheinrich/Math#79</c> leg 2 keeps the reference walk's time model as it was, and
        /// this is that model's code, moved out of <see cref="SurvivorRun.Run"/> and not otherwise
        /// touched. It walks under <see cref="SurvivorLimit.None"/>: its cost is <c>Q</c>
        /// denominators whatever it finds, which a count says nothing about.
        /// </remarks>
        public override SurvivorAdmission Admit(
            IReadOnlyList<Approximation> enclosures, BigInteger derived, SurvivorRequest request)
        {
            RequireFittingLimit(request);

            WalkPrice price = SurvivorRun.Calibrate(enclosures, derived, request.Mode);

            if (request.Mode == SurvivorMode.Deep)
            {
                // Ruling 5 on halheinrich/Math#64: a deep run is never refused for its cost - its Q
                // comes down to what the budget affords instead. Which makes the unreachable-control
                // check necessary but no longer sufficient, since a cap can fall below an answer
                // the derived bound reached.
                SurvivorBound capped = SurvivorRun.Afford(enclosures, derived, price);
                string? unaffordable = SurvivorRun.RefuseUnaffordableControl(request, capped);

                return unaffordable is not null ? new RefusedWalk(unaffordable) : new TimedWalk(capped, price);
            }

            SurvivorRefusal? tooDear = SurvivorRun.Refuse(enclosures, derived, price);

            return tooDear is { } refused
                ? new RefusedWalk(refused.Message)
                : new TimedWalk(new SurvivorBound(derived, null), price);
        }
    }
}
