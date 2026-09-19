using System.Globalization;
using System.Numerics;
using System.Xml.Linq;
using HalHeinrich.Numerics.Experiments;

namespace HalHeinrich.Numerics.Tests;

/// <summary>
/// The walk a survivor run is asked for, and the guard that comes with it: the word on the command
/// line, the pairing in <see cref="SurvivorWalkChoice"/>, the survivor count that guards
/// <see cref="FareyWalk"/>, and the labels that name the walk that ran.
/// </summary>
/// <remarks>
/// <para>
/// <b>Every test here is pure and holds no timing</b>, as <c>halheinrich/Math#79</c>'s leg 2
/// ruling asks of the guard's tests. The count guard is a function of enclosures, a bound and a
/// limit; its refusal is a value; the text is rendered from that value. The one admission that
/// takes a timing - the reference walk's calibration - is not exercised here, and its pieces keep
/// the pure tests <c>SurvivorReportTests</c> already holds them to.
/// </para>
/// </remarks>
public sealed class SurvivorWalkChoiceTests
{
    private static BigRational Ratio(BigInteger numerator, BigInteger denominator) => new(numerator, denominator);

    private static Approximation At(BigRational value, BigInteger errorNumerator, BigInteger errorDenominator) =>
        Approximation.Create(value, new BigRational(errorNumerator, errorDenominator));

    private static SurvivorRequest Request(SurvivorMode mode, SurvivorWalkChoice walk) => new(3, 2, 12, mode, walk, walk.DefaultLimit);

    // ---------- the choices ----------

    [Fact]
    public void All_IsBothWalksWithFareyTheDefault()
    {
        // Literals rather than the members that define them: moving the default is meant to redden
        // something. The user's ruling on halheinrich/Math#79 leg 2 put FareyWalk first.
        Assert.Equal(2, SurvivorWalkChoice.All.Count);
        Assert.Same(SurvivorWalkChoice.Farey, SurvivorWalkChoice.Default);
        Assert.True(SurvivorWalkChoice.Farey.IsDefault);
        Assert.False(SurvivorWalkChoice.Denominator.IsDefault);
        Assert.Equal([SurvivorWalkChoice.Farey, SurvivorWalkChoice.Denominator], SurvivorWalkChoice.All);
    }

    [Fact]
    public void Name_IsTheWalksOwnTypeName()
    {
        // Read off the walk rather than typed, so a label cannot name a walk that did not run.
        Assert.Equal(nameof(FareyWalk), SurvivorWalkChoice.Farey.Name);
        Assert.Equal(nameof(DenominatorWalk), SurvivorWalkChoice.Denominator.Name);
    }

    [Theory]
    [InlineData("farey")]
    [InlineData("denominator")]
    [InlineData("FAREY")]
    [InlineData("Denominator")]
    public void Named_FindsEachWalkByItsWordInAnyCase(string word)
    {
        SurvivorWalkChoice? found = SurvivorWalkChoice.Named(word);

        Assert.NotNull(found);
        Assert.Equal(word, found.Argument, ignoreCase: true);
    }

    [Theory]
    [InlineData("fary")]
    [InlineData("FareyWalk")]
    [InlineData("3")]
    [InlineData("")]
    public void Named_FindsNothingForAWordNamingNoWalk(string word) =>
        Assert.Null(SurvivorWalkChoice.Named(word));

    [Fact]
    public void Walk_IsTheNamedWalksOwnSurvivors()
    {
        // Behaviour, not identity: each choice's walk yields exactly what a fresh instance of its
        // own type yields, in that type's own order - which is where the two differ.
        Approximation[] enclosures = [At(BigRational.FromInteger(6), 1, 2)];

        Assert.Equal(new FareyWalk().Survivors(enclosures, 12), SurvivorWalkChoice.Farey.Walk(enclosures, 12));
        Assert.Equal(new DenominatorWalk().Survivors(enclosures, 12), SurvivorWalkChoice.Denominator.Walk(enclosures, 12));
        Assert.NotEqual(SurvivorWalkChoice.Farey.Walk(enclosures, 12), SurvivorWalkChoice.Denominator.Walk(enclosures, 12));
    }

