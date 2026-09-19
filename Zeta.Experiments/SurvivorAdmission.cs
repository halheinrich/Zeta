using System.Globalization;

namespace HalHeinrich.Numerics.Experiments;

/// <summary>
/// What <see cref="SurvivorWalkChoice.Admit"/> decided about a run: refused, or admitted with a
/// bound and a limit.
/// </summary>
/// <remarks>
/// <para>
/// <b>A value, printed by nobody who made it.</b> <see cref="SurvivorRun.Run"/> matches on it and
/// prints; each admitted walk supplies its own lines as pure functions of what it holds, so the
/// walk-specific text is decided where the walk is and rendered where the printing is, and neither
/// place asks which walk it holds.
/// </para>
/// </remarks>
internal abstract record SurvivorAdmission;

/// <summary>A run its walk's guard turned down.</summary>
/// <param name="Message">
/// The refusal as the command prints it, rendered from the refusal's value by the pure function
/// that owns it - <see cref="SurvivorRefusal.Message"/>,
/// <see cref="SurvivorRun.RefuseUnaffordableControl"/> or <see cref="SurvivorCountGuard.Describe"/>.
/// </param>
internal sealed record RefusedWalk(string Message) : SurvivorAdmission;

/// <summary>A run its walk's guard let through: how far to walk, under what limit, and what to say about it.</summary>
/// <param name="Bound">The bound to walk to, and where it came from.</param>
/// <param name="Limit">The survivor limit the report enforces during the walk.</param>
internal abstract record AdmittedWalk(SurvivorBound Bound, SurvivorLimit Limit) : SurvivorAdmission
{
    /// <summary>The lines that say, before the walk, what it will cost and why.</summary>
    /// <param name="enclosures">Every enclosure the run will intersect, in order.</param>
    /// <param name="mode">Which walk: every prefix, or the final one alone.</param>
    /// <returns>The lines, in order.</returns>
    public abstract IReadOnlyList<string> Sizing(IReadOnlyList<Approximation> enclosures, SurvivorMode mode);

    /// <summary>The line that says, after the walk, how well the guard predicted it.</summary>
    /// <param name="report">What the walk produced.</param>
    /// <param name="mode">Which walk it was.</param>
    /// <param name="walkSeconds">How long the walk took, by the clock around it.</param>
    /// <returns>The lines, in order; possibly none.</returns>
    public abstract IReadOnlyList<string> Reading(SurvivorReport report, SurvivorMode mode, BigRational walkSeconds);
}

/// <summary>
/// A <see cref="DenominatorWalk"/> run admitted by the calibrated time budget: the price it was
/// measured at, and the bound that budget allows.
/// </summary>
/// <param name="Bound">The derived bound, or for a deep run the budget's cap below it.</param>
/// <param name="Price">What a turn of each loop was measured to cost.</param>
/// <remarks>
/// Its lines are the ones the command printed before a walk could be chosen, moved and not
/// reworded: ruling 3 on <c>halheinrich/Math#79</c> leg 2 keeps the reference walk's time model
/// unchanged. It walks under <see cref="SurvivorLimit.None"/>.
/// </remarks>
internal sealed record TimedWalk(SurvivorBound Bound, WalkPrice Price) : AdmittedWalk(Bound, SurvivorLimit.None)
{
    /// <inheritdoc/>
    public override IReadOnlyList<string> Sizing(IReadOnlyList<Approximation> enclosures, SurvivorMode mode)
    {
        WalkSize size = SurvivorRun.Size(enclosures, Bound.Q, mode);
        string micro = Presentation.Roughly(Price.PerDenominator * SurvivorRun.Million);

        if (mode == SurvivorMode.Deep)
        {
            WalkSize chart = SurvivorRun.Size(enclosures, Bound.Q, SurvivorMode.Chart);
            string affordable = Bound.Affordable is { } most ? most.ToString(CultureInfo.InvariantCulture) : "unbounded";
            var lines = new List<string>
            {
                Inv($"  a sample of the deep walk itself to q = {Price.SmallSample} priced a denominator at {micro} microseconds -"),
                "  one price, since there is almost nothing else in it.",
                Inv($"  Q = {affordable} is what the budget of {SurvivorRun.BudgetSeconds} s affords at that price, so this run walks to"),
                Bound.IsCapped
                    ? Inv($"  Q = {Bound.Q} - CAPPED below the derived bound. Ruling 5: claiming less than the precision")
                    : Inv($"  Q = {Bound.Q}, the derived bound, which the budget affords."),
            };

            if (Bound.IsCapped)
            {
                lines.Add("  supports is always sound, and the epilogue says what the cap does to the reading.");
            }

            lines.Add(Inv($"  the walk is ONE pass over all {enclosures.Count} enclosures, seeded from the narrowest: {size.Denominators:N0} denominators"));
            lines.Add(Inv($"  and {size.Candidates:N0} candidates, where the chart would walk {chart.Denominators:N0} and {chart.Candidates:N0}."));
            lines.Add(Inv($"  It predicts {Presentation.Roughly(Price.Seconds(size))} s. The price is this machine's; the answer is not."));

            return lines;
        }

        return
        [
            Inv($"  the walk is {size.Denominators:N0} denominators and {size.Candidates:N0} candidates over all {enclosures.Count} prefixes,"),
            Inv($"  of which the opening step is {SurvivorRun.Estimate([enclosures[0]], Bound.Q, SurvivorMode.Chart):N0}."),
            Inv($"  samples to q = {Price.SmallSample} and q = {Price.LargeSample} priced a denominator at {micro} microseconds"),
            Inv($"  and a candidate at {Presentation.Roughly(Price.PerCandidate * SurvivorRun.Million)}, so the walk predicts {Presentation.Roughly(Price.Seconds(size))} s against a budget of {SurvivorRun.BudgetSeconds}."),
            "  The price is this machine's; the answer is not.",
        ];
    }

