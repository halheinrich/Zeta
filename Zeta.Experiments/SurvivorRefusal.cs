using System.Globalization;
using System.Numerics;

namespace HalHeinrich.Numerics.Experiments;

/// <summary>One end of a <c>survivors</c> schedule, named as the caller types it.</summary>
/// <remarks>
/// <b>The two ends are different knobs and a refusal that treats them as one sends a caller the
/// wrong way</b> - ruled on <c>halheinrich/Math#64</c> 2026-09-09, off a live instance. The last
/// exponent sets <c>Q</c> and so the bound the run exists to claim; the first sets the widest
/// enclosure and so the cost, and moving it leaves <c>Q</c> alone. A message saying only "shorten
/// the schedule" invites a reader to shorten the end that guts the result, two lines above the
/// same message calling that end derived on purpose.
/// </remarks>
internal enum ScheduleEnd
{
    /// <summary>The first exponent: the cost knob. Raising it leaves <c>Q</c> where it is.</summary>
    First,

    /// <summary>The last exponent: the claim knob. Lowering it lowers <c>Q</c>.</summary>
    Last,
}

/// <summary>
/// A priced-out run, and which end of its schedule to move.
/// </summary>
/// <param name="Size">How much walking the run would be, counted as its two loops.</param>
/// <param name="Price">What each of those loops costs here.</param>
/// <param name="DenominatorBound">The bound the run derived, which the message quotes.</param>
/// <param name="Prefixes">How many collapse points the walk would have produced.</param>
/// <param name="CostKnob">The end to move to bring the run inside the budget.</param>
/// <param name="ClaimKnob">The end that sets <c>Q</c>, and so what the run would have claimed.</param>
/// <remarks>
/// <para>
/// <b>A value rather than a string, so which end the advice names is something a test can read.</b>
/// The defect this replaces was in the prose - "one more decade of schedule is ten times this
/// figure", true of the last end, false of the first, and silent about which - and a defect in
/// prose is caught by asserting on prose, which is the weakest assertion available. The two knobs
/// are fields here and <see cref="Message"/> renders them, so an implementation that swapped them
/// would say the wrong thing and a test would see it.
/// </para>
/// <para>
/// Both are constant in this command, and that is not an argument against holding them: the
/// constant is the claim. It is <i>which</i> end does what, which is precisely what the shipped
/// message left a reader to guess.
/// </para>
/// </remarks>
internal readonly record struct SurvivorRefusal(
    WalkSize Size,
    WalkPrice Price,
    BigInteger DenominatorBound,
    int Prefixes,
    ScheduleEnd CostKnob,
    ScheduleEnd ClaimKnob)
{
    /// <summary>Gets how long the walk was predicted to take.</summary>
    public BigRational PredictedSeconds => Price.Seconds(Size);

    /// <summary>Gets the refusal as the command prints it.</summary>
    public string Message => string.Create(CultureInfo.InvariantCulture,
        $"Refusing this run: it is predicted to spend {Presentation.Roughly(PredictedSeconds)} " +
        $"seconds enumerating, past the budget of {SurvivorRun.BudgetSeconds}.\n" +
        $"  the bound   Q = {DenominatorBound}, derived as floor(eps^(-1/2)) from the final enclosure\n" +
        $"  the walk    {Size.Denominators:N0} denominators and {Size.Candidates:N0} candidates: each\n" +
        $"              of the {Prefixes} collapse points steps through 1..Q afresh, counting the\n" +
        $"              rationals its own prefix admits - about h*Q^2 + Q apiece for a prefix of\n" +
        $"              half-width h\n" +
        $"  the price   {Presentation.Roughly(Price.PerDenominator * SurvivorRun.Million)} microseconds " +
        $"a denominator, {Presentation.Roughly(Price.PerCandidate * SurvivorRun.Million)} a candidate,\n" +
        $"              timed here against these enclosures before the search. It rises with the\n" +
        $"              ORDER, so a budget counting candidates would not mean the same thing here\n" +
        $"              as at order 3\n" +
        $"\n" +
        $"The two ends of the schedule are different knobs. Raise the {Name(CostKnob).ToUpperInvariant()} exponent:\n" +
        $"  it leaves Q exactly where it is and drops the cost by whatever power-of-two step the\n" +
        $"  widest enclosure moves - one decade cut a refused order-10 run from 68,157,440 to\n" +
        $"  1,572,864, a 43x fall, because the realised widest moved seven bits. What it costs is\n" +
        $"  collapse points, and the survivor set is unchanged.\n" +
        $"Lowering the {Name(ClaimKnob).ToUpperInvariant()} exponent also works and is usually not what you want:\n" +
        $"  it divides Q by about sqrt(10) a decade, and Q is the bound this run exists to claim.\n" +
        $"Do not reach for a hand-picked Q instead. That is what made the exploration's second\n" +
        $"graph misleading, and Q is derived here on purpose.");

    /// <summary>The word a caller would use for one end of the schedule.</summary>
    private static string Name(ScheduleEnd end) => end == ScheduleEnd.First ? "first" : "last";
}