    // ---------- the walk word ----------

    [Fact]
    public void Interpret_DefaultsToFareyWalkInBothCommands()
    {
        foreach (SurvivorMode mode in new[] { SurvivorMode.Chart, SurvivorMode.Deep })
        {
            Assert.Null(SurvivorRun.Interpret(["3", "2", "12"], mode, out SurvivorRequest request));

            Assert.Same(SurvivorWalkChoice.Farey, request.Walk);
        }
    }

    [Theory]
    [InlineData("denominator", 2, 2, 8)]
    [InlineData("3 denominator", 3, 2, 8)]
    [InlineData("3 2 12 denominator", 3, 2, 12)]
    [InlineData("5 4 11 DENOMINATOR", 5, 4, 11)]
    public void Interpret_TakesTheWalkLastAfterAnyOfTheScheduleForms(string typed, int order, int first, int last)
    {
        string[] arguments = typed.Split(' ');

        foreach (SurvivorMode mode in new[] { SurvivorMode.Chart, SurvivorMode.Deep })
        {
            Assert.Null(SurvivorRun.Interpret(arguments, mode, out SurvivorRequest request));

            Assert.Equal(new SurvivorRequest(order, first, last, mode, SurvivorWalkChoice.Denominator, SurvivorWalkChoice.Denominator.DefaultLimit), request);
        }
    }

    [Fact]
    public void Interpret_TakesTheDefaultWalkNamedExplicitly()
    {
        Assert.Null(SurvivorRun.Interpret(["3", "farey"], SurvivorMode.Deep, out SurvivorRequest request));

        Assert.Equal(new SurvivorRequest(3, 2, 8, SurvivorMode.Deep, SurvivorWalkChoice.Farey, SurvivorWalkChoice.Farey.DefaultLimit), request);
    }

    [Theory]
    [InlineData("3 farey 2 12")]
    [InlineData("farey 3 2")]
    public void Interpret_RefusesAWalkAnywhereButLastAndSaysWhereItGoes(string typed)
    {
        string? refusal = SurvivorRun.Interpret(typed.Split(' '), SurvivorMode.Chart, out SurvivorRequest request);

        Assert.NotNull(refusal);
        Assert.Contains("names a walk, and the walk goes last", refusal, StringComparison.Ordinal);
        Assert.Equal(default, request);
    }

    [Theory]
    [InlineData("farey denominator")]
    [InlineData("3 denominator 2 12 farey")]
    public void Interpret_RefusesTwoWalks(string typed)
    {
        string? refusal = SurvivorRun.Interpret(typed.Split(' '), SurvivorMode.Chart, out SurvivorRequest request);

        Assert.NotNull(refusal);
        Assert.Contains("each name a walk, and a run takes one", refusal, StringComparison.Ordinal);
        Assert.Equal(default, request);
    }

    [Fact]
    public void Interpret_StillRefusesOneExponentWhenAWalkFollowsIt()
    {
        // Peeling the walk leaves the schedule's own rules to judge what is left, unchanged.
        string? refusal = SurvivorRun.Interpret(["3", "12", "denominator"], SurvivorMode.Chart, out _);

        Assert.NotNull(refusal);
        Assert.Contains("both ends", refusal, StringComparison.Ordinal);
        Assert.Contains("'farey' (the default) or 'denominator'", refusal, StringComparison.Ordinal);
    }

    [Fact]
    public void Interpret_TreatsAMisspeltWalkAsTheNumberItIsNot()
    {
        // Only a word that names a walk is taken as one. Anything else is judged by the schedule's
        // rules, and a lone word in the order's place is refused as not being an order.
        string? refusal = SurvivorRun.Interpret(["fary"], SurvivorMode.Chart, out _);

        Assert.NotNull(refusal);
        Assert.Contains("'fary' is not an order", refusal, StringComparison.Ordinal);
    }

    // ---------- the invocation a refusal offers ----------

    [Fact]
    public void Invocation_CarriesTheWalkOnlyWhenItIsNotTheDefault()
    {
        Assert.Equal("survivors 3 2 12", Request(SurvivorMode.Chart, SurvivorWalkChoice.Farey).Invocation(2, 12));
        Assert.Equal("deep 3 5 14 denominator", Request(SurvivorMode.Deep, SurvivorWalkChoice.Denominator).Invocation(5, 14));
    }

