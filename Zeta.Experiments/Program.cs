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
            return SurvivorRun.Run(rest);
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
        notes.WriteLine("usage: dotnet run --project Zeta.Experiments -- <name> [argument]");
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
            $"                    fixed in advance. Order defaults to {SurvivorRun.DefaultOrder}, ceiling {SurvivorRun.MaxOrder}."));
        notes.WriteLine("                    Two charts go to stdout as ONE SVG - redirect it.");
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
        notes.WriteLine("worth running");
        notes.WriteLine();
        notes.WriteLine(string.Create(CultureInfo.InvariantCulture,
            $"  {WalkCommand,-11}     the halting rule watchable - who owns the error, who gets"));
        notes.WriteLine("                  advanced, and two columns that do nothing because the");
        notes.WriteLine("                  realised bound had already passed the next target");
        notes.WriteLine(string.Create(CultureInfo.InvariantCulture,
            $"  {TargetCommand + " 8",-11}     the same shape in about a tenth of a second, for a look at"));
        notes.WriteLine("                  the report before paying for the real one");
        notes.WriteLine(string.Create(CultureInfo.InvariantCulture,
            $"  {TargetCommand,-11}     the run itself, about 35 s, ending some four million"));
        notes.WriteLine("                  denominators deep");
        notes.WriteLine(string.Create(CultureInfo.InvariantCulture,
            $"  {SurvivorsCommand,-11}     the collapse of a survivor set to the answer, on a target"));
        notes.WriteLine("                  whose answer is known - redirect it and open the SVG");
        notes.WriteLine(string.Create(CultureInfo.InvariantCulture,
            $"  {SurvivorsCommand + " 3",-11}     the same against pi^3/zeta(3), where nothing is known"));
        notes.WriteLine();
    }
}