    /// <inheritdoc/>
    /// <remarks>
    /// The predicted-to-realised ratio is printed so the guard's own error is on the screen of
    /// every run; <see cref="SurvivorRun.Calibrate"/>'s remarks carry why no band is promised.
    /// </remarks>
    public override IReadOnlyList<string> Reading(SurvivorReport report, SurvivorMode mode, BigRational walkSeconds)
    {
        ArgumentNullException.ThrowIfNull(report);

        if (walkSeconds.Sign <= 0)
        {
            return [];
        }

        BigRational predicted = Price.Seconds(SurvivorRun.Size(report.Enclosures, Bound.Q, mode));

        return [Inv($"The guard predicted {Presentation.Roughly(predicted)} s for that walk: {Presentation.Roughly(predicted / walkSeconds)} of what it took.")];
    }

    private static string Inv(FormattableString text) => text.ToString(CultureInfo.InvariantCulture);
}

/// <summary>
/// A <see cref="FareyWalk"/> run admitted by the survivor count: the limit it walks under and the
/// count expected of it.
/// </summary>
/// <param name="Bound">The derived bound, which nothing caps.</param>
/// <param name="Limit">The survivor limit, checked again as the walk runs.</param>
/// <param name="Expected">
/// The survivors <see cref="SurvivorCountGuard.Expected"/> predicted over every walk the run makes.
/// </param>
internal sealed record CountedWalk(SurvivorBound Bound, SurvivorLimit Limit, BigRational Expected) : AdmittedWalk(Bound, Limit)
{
    /// <inheritdoc/>
    public override IReadOnlyList<string> Sizing(IReadOnlyList<Approximation> enclosures, SurvivorMode mode)
    {
        ArgumentNullException.ThrowIfNull(enclosures);

        string walks = mode == SurvivorMode.Chart
            ? Inv($"over all {enclosures.Count} prefixes")
            : "over its one walk";

        return
        [
            Inv($"  {SurvivorWalkChoice.Farey.Name} costs about log Q plus one step a survivor, so the survivors it finds are its"),
            "  cost, and a count can be predicted where a time could only be sampled - no calibration.",
            Inv($"  it expects about {Presentation.Roughly(Expected)} survivors {walks} (6*h*Q^2/pi^2, each walk at its"),
            Inv($"  narrowest enclosure) against a limit of {Limit.Count:N0}, and counts them again as it goes."),
        ];
    }

    /// <inheritdoc/>
    public override IReadOnlyList<string> Reading(SurvivorReport report, SurvivorMode mode, BigRational walkSeconds)
    {
        ArgumentNullException.ThrowIfNull(report);

        return [Inv($"The guard expected about {Presentation.Roughly(Expected)} survivors over the walks; they found {report.SurvivorsWalked:N0}.")];
    }

    private static string Inv(FormattableString text) => text.ToString(CultureInfo.InvariantCulture);
}