    [Fact]
    public void RefuseUnreachableControl_OffersAnInvocationThatRepeatsTheSameWalk()
    {
        // Pasted, an invocation without the word would run the other walk under the other guard.
        var asked = new SurvivorRequest(18, 2, 8, SurvivorMode.Deep, SurvivorWalkChoice.Denominator, SurvivorWalkChoice.Denominator.DefaultLimit);

        string? refusal = SurvivorRun.RefuseUnreachableControl(asked, 16_384);

        Assert.NotNull(refusal);
        Assert.Contains(
            Inv($"'deep 18 2 {SurvivorRun.ExponentReaching(18)} denominator'"), refusal, StringComparison.Ordinal);
    }

    [Fact]
    public void RefuseUnaffordableControl_OffersTheWalkNoBudgetCaps()
    {
        var asked = new SurvivorRequest(18, 5, 11, SurvivorMode.Deep, SurvivorWalkChoice.Denominator, SurvivorWalkChoice.Denominator.DefaultLimit);

        string? refusal = SurvivorRun.RefuseUnaffordableControl(asked, new SurvivorBound(100_000, 43_866));

        Assert.NotNull(refusal);
        Assert.Contains("Or walk with FareyWalk, which no budget caps: 'deep 18 5 11'.", refusal, StringComparison.Ordinal);
    }

    // ---------- the expected count ----------

    [Fact]
    public void Expected_SumsEveryPrefixForAChartAndTakesTheNarrowestForADeepWalk()
    {
        Approximation wide = At(BigRational.FromInteger(6), 1, 2);
        Approximation narrow = At(BigRational.FromInteger(6), 1, 4);
        BigRational atWide = SurvivorReport.ExpectedSurvivors(wide.MaxError, 10).Upper;
        BigRational atNarrow = SurvivorReport.ExpectedSurvivors(narrow.MaxError, 10).Upper;

        Assert.Equal(atWide + atNarrow, SurvivorCountGuard.Expected([wide, narrow], 10, SurvivorMode.Chart));
        Assert.Equal(atNarrow, SurvivorCountGuard.Expected([wide, narrow], 10, SurvivorMode.Deep));
    }

    [Fact]
    public void Expected_SizesEachPrefixAtItsNarrowestEnclosureWhateverTheOrder()
    {
        // A list that widens again: the second prefix still holds the narrow enclosure, and its
        // survivors lie inside it, so it is sized there. Sizing at the latest would understate.
        Approximation wide = At(BigRational.FromInteger(6), 1, 2);
        Approximation narrow = At(BigRational.FromInteger(6), 1, 4);
        BigRational atNarrow = SurvivorReport.ExpectedSurvivors(narrow.MaxError, 10).Upper;

        Assert.Equal(atNarrow + atNarrow, SurvivorCountGuard.Expected([narrow, wide], 10, SurvivorMode.Chart));
    }

    [Fact]
    public void Expected_AtADerivedBoundADeepWalkExpectsUnderOne()
    {
        // The identity SurvivorReport.ExpectedUnderBound states: Q = floor(eps^(-1/2)) cancels the
        // eps, leaving 6/pi^2 or just under it at every precision. So a deep run at a derived bound
        // cannot pass any limit before the walk, and the during-walk check is its guard.
        foreach (int exponent in new[] { 8, 15, 30, 60 })
        {
            Approximation final = Approximation.Create(Ratio(314159, 100000), TargetSchedule.Decade(exponent));
            BigInteger derived = SurvivorReport.DerivedBound(final);

            BigRational expected = SurvivorCountGuard.Expected([final], derived, SurvivorMode.Deep);

            Assert.True(expected < BigRational.One, Inv($"1e-{exponent}: expected {expected}."));
            Assert.True(expected > Ratio(6, 10), Inv($"1e-{exponent}: expected {expected}."));
        }
    }

    // ---------- the refusal before the walk ----------

