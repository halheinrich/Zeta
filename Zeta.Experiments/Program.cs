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
        notes.WriteLine("  see those. It runs, times a short sample against those enclosures, and");
        notes.WriteLine(string.Create(CultureInfo.InvariantCulture,
            $"  refuses the enumeration if that predicts past {SurvivorRun.BudgetSeconds} seconds. A budget in"));
        notes.WriteLine("  seconds rather than candidates because a candidate costs five times more");
        notes.WriteLine("  at order 10 than at order 3. Not a timed abort: the search then runs to");
        notes.WriteLine("  completion, so a slow machine refuses more and reports the same answer.");
        notes.WriteLine("  Each collapse point walks about h*Q^2 + Q for a prefix half-width h, so a");
        notes.WriteLine("  decade off the last end costs ten times as much, while a decade off the");
        notes.WriteLine("  first end saves less and by no fixed factor - about eight to the decade");
        notes.WriteLine("  here, and lumpy. RatioEnclosure.Of coarsens, so every realised half-width");
        notes.WriteLine("  is a power of two by construction, and which one a schedule lands on is");
        notes.WriteLine("  set by the first provider step to meet the target. Deeper runs are bought");
        notes.WriteLine("  by starting later, at the cost of a shorter chart.");
        notes.WriteLine();
        notes.WriteLine("  deep takes that trade to its end. Its survivor set is identical, because");
        notes.WriteLine("  SurvivorSearch intersects every enclosure it is given and seeds from the");
        notes.WriteLine("  narrowest, so one walk over all of them is the chart's last prefix. What it");
        notes.WriteLine("  gives up is every picture that needs a shorter prefix. Its walk is about Q");
        notes.WriteLine("  denominators and almost nothing else, so the first exponent does not move");
        notes.WriteLine("  its cost at all; it is priced by timing a sample of that walk itself.");
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
            $"  {SurvivorsCommand + " 3",-16}  the same against pi^3/zeta(3), where nothing is known -"));
        notes.WriteLine("                    a few seconds, ending at Q = 11,585");
        notes.WriteLine(string.Create(CultureInfo.InvariantCulture,
            $"  {SurvivorsCommand + " 3 2 9",-16}  one decade deeper from the default first end: about"));
        notes.WriteLine("                    16 s and Q = 32,768");
        notes.WriteLine(string.Create(CultureInfo.InvariantCulture,
            $"  {SurvivorsCommand + " 3 4 11",-16}  what starting later buys: Q = 741,455, some 64 times"));
        notes.WriteLine("                    the default's bound, in about two minutes and on a");
        notes.WriteLine("                    collapse chart of eight enclosures rather than seven");
        notes.WriteLine();
    }
}
