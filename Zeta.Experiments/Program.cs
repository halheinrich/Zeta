using System.Globalization;

namespace HalHeinrich.Numerics.Experiments;

/// <summary>
/// Entry point for the Zeta bench's experiments.
/// </summary>
/// <remarks>
/// <para>
/// Run one by name, or list what is available:
/// </para>
/// <code>
/// dotnet run --project Zeta.Experiments -- list
/// dotnet run --project Zeta.Experiments -- walk
/// dotnet run --project Zeta.Experiments -- target
/// dotnet run --project Zeta.Experiments -- target 12
/// dotnet run --project Zeta.Experiments -- survivors &gt; survivors.svg
/// dotnet run --project Zeta.Experiments -- survivors 3 2 9 &gt; survivors3.svg
/// dotnet run --project Zeta.Experiments -- deep 3 4 13 &gt; deep3.svg
/// </code>
/// <para>
/// <c>survivors</c> writes an SVG rather than a table, so its stdout is redirected to a file the
/// way <c>RealConstants.Experiments</c>' <c>compare</c> is. Redirection is the caller's job:
/// nothing here opens a file, which keeps the runner's only output channels the two every other
/// command uses.
/// </para>
/// <para>
/// <b>Nothing here is interactive, and nothing here may become interactive.</b> There is no
/// <c>Console.ReadKey</c> and no <c>Console.IsInputRedirected</c>. An agent session cannot
/// exercise a path gated on a terminal - <c>../CLAUDE.md</c> § Shell records that both defects
/// which ever reached a push in this project lived in that gap - so a prompt added here would be
/// unverifiable by the party most likely to add it.
/// </para>
/// <para>
/// Data goes to stdout and labels, legends and caveats go to stderr, so a table can be
/// redirected without the prose around it. Nothing here has a pass or a fail: the exit code says
/// only whether the named experiment was found, was given arguments it accepted, and ran to
/// completion. <c>../AGENTS.md</c> § Exactness discipline is explicit that a long run printing a
/// table must not masquerade as a test.
/// </para>
/// </remarks>
internal static class Program
{
    /// <summary>The zeta(2) walk's command name, spelled once.</summary>
    public const string WalkCommand = "walk";

    /// <summary>The pi^3/zeta(3) run's command name, spelled once.</summary>
    public const string TargetCommand = "target";

    /// <summary>The survivor report's command name, spelled once.</summary>
    public const string SurvivorsCommand = "survivors";

    /// <summary>The same survivor report walked once rather than per prefix, spelled once.</summary>
    /// <remarks>
    /// A verb of its own rather than a flag on <see cref="SurvivorsCommand"/>, because this runner
    /// has one grammar - verbs, positional arguments, no flags - and a flag or a leading keyword
    /// would be a second one. The two share their argument grammar exactly, and
    /// <see cref="SurvivorRun.Interpret"/> reads both.
    /// </remarks>
    public const string DeepCommand = "deep";

    private static int Main(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);

        if (args.Length == 0 || args[0] == "list" || args[0] == "--help")
        {
            Usage();
            return args.Length > 0 ? 0 : 2;
        }

        string[] rest = args[1..];

        if (string.Equals(args[0], WalkCommand, StringComparison.OrdinalIgnoreCase))
        {
            if (rest.Length > 0)
            {
                Console.Error.WriteLine("walk takes no arguments - its schedule is fixed, being a control.");
                return 2;
            }

            return RatioWalk.Walk();
        }

        if (string.Equals(args[0], TargetCommand, StringComparison.OrdinalIgnoreCase))
        {
            return TargetRun.Run(rest);
        }

        if (string.Equals(args[0], SurvivorsCommand, StringComparison.OrdinalIgnoreCase))
        {
            return SurvivorRun.Run(rest, SurvivorMode.Chart);
        }

        if (string.Equals(args[0], DeepCommand, StringComparison.OrdinalIgnoreCase))
        {
            return SurvivorRun.Run(rest, SurvivorMode.Deep);
        }

