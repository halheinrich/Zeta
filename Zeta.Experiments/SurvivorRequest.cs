using System.Globalization;

namespace HalHeinrich.Numerics.Experiments;

/// <summary>How a survivor run walks its enclosures, and so what it can draw.</summary>
/// <remarks>
/// <para>
/// <b>Both modes report the same survivor set</b>, and that is the whole of why the second one is
/// allowed to exist. <see cref="SurvivorSearch"/> intersects every enclosure it is handed and seeds
/// its walk from the narrowest, so one walk over all of them returns exactly what the chart's last
/// prefix returns. What differs is everything else the chart draws - ruling 4 on
/// <c>halheinrich/Math#64</c> - and <see cref="SurvivorReport.Omitted"/> says which panels.
/// </para>
/// <para>
/// <b>The chart is the default and stays it.</b> The collapse is what makes a refutation legible to
/// a reader, and <c>../SPEC-rational-ratio.md</c> § 2 keeps it as presentation. The deep walk is
/// the deliberate trade of that picture for reach, taking literally § 1's point that the
/// deliverable is the bound rather than the picture.
/// </para>
/// </remarks>
internal enum SurvivorMode
{
    /// <summary>
    /// One walk per prefix of the enclosures: the collapse chart, the nearest-excluded family and
    /// the survivor set. The default, and the <c>survivors</c> command.
    /// </summary>
    Chart,

    /// <summary>
    /// One walk over every enclosure: the survivor set and nothing that needs a prefix. The
    /// <c>deep</c> command.
    /// </summary>
    Deep,
}

/// <summary>
/// What one <c>survivors</c> or <c>deep</c> invocation asks for: an order of zeta, the two ends of
/// the schedule the run is driven to, and how the enclosures are walked.
/// </summary>
/// <param name="Order">The order of zeta, as in <c>pi^n / zeta(n)</c>.</param>
/// <param name="FirstExponent">The first target's exponent, as <c>10^-firstExponent</c>.</param>
/// <param name="LastExponent">The last target's exponent, as <c>10^-lastExponent</c>.</param>
/// <param name="Mode">Which command it came from, and so how the enclosures are walked.</param>
/// <remarks>
/// <para>
/// <b>This validates nothing, and that is the same decision <see cref="TargetSchedule"/> records
/// for itself.</b> The rule about what shape a schedule may take belongs at the command edge -
/// <see cref="SurvivorRun.RefuseSchedule"/> owns it, because that is where the argument arrives and
/// where a refusal can say something about the experiment rather than about a parameter name.
/// Repeating the check here would encode one rule in two places, which <c>../AGENTS.md</c>
/// § Writing code calls a defect: a correction lands on one copy and the other survives to
/// contradict it. So every member below is defined for a request
/// <see cref="SurvivorRun.Interpret"/> produced, and undefined for one hand-built out of range.
/// </para>
/// <para>
/// It exists so the parse has somewhere to put its answer. <c>../CLAUDE.md</c> § Shell records
/// that the three defects which ever reached a push in this project all lived behind an input the
/// session could not exercise, and that what removed them was splitting reading an input from
/// interpreting it. This is the value that split produces: everything downstream of the argument
/// array is a request some test can hand to a function.
/// </para>
/// </remarks>
internal readonly record struct SurvivorRequest(
    int Order, int FirstExponent, int LastExponent, SurvivorMode Mode)
{
    /// <summary>The command this request came from, as a caller types it.</summary>
    public string Command => SurvivorRun.CommandFor(Mode);

    /// <summary>The schedule as every report here names it: <c>1e-2 .. 1e-8</c>.</summary>
    /// <remarks>
    /// Spelled once because two reports carry it - the preamble on stderr and the SVG's caption -
    /// and they must agree. They agreed by coincidence while the ends were constants; with the
    /// ends a parameter, a second spelling is a second thing to forget to update.
    /// </remarks>
    public string ScheduleLabel => string.Create(CultureInfo.InvariantCulture,
        $"1e-{FirstExponent} .. 1e-{LastExponent}");

    /// <summary>The error targets this request asks the run to be driven to.</summary>
    /// <returns>The schedule, one target per decade.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <see cref="LastExponent"/> precedes <see cref="FirstExponent"/> - which
    /// <see cref="SurvivorRun.RefuseSchedule"/> has already rejected for any request
    /// <see cref="SurvivorRun.Interpret"/> produced.
    /// </exception>
    public IReadOnlyList<BigRational> Schedule() => TargetSchedule.Decades(FirstExponent, LastExponent);
}