    [Fact]
    public void Refuse_TurnsOnWhetherTheExpectationPassesTheLimit()
    {
        // 6 +- 1/2 at Q = 100 expects 6 * (1/2) * 100^2 / pi^2, about 3,040. A limit just under
        // refuses and one just over admits; None admits anything.
        Approximation[] enclosures = [At(BigRational.FromInteger(6), 1, 2)];
        BigRational expected = SurvivorCountGuard.Expected(enclosures, 100, SurvivorMode.Chart);
        long floor = (long)(expected.Numerator / expected.Denominator);

        SurvivorCountRefusal? refused = SurvivorCountGuard.Refuse(enclosures, 100, SurvivorMode.Chart, SurvivorLimit.At(floor));

        Assert.NotNull(refused);
        Assert.Equal(SurvivorCountBasis.Expected, refused.Basis);
        Assert.Equal(expected, refused.Count);
        Assert.Equal(floor, refused.Limit);
        Assert.Equal(1, refused.Prefixes);
        Assert.Null(refused.PassedAt);
        Assert.Null(SurvivorCountGuard.Refuse(enclosures, 100, SurvivorMode.Chart, SurvivorLimit.At(floor + 1)));
        Assert.Null(SurvivorCountGuard.Refuse(enclosures, 100, SurvivorMode.Chart, SurvivorLimit.None));
    }

    [Fact]
    public void Refuse_CountsAChartsPrefixesAndADeepWalksOneWalk()
    {
        Approximation[] enclosures = [At(BigRational.FromInteger(6), 1, 2), At(BigRational.FromInteger(6), 1, 4)];

        SurvivorCountRefusal? chart = SurvivorCountGuard.Refuse(enclosures, 100, SurvivorMode.Chart, SurvivorLimit.At(1));
        SurvivorCountRefusal? deep = SurvivorCountGuard.Refuse(enclosures, 100, SurvivorMode.Deep, SurvivorLimit.At(1));

        Assert.Equal(2, chart!.Prefixes);
        Assert.Equal(1, deep!.Prefixes);
        Assert.Equal(SurvivorMode.Deep, deep.Mode);
    }

    // ---------- FareyWalk's admission ----------

    [Fact]
    public void Admit_UnderFareyWalksToTheDerivedBoundUnderTheDefaultLimit()
    {
        Approximation[] enclosures = [At(BigRational.FromInteger(6), 1, 100)];
        SurvivorRequest request = Request(SurvivorMode.Chart, SurvivorWalkChoice.Farey);

        CountedWalk admitted = Assert.IsType<CountedWalk>(SurvivorWalkChoice.Farey.Admit(enclosures, 1_000, request));

        Assert.Equal(new SurvivorBound(1_000, null), admitted.Bound);
        Assert.False(admitted.Bound.IsCapped);
        Assert.Equal(SurvivorLimit.At(100_000_000), admitted.Limit);
        Assert.Equal(SurvivorCountGuard.Expected(enclosures, 1_000, SurvivorMode.Chart), admitted.Expected);
    }

    [Fact]
    public void Admit_UnderFareyRefusesAChartExpectedPastTheLimit()
    {
        // 6 +- 1/2 at Q = 20,000 expects about 1.2e8, past the default 1e8.
        Approximation[] enclosures = [At(BigRational.FromInteger(6), 1, 2)];
        SurvivorRequest request = Request(SurvivorMode.Chart, SurvivorWalkChoice.Farey);
        SurvivorCountRefusal expected = SurvivorCountGuard.Refuse(
            enclosures, 20_000, SurvivorMode.Chart, SurvivorLimit.At(SurvivorCountGuard.DefaultLimit))!;

        RefusedWalk refused = Assert.IsType<RefusedWalk>(SurvivorWalkChoice.Farey.Admit(enclosures, 20_000, request));

        Assert.Equal(SurvivorCountGuard.Describe(expected, request), refused.Message);
    }

    // ---------- what a count refusal says ----------