        Console.Error.WriteLine(string.Create(CultureInfo.InvariantCulture,
            $"no experiment named '{args[0]}' - try 'list'."));
        return 2;
    }

    private static void Usage()
    {
        TextWriter notes = Console.Error;

        notes.WriteLine("Zeta experiments - the composition of SPEC-rational-ratio.md section 2, run");
        notes.WriteLine("against a target whose answer is known and one whose answer is not.");
        notes.WriteLine("Data goes to stdout; labels and caveats go to stderr. No pass, no fail.");
        notes.WriteLine();
        notes.WriteLine("usage: dotnet run --project Zeta.Experiments -- <name> [arguments]");
        notes.WriteLine();
        notes.WriteLine(string.Create(CultureInfo.InvariantCulture,
            $"  {WalkCommand,-17}  pi^2 / zeta(2), whose answer is 6, one provider step at a"));
        notes.WriteLine("                    time. pi, pi^2 and zeta(2) each with their own bound,");
        notes.WriteLine("                    the composed ratio, the blame split, and a ladder of");
        notes.WriteLine("                    rival rationals being refuted beside it.");
        notes.WriteLine(string.Create(CultureInfo.InvariantCulture,
            $"  {TargetCommand + " [exponent]",-17}  pi^3 / zeta(3), whose answer nobody knows. Reports a"));
        notes.WriteLine(string.Create(CultureInfo.InvariantCulture,
            $"                    denominator bound. The schedule runs 1e-{TargetRun.FirstExponent} to"));
        notes.WriteLine(string.Create(CultureInfo.InvariantCulture,
            $"                    1e-<exponent>, default {TargetRun.DefaultLastExponent}, ceiling {TargetRun.MaxLastExponent}."));
        notes.WriteLine(string.Create(CultureInfo.InvariantCulture,
            $"  {SurvivorsCommand + " [order]",-17}  pi^n / zeta(n) reported the way section 2 step 6 says a"));
        notes.WriteLine("                    run reports: the survivor set under a denominator bound");
        notes.WriteLine(string.Create(CultureInfo.InvariantCulture,
            $"                    fixed in advance. Order defaults to {SurvivorRun.DefaultOrder} and has no"));
        notes.WriteLine("                    ceiling - every even order has a known answer, so a run");
        notes.WriteLine("                    is refused for its cost or for a bound it cannot reach.");
        notes.WriteLine("                    Two charts go to stdout as ONE SVG - redirect it.");
        notes.WriteLine(string.Create(CultureInfo.InvariantCulture,
            $"  {SurvivorsCommand + " [o f l]",-17}  the same, with both ends of the schedule named:"));
        notes.WriteLine("                    'survivors 3 2 12' runs 1e-2 .. 1e-12. It defaults to");
        notes.WriteLine(string.Create(CultureInfo.InvariantCulture,
            $"                    1e-{SurvivorRun.DefaultFirstExponent} .. 1e-{SurvivorRun.DefaultLastExponent}, " +
            $"and takes both ends or neither, since"));
        notes.WriteLine("                    one exponent alone could name either.");
        notes.WriteLine(string.Create(CultureInfo.InvariantCulture,
            $"  {DeepCommand + " [o f l]",-17}  the same survivor set as survivors, without the charts,"));
        notes.WriteLine("                    traded for reach. One walk over every enclosure where");
        notes.WriteLine("                    survivors walks once per prefix, so the collapse and the");
        notes.WriteLine("                    nearest excluded are not drawn - and the output says so.");
        notes.WriteLine("                    Same arguments, same defaults.");
        notes.WriteLine(string.Create(CultureInfo.InvariantCulture,
            $"  {"... <walk>",-17}  either command takes a survivor walk LAST: '" +
            $"{SurvivorWalkChoice.Farey.Argument}' or '{SurvivorWalkChoice.Denominator.Argument}', as in"));
        notes.WriteLine(string.Create(CultureInfo.InvariantCulture,
            $"                    '{SurvivorsCommand} 3 2 12 {SurvivorWalkChoice.Denominator.Argument}'. " +
            $"{SurvivorWalkChoice.Default.Name} is the default."));
        notes.WriteLine(string.Create(CultureInfo.InvariantCulture,
            $"  {"... farey <n>",-17}  {SurvivorWalkChoice.Farey.Name} with a survivor limit of n, a whole number;"));
        notes.WriteLine(string.Create(CultureInfo.InvariantCulture,
            $"                    the default is {SurvivorCountGuard.DefaultLimit:N0}. " +
            $"{SurvivorWalkChoice.Denominator.Name} takes none."));
        notes.WriteLine();
        notes.WriteLine("what they are for");
        notes.WriteLine();
        notes.WriteLine("  walk is the control for what target prints. Its answer is known, so a");
        notes.WriteLine("  reader can check every column of it; target's answer is not, so nothing");
        notes.WriteLine("  about that output can be checked by reading it. That is the same argument");
        notes.WriteLine("  section 1 makes for the even orders at large.");
        notes.WriteLine();
        notes.WriteLine("  walk does NOT exercise the search - 6/1 is enclosed from the first column,");
        notes.WriteLine("  so the search ends at it immediately and refutes nothing. What it exercises");
        notes.WriteLine("  is composition and halting. The rival ladder supplies the other half.");
        notes.WriteLine();
        notes.WriteLine("  target is expensive at the bottom of its schedule and its ceiling is a");
        notes.WriteLine("  measured number, not a computed bound. Ask for one past the ceiling and it");
        notes.WriteLine("  says what the run would have cost instead of starting it.");
        notes.WriteLine();
        notes.WriteLine("  survivors is the one that reports a RESULT rather than a trend. walk and");
        notes.WriteLine("  target both print a matrix that section 2 now keeps as presentation; this");
        notes.WriteLine("  prints what decides - every rational under a bound that no enclosure");
        notes.WriteLine("  excludes. An even order is a control whose answer section 1 lists; an odd");
        notes.WriteLine("  one is the question. Its bound is derived, never picked.");
        notes.WriteLine();
        notes.WriteLine("  survivors has no depth ceiling, unlike target, because what a schedule");
        notes.WriteLine("  costs is priced off the enclosures a run REALISES and an argument cannot");
        notes.WriteLine("  see those. How it is priced is the walk's, and the output names both.");
        notes.WriteLine();
        notes.WriteLine(string.Create(CultureInfo.InvariantCulture,
            $"  {SurvivorWalkChoice.Farey.Name}, the default, costs about log Q plus one step a survivor, so it"));
        notes.WriteLine("  is guarded by a COUNT: the survivors expected over the prefixes it walks,");
        notes.WriteLine("  6*h*Q^2/pi^2 each, are checked against a limit before the walk, and the");
        notes.WriteLine("  survivors found are checked against it as the walk runs - an expectation");
        notes.WriteLine(string.Create(CultureInfo.InvariantCulture,
            $"  is not a bound. The limit is {SurvivorCountGuard.DefaultLimit:N0} unless you give another."));
        notes.WriteLine("  No calibration, and nothing caps Q: deep walks to the derived bound.");
        notes.WriteLine();
        notes.WriteLine(string.Create(CultureInfo.InvariantCulture,
            $"  {SurvivorWalkChoice.Denominator.Name} is the reference, linear in Q, and is guarded by TIME. It"));
        notes.WriteLine(string.Create(CultureInfo.InvariantCulture,
            $"  times a short sample and refuses a chart predicted past {SurvivorRun.BudgetSeconds} seconds -"));
        notes.WriteLine("  seconds rather than candidates because a candidate costs five times more");
        notes.WriteLine("  at order 10 than at order 3. Not a timed abort: the search then runs to");
        notes.WriteLine("  completion, so a slow machine refuses more and reports the same answer.");
        notes.WriteLine("  Each collapse point walks about h*Q^2 + Q for a prefix half-width h.");
        notes.WriteLine();
        notes.WriteLine("  Under either walk the widest prefix dominates a chart, so a decade off the");
        notes.WriteLine("  last end costs ten times as much, while a decade off the first end saves");
        notes.WriteLine("  less and by no fixed factor - about eight to the decade here, and lumpy.");
        notes.WriteLine("  RatioEnclosure.Of coarsens, so every realised half-width is a power of two");
        notes.WriteLine("  by construction, and which one a schedule lands on is set by the first");
        notes.WriteLine("  provider step to meet the target. Deeper runs are bought by starting later,");
        notes.WriteLine("  at the cost of a shorter chart.");
        notes.WriteLine();
        notes.WriteLine("  deep takes that trade to its end. Its survivor set is identical, because");
        notes.WriteLine("  every walk returns what all the enclosures it is given contain, so one walk");
        notes.WriteLine("  over all of them is the chart's last prefix. What it gives up is every");
        notes.WriteLine("  picture that needs a shorter prefix. The first exponent does not move its");
        notes.WriteLine("  cost. Under the reference walk it is priced by timing a sample of that walk");
        notes.WriteLine("  and never refused for that cost: where the derived Q would pass the budget,");
        notes.WriteLine("  it walks to the largest Q the budget affords and prints both.");
        notes.WriteLine();
        notes.WriteLine("worth running");
        notes.WriteLine();
        notes.WriteLine(string.Create(CultureInfo.InvariantCulture,
            $"  {WalkCommand,-16}  the halting rule watchable - who owns the error, who"));
        notes.WriteLine("                    gets advanced, and two columns that do nothing because");
        notes.WriteLine("                    the realised bound had already passed the next target");
        notes.WriteLine(string.Create(CultureInfo.InvariantCulture,
            $"  {TargetCommand + " 8",-16}  the same shape in about a tenth of a second, for a look"));
        notes.WriteLine("                    at the report before paying for the real one");
        notes.WriteLine(string.Create(CultureInfo.InvariantCulture,
            $"  {TargetCommand,-16}  the run itself, about 35 s, ending some four million"));
        notes.WriteLine("                    denominators deep");
        notes.WriteLine(string.Create(CultureInfo.InvariantCulture,
            $"  {SurvivorsCommand,-16}  the collapse of a survivor set to the answer, on a"));
        notes.WriteLine("                    target whose answer is known - redirect it and open");
        notes.WriteLine("                    the SVG");
        notes.WriteLine(string.Create(CultureInfo.InvariantCulture,
            $"  {SurvivorsCommand + " 3",-16}  the same against pi^3/zeta(3), where nothing is known:"));
        notes.WriteLine("                    Q = 11,585, about 400,000 survivors walked in all");
        notes.WriteLine(string.Create(CultureInfo.InvariantCulture,
            $"  {SurvivorsCommand + " 3 4 11",-16}  what starting later buys: Q = 741,455, some 64 times"));
        notes.WriteLine("                    the default's bound, on a collapse chart of eight");
        notes.WriteLine("                    enclosures - about 13 million survivors walked");
        notes.WriteLine(string.Create(CultureInfo.InvariantCulture,
            $"  {DeepCommand + " 5 2 15",-16}  pi^5/zeta(5) to Q = 134,217,728, the derived bound,"));
        notes.WriteLine("                    uncapped - the run the reference walk's budget capped");
        notes.WriteLine("                    at Q = 16,868,855");
        notes.WriteLine(string.Create(CultureInfo.InvariantCulture,
            $"  {SurvivorsCommand} 3 2 9 {SurvivorWalkChoice.Denominator.Argument}"));
        notes.WriteLine("                    the reference walk, one decade deeper from the default");
        notes.WriteLine("                    first end: about 16 s and Q = 32,768");
        notes.WriteLine(string.Create(CultureInfo.InvariantCulture,
            $"  {DeepCommand} 3 5 14 {SurvivorWalkChoice.Denominator.Argument}"));
        notes.WriteLine("                    Q = 23,726,566 in about four minutes - and one decade");
        notes.WriteLine("                    further, the budget caps Q rather than refusing the run");
        notes.WriteLine();
    }
}
