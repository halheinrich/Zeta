namespace HalHeinrich.Numerics.Experiments;

/// <summary>What a survivor walk ended with: a report, or a refusal because it passed its survivor limit.</summary>
/// <remarks>
/// <para>
/// <b>A value rather than an exception, and the reason is what a refusal is.</b> Passing the limit
/// is an expected outcome of the count policy ruled on <c>halheinrich/Math#79</c> leg 2, with the
/// same shape as every refusal this runner already returns as a value and exits 2 on - the
/// schedule, the order, the cost, the unreachable control. Exceptions stay for defects, like
/// <see cref="FareyWalk"/>'s <see cref="System.Diagnostics.UnreachableException"/> when its own
/// invariants break. (That the analyzers would also reject an internal exception type here,
/// <c>CA1064</c>, is a footnote and not the reason.)
/// </para>
/// <para>
/// <b><see cref="Report"/> throws on a refused run</b>, so a caller cannot read a report off an
/// outcome without either having checked <see cref="Refusal"/> or failing loudly. A refused walk
/// stopped part-way, and nothing it had counted is a report of anything.
/// </para>
/// <para>
/// A sealed class with a private constructor, because exactly one of the two is present and no
/// default could say which.
/// </para>
/// </remarks>
internal sealed class SurvivorOutcome
{
    private const string RefusedMessage =
        "This walk was refused for passing its survivor limit, so there is no report - it stopped " +
        "part-way, and nothing it counted describes the survivor set. Check Refusal first.";

    private readonly SurvivorReport? report;

    private SurvivorOutcome(SurvivorReport? report, SurvivorCountRefusal? refusal)
    {
        this.report = report;
        Refusal = refusal;
    }

    /// <summary>Gets the refusal, or null when the walk completed.</summary>
    public SurvivorCountRefusal? Refusal { get; }

    /// <summary>Gets the report of a walk that completed.</summary>
    /// <exception cref="InvalidOperationException">The walk was refused; see <see cref="Refusal"/>.</exception>
    public SurvivorReport Report => report ?? throw new InvalidOperationException(RefusedMessage);

    /// <summary>A walk that ran to completion.</summary>
    /// <param name="report">Its report.</param>
    /// <returns>The outcome.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="report"/> is null.</exception>
    public static SurvivorOutcome Completed(SurvivorReport report)
    {
        ArgumentNullException.ThrowIfNull(report);

        return new SurvivorOutcome(report, null);
    }

    /// <summary>A walk refused for its survivor count.</summary>
    /// <param name="refusal">Why.</param>
    /// <returns>The outcome.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="refusal"/> is null.</exception>
    public static SurvivorOutcome Refused(SurvivorCountRefusal refusal)
    {
        ArgumentNullException.ThrowIfNull(refusal);

        return new SurvivorOutcome(null, refusal);
    }
}