    [Fact]
    public void Describe_NamesTheWalkTheLimitAndTheFirstExponentForAChart()
    {
        SurvivorCountRefusal refusal = SurvivorCountRefusal.BeforeTheWalk(Ratio(123, 1), 100, 5_000, 7, SurvivorMode.Chart);

        string message = SurvivorCountGuard.Describe(refusal, Request(SurvivorMode.Chart, SurvivorWalkChoice.Farey));

        Assert.StartsWith("Refusing this run: FareyWalk is expected to find about", message, StringComparison.Ordinal);
        Assert.Contains("over its 7 walks, past the limit of 100.", message, StringComparison.Ordinal);
        Assert.Contains("Raise the FIRST exponent", message, StringComparison.Ordinal);
        Assert.Contains("'survivors 3 2 12 denominator'", message, StringComparison.Ordinal);
    }

    [Fact]
    public void Describe_SaysADeepWalkRefusedMidWalkHasNoScheduleKnob()
    {
        SurvivorCountRefusal refusal = SurvivorCountRefusal.DuringTheWalk(
            101, SurvivorLimit.At(100), 5_000, 1, null, SurvivorMode.Deep);

        string message = SurvivorCountGuard.Describe(refusal, Request(SurvivorMode.Deep, SurvivorWalkChoice.Farey));

        Assert.StartsWith("Refused mid-walk: FareyWalk passed the limit of 100 survivors, so there is no report.", message, StringComparison.Ordinal);
        Assert.Contains("An expectation is not a", message, StringComparison.Ordinal);
        Assert.Contains("No schedule change helps a deep walk", message, StringComparison.Ordinal);
        Assert.DoesNotContain("FIRST", message, StringComparison.Ordinal);
        Assert.Contains("'deep 3 2 12 denominator'", message, StringComparison.Ordinal);
    }

    [Fact]
    public void Describe_NamesTheChartPrefixThatPassed()
    {
        SurvivorCountRefusal refusal = SurvivorCountRefusal.DuringTheWalk(
            101, SurvivorLimit.At(100), 5_000, 7, 2, SurvivorMode.Chart);

        string message = SurvivorCountGuard.Describe(refusal, Request(SurvivorMode.Chart, SurvivorWalkChoice.Farey));

        Assert.Contains("passed the limit of 100 survivors in prefix 2 of 7", message, StringComparison.Ordinal);
    }

    // ---------- the labels name the walk that ran ----------

