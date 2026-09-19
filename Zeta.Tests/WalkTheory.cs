using HalHeinrich.Numerics.Experiments;

namespace HalHeinrich.Numerics.Tests;

/// <summary>Every survivor walk the runner offers, as theory data, for the tests that walk.</summary>
/// <remarks>
/// <para>
/// <b><c>Zeta</c>'s survivor controls run against both walks</b> - ruled on
/// <c>halheinrich/Math#79</c> leg 2. The data is <see cref="SurvivorWalkChoice.All"/> and not a
/// list written here, so a test over it covers every walk the runner can be asked for; that
/// closed set is one of the reasons <see cref="SurvivorWalkChoice"/> is a closed type.
/// </para>
/// <para>
/// A walk travels as the word that names it rather than as a delegate, so each case reads
/// <c>(walk: "farey")</c> in a test listing and is enumerated at discovery like any other data.
/// </para>
/// </remarks>
internal static class WalkTheory
{
    /// <summary>Gets the word naming each walk, one theory case each.</summary>
    public static TheoryData<string> Names => new(SurvivorWalkChoice.All.Select(choice => choice.Argument));

    /// <summary>The walk a theory case names.</summary>
    /// <param name="walk">A word from <see cref="Names"/>.</param>
    /// <returns>That walk, as the report methods take it.</returns>
    public static SurvivorWalk Named(string walk) =>
        SurvivorWalkChoice.Named(walk)?.Walk ?? throw new ArgumentException("Not a walk this runner offers.", nameof(walk));
}