    [Theory]
    [InlineData("farey", "FareyWalk")]
    [InlineData("denominator", "DenominatorWalk")]
    public void Preamble_NamesTheWalk(string word, string name)
    {
        using var notes = new StringWriter(CultureInfo.InvariantCulture);

        SurvivorRun.Preamble(notes, Request(SurvivorMode.Chart, SurvivorWalkChoice.Named(word)!), 11);

        Assert.Contains("walk  " + name, notes.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("SurvivorSearch", notes.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Chart_NamesTheWalkInTheHeadingAndOnTheAxisNote()
    {
        Approximation[] enclosures = [At(BigRational.FromInteger(6), 1, 2), At(BigRational.FromInteger(6), 1, 4)];

        foreach (SurvivorWalkChoice walk in SurvivorWalkChoice.All)
        {
            SurvivorReport chart = SurvivorReport.Of(enclosures, 2, 4, walk.Walk, SurvivorLimit.None).Report;
            SurvivorReport deep = SurvivorReport.Deep(enclosures, new SurvivorBound(2, null), 4, walk.Walk, SurvivorLimit.None).Report;

            // Read as the parsed text a viewer sees, not as markup, which escapes the apostrophe.
            List<string> drawnChart = Texts(Draw(chart, walk));
            List<string> drawnDeep = Texts(Draw(deep, walk));

            Assert.Contains(drawnChart, line => line.Contains("     walk  " + walk.Name + "     guard  ", StringComparison.Ordinal));
            Assert.Contains(drawnDeep, line => line.Contains("     walk  " + walk.Name + ", once over every enclosure (deep)     guard  ", StringComparison.Ordinal));
            Assert.Contains(drawnChart, line => line.Contains("which is " + walk.Name + "'s own axis", StringComparison.Ordinal));
            Assert.DoesNotContain(drawnChart, line => line.Contains("SurvivorSearch", StringComparison.Ordinal));
        }
    }

    // ---------- the guard's own reading ----------

    [Fact]
    public void CountedWalk_ReadsTheSurvivorsEveryWalkFoundAgainstItsExpectation()
    {
        // A chart walks every prefix afresh, so what its guard counted is the sum of its counts -
        // 3 then 1 here - and that is the figure the reading must print beside the expectation.
        Approximation[] enclosures = [At(BigRational.FromInteger(6), 1, 2), At(BigRational.FromInteger(6), 1, 4)];
        SurvivorReport chart = SurvivorReport.Of(enclosures, 2, 4, SurvivorWalkChoice.Farey.Walk, SurvivorLimit.None).Report;
        var admitted = new CountedWalk(new SurvivorBound(2, null), SurvivorLimit.At(10), BigRational.FromInteger(5));

        Assert.Equal([3L, 1L], chart.Counts);
        Assert.Equal(4, chart.SurvivorsWalked);
        Assert.Equal(
            ["The guard expected about 5.0 survivors over the walks; they found 4."],
            admitted.Reading(chart, SurvivorMode.Chart, BigRational.One));
    }

    [Fact]
    public void SurvivorsWalked_IsADeepWalksOneCount()
    {
        Approximation[] enclosures = [At(BigRational.FromInteger(6), 1, 2), At(BigRational.FromInteger(6), 1, 4)];
        SurvivorReport deep = SurvivorReport.Deep(enclosures, new SurvivorBound(2, null), 4, SurvivorWalkChoice.Farey.Walk, SurvivorLimit.None).Report;

        Assert.Equal(deep.SurvivorCount, deep.SurvivorsWalked);
    }

    // ---------- the survivor limit, which the user chooses ----------

    [Theory]
    [InlineData("3 2 12 farey 500000000", 3, 2, 12, 500_000_000)]
    [InlineData("farey 1", 2, 2, 8, 1)]
    [InlineData("3 FAREY 20", 3, 2, 8, 20)]
    [InlineData("farey 9223372036854775807", 2, 2, 8, long.MaxValue)]
    public void Interpret_TakesALimitAfterFarey(string typed, int order, int first, int last, long limit)
    {
        foreach (SurvivorMode mode in new[] { SurvivorMode.Chart, SurvivorMode.Deep })
        {
            Assert.Null(SurvivorRun.Interpret(typed.Split(' '), mode, out SurvivorRequest request));

            Assert.Equal(new SurvivorRequest(order, first, last, mode, SurvivorWalkChoice.Farey, SurvivorLimit.At(limit)), request);
        }
    }

    [Fact]
    public void Interpret_DefaultsTheLimitToOneHundredMillionAndDenominatorToNone()
    {
        // Literals: the default is a policy with a recorded basis, and moving it should redden.
        Assert.Null(SurvivorRun.Interpret(["3"], SurvivorMode.Chart, out SurvivorRequest farey));
        Assert.Null(SurvivorRun.Interpret(["3", "denominator"], SurvivorMode.Chart, out SurvivorRequest reference));

        Assert.Equal(SurvivorLimit.At(100_000_000), farey.Limit);
        Assert.Equal(SurvivorLimit.None, reference.Limit);
    }

    [Fact]
    public void Interpret_RefusesALimitBesideDenominatorAndOffersTheOrderReading()
    {
        // The reference walk is bounded by time, so a limit given beside it would be ignored -
        // which would mislead whoever set it. The same slip may be a walk put before an order.
        string? refusal = SurvivorRun.Interpret(["denominator", "5"], SurvivorMode.Chart, out SurvivorRequest request);

        Assert.NotNull(refusal);
        Assert.Contains("'5' after 'denominator' would be a survivor limit, and DenominatorWalk takes none", refusal, StringComparison.Ordinal);
        Assert.Contains("'survivors 5 denominator'", refusal, StringComparison.Ordinal);
        Assert.Equal(default, request);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-5")]
    [InlineData("+5")]
    [InlineData("1e8")]
    [InlineData("1.5")]
    [InlineData("1,000")]
    [InlineData("9223372036854775808")]
    [InlineData("five")]
    public void Interpret_RefusesALimitThatIsNotAnExactPositiveInteger(string limit)
    {
        // Digits alone: nothing that parses may mean anything but the digits it shows, and no
        // floating point stands between the argument and the count - 1e8 is refused, not read.
        string? refusal = SurvivorRun.Interpret(["3", "2", "12", "farey", limit], SurvivorMode.Chart, out SurvivorRequest request);

        Assert.NotNull(refusal);
        Assert.Contains("'" + limit + "' is not a survivor limit", refusal, StringComparison.Ordinal);
        Assert.Equal(default, request);
    }

    [Fact]
    public void Interpret_RefusesAnythingAfterTheLimit()
    {
        string? refusal = SurvivorRun.Interpret(["3", "2", "12", "farey", "5", "6"], SurvivorMode.Chart, out _);

        Assert.NotNull(refusal);
        Assert.Contains("a survivor limit and nothing else", refusal, StringComparison.Ordinal);
    }

    [Fact]
    public void Invocation_CarriesALimitOnlyWhenItIsNotTheDefault()
    {
        SurvivorRequest request = Request(SurvivorMode.Chart, SurvivorWalkChoice.Farey);

        Assert.Equal("survivors 3 2 12", request.Invocation(2, 12));
        Assert.Equal("survivors 3 2 12 farey 5", (request with { Limit = SurvivorLimit.At(5) }).Invocation(2, 12));
    }

    [Fact]
    public void WithWalk_TakesTheNewWalksDefaultLimit()
    {
        // A plain with { Walk = ... } would carry the old walk's limit across, and the invocation
        // would then offer 'denominator 5' - a limit the reference walk does not take.
        SurvivorRequest limited = Request(SurvivorMode.Chart, SurvivorWalkChoice.Farey) with { Limit = SurvivorLimit.At(5) };

        SurvivorRequest reference = limited.WithWalk(SurvivorWalkChoice.Denominator);

        Assert.Equal(SurvivorLimit.None, reference.Limit);
        Assert.Equal("survivors 3 2 12 denominator", reference.Invocation(2, 12));
        Assert.Equal(SurvivorLimit.At(100_000_000), reference.WithWalk(SurvivorWalkChoice.Farey).Limit);
    }

    [Fact]
    public void GuardLabel_NamesEachWalksOwnGuard()
    {
        SurvivorRequest farey = Request(SurvivorMode.Chart, SurvivorWalkChoice.Farey);

        Assert.Equal("survivor limit 100,000,000", farey.GuardLabel);
        Assert.Equal("survivor limit 5", (farey with { Limit = SurvivorLimit.At(5) }).GuardLabel);
        Assert.Equal("time budget 300 s", Request(SurvivorMode.Chart, SurvivorWalkChoice.Denominator).GuardLabel);
    }

    [Fact]
    public void Preamble_ShowsTheLimitWhereverTheWalkIsShown()
    {
        using var notes = new StringWriter(CultureInfo.InvariantCulture);

        SurvivorRun.Preamble(notes, Request(SurvivorMode.Deep, SurvivorWalkChoice.Farey) with { Limit = SurvivorLimit.At(5) }, 11);

        Assert.Contains("  guard       survivor limit 5", notes.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Chart_ShowsTheGuardBesideTheWalk()
    {
        Approximation[] enclosures = [At(BigRational.FromInteger(6), 1, 2), At(BigRational.FromInteger(6), 1, 4)];
        SurvivorReport chart = SurvivorReport.Of(enclosures, 2, 4, SurvivorWalkChoice.Farey.Walk, SurvivorLimit.None).Report;

        using var document = new StringWriter(CultureInfo.InvariantCulture);
        SurvivorChart.Write(document, chart, new ChartCaption(
            "pi^2 / zeta(2)", "MachinPi, EulerMaclaurinZeta(2)", "FareyWalk", "survivor limit 5", "1e-2 .. 1e-4", "Q = 2"));

        Assert.Contains(Texts(document.ToString()), line => line.EndsWith("walk  FareyWalk     guard  survivor limit 5", StringComparison.Ordinal));
    }

    [Fact]
    public void Admit_UnderFareyWalksUnderTheRequestsOwnLimit()
    {
        // 6 +- 1/100 at Q = 1,000 expects 6 * (1/100) * 10^6 / pi^2, about 6,079: admitted at the
        // default, refused at 1,000, and the refusal suggests the smallest round limit above it.
        Approximation[] enclosures = [At(BigRational.FromInteger(6), 1, 100)];
        SurvivorRequest request = Request(SurvivorMode.Chart, SurvivorWalkChoice.Farey) with { Limit = SurvivorLimit.At(1_000) };

        RefusedWalk refused = Assert.IsType<RefusedWalk>(SurvivorWalkChoice.Farey.Admit(enclosures, 1_000, request));

        Assert.Contains("past the limit of 1,000.", refused.Message, StringComparison.Ordinal);
        Assert.Contains("'survivors 3 2 12 farey 10000'", refused.Message, StringComparison.Ordinal);
        Assert.IsType<CountedWalk>(SurvivorWalkChoice.Farey.Admit(enclosures, 1_000, request with { Limit = SurvivorLimit.At(10_000) }));
    }

    [Fact]
    public void Admit_RefusesARequestPairingAWalkWithALimitItDoesNotTake()
    {
        // No argument a caller can type builds either; Interpret refuses the one that would. Checked
        // before anything is timed, so the reference walk's calibration never runs here.
        Approximation[] enclosures = [At(BigRational.FromInteger(6), 1, 100)];
        SurvivorRequest unlimitedFarey = Request(SurvivorMode.Chart, SurvivorWalkChoice.Farey) with { Limit = SurvivorLimit.None };
        SurvivorRequest limitedReference = Request(SurvivorMode.Chart, SurvivorWalkChoice.Denominator) with { Limit = SurvivorLimit.At(5) };

        Assert.Throws<ArgumentException>(() => SurvivorWalkChoice.Farey.Admit(enclosures, 1_000, unlimitedFarey));
        Assert.Throws<ArgumentException>(() => SurvivorWalkChoice.Denominator.Admit(enclosures, 1_000, limitedReference));
        Assert.Throws<ArgumentException>(
            () => SurvivorWalkChoice.Farey.Admit(enclosures, 1_000, Request(SurvivorMode.Chart, SurvivorWalkChoice.Denominator)));
    }

    [Theory]
    [InlineData(0, 1, 1)]
    [InlineData(1, 1, 1)]
    [InlineData(3, 2, 2)]
    [InlineData(3, 1, 5)]
    [InlineData(5, 1, 5)]
    [InlineData(6, 1, 10)]
    [InlineData(418_923_113, 1, 500_000_000)]
    [InlineData(100_000_001, 1, 200_000_000)]
    public void RoundLimitAtOrAbove_IsTheSmallestOneTwoOrFiveTimesAPowerOfTenThatAdmits(long numerator, long denominator, long expected) =>
        Assert.Equal(expected, SurvivorCountGuard.RoundLimitAtOrAbove(Ratio(numerator, denominator)));

    [Fact]
    public void Describe_DuringTheWalkSaysWhereTheLimitIsSet()
    {
        SurvivorCountRefusal refusal = SurvivorCountRefusal.DuringTheWalk(
            101, SurvivorLimit.At(100), 5_000, 7, 2, SurvivorMode.Chart);

        string message = SurvivorCountGuard.Describe(refusal, Request(SurvivorMode.Chart, SurvivorWalkChoice.Farey));

        Assert.Contains("Or raise the limit, which is the number after 'farey'", message, StringComparison.Ordinal);
    }

    private static string Draw(SurvivorReport report, SurvivorWalkChoice walk)
    {
        using var document = new StringWriter(CultureInfo.InvariantCulture);
        SurvivorChart.Write(document, report, new ChartCaption(
            "pi^2 / zeta(2)", "MachinPi, EulerMaclaurinZeta(2)", walk.Name, walk.DescribeGuard(walk.DefaultLimit), "1e-2 .. 1e-4", "Q = 2"));

        return document.ToString();
    }

    private static List<string> Texts(string svg) =>
        [.. XDocument.Parse(svg).Descendants().Where(element => element.Name.LocalName == "text").Select(element => element.Value)];

    private static string Inv(FormattableString message) => message.ToString(CultureInfo.InvariantCulture);
}
