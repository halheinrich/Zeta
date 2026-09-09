using System.Globalization;
using System.Xml.Linq;
using HalHeinrich.Numerics.Experiments;

namespace HalHeinrich.Numerics.Tests;

/// <summary>
/// The chart geometry and the markup it produces: the affine map an axis is, the decade ticks it
/// offers, and that a whole document comes out as well-formed XML.
/// </summary>
/// <remarks>
/// <para>
/// <b>These are here because the coordinate transform is the exactness boundary.</b> Everything
/// upstream of it is exact rational arithmetic that other suites already hold; everything
/// downstream is a picture. The transform itself is the one step that can be silently wrong -
/// an axis inverted, a degenerate range dividing by zero, a value clamped where it should
/// extrapolate - and none of those show up as an error, only as a chart that reads plausibly and
/// says the wrong thing.
/// </para>
/// <para>
/// The experiments themselves are not run here. They have no pass or fail and depend on
/// wall-clock time, which <c>../AGENTS.md</c> § Exactness discipline keeps out of a test project;
/// what is held here is the geometry, which has neither property.
/// </para>
/// </remarks>
public sealed class SvgTests
{
    private static string Inv(FormattableString message) => message.ToString(CultureInfo.InvariantCulture);

    // ---------- the affine map ----------

    [Fact]
    public void At_MapsBothEndsExactly()
    {
        var axis = new Axis(0, 10, 100, 500);

        Assert.Equal(100, axis.At(0), 9);
        Assert.Equal(500, axis.At(10), 9);
    }

    [Fact]
    public void At_MapsTheMidpointToTheMiddleOfThePixelRange()
    {
        var axis = new Axis(2, 6, 40, 240);

        Assert.Equal(140, axis.At(4), 9);
    }

    [Fact]
    public void At_HonoursAnInvertedPixelRange()
    {
        // The ordinary case for a vertical axis: SVG's y grows downward, so the low value sits at
        // the large pixel. An axis that quietly re-ordered its pixels would draw every chart
        // upside down and still look like a chart.
        var axis = new Axis(0, 4, 300, 100);

        Assert.Equal(300, axis.At(0), 9);
        Assert.Equal(200, axis.At(2), 9);
        Assert.Equal(100, axis.At(4), 9);
    }

    [Fact]
    public void At_ExtrapolatesOutsideTheRangeRatherThanClamping()
    {
        // A point outside the range means the range was chosen too small. Drawing it on the edge
        // would hide that, and a chart whose worst point is always on the frame is a chart nobody
        // can read a scale off.
        var axis = new Axis(0, 10, 0, 100);

        Assert.Equal(-50, axis.At(-5), 9);
        Assert.Equal(150, axis.At(15), 9);
    }

    [Fact]
    public void At_PutsADegenerateRangeInTheMiddleInsteadOfDividingByZero()
    {
        // Reached by real data, not only by a malformed axis: a run whose survivor count is 1 in
        // every column has exactly one distinct value to plot.
        var axis = new Axis(3, 3, 80, 280);

        Assert.Equal(180, axis.At(3), 9);
        Assert.Equal(180, axis.At(99), 9);
    }

    // ---------- decade ticks ----------

    [Fact]
    public void DecadeTicks_ReturnsTheWholeExponentsInRange()
    {
        var axis = new Axis(-0.4, 5.8, 400, 100);

        Assert.Equal([0, 1, 2, 3, 4, 5], axis.DecadeTicks(10));
    }

    [Fact]
    public void DecadeTicks_ReadsAnInvertedRangeTheSameWay()
    {
        var axis = new Axis(5.8, -0.4, 100, 400);

        Assert.Equal([0, 1, 2, 3, 4, 5], axis.DecadeTicks(10));
    }

    [Fact]
    public void DecadeTicks_StridesRatherThanCrowding()
    {
        var axis = new Axis(0, 25, 400, 100);
        IReadOnlyList<int> ticks = axis.DecadeTicks(10);

        Assert.Equal([0, 3, 6, 9, 12, 15, 18, 21, 24], ticks);
        Assert.True(ticks.Count <= 10, Inv($"{ticks.Count} ticks were offered where 10 was the most."));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(7)]
    [InlineData(50)]
    public void DecadeTicks_NeverOffersMoreThanItWasAsked(int most)
    {
        var axis = new Axis(-30, 91.5, 400, 100);

        Assert.True(axis.DecadeTicks(most).Count <= most);
    }

    [Fact]
    public void DecadeTicks_IsEmptyWhenNoWholeExponentIsInRange()
    {
        var axis = new Axis(0.2, 0.9, 400, 100);

        Assert.Empty(axis.DecadeTicks(10));
    }

    [Fact]
    public void DecadeTicks_RefusesAnImpossibleCount()
    {
        var axis = new Axis(0, 10, 400, 100);

        Assert.Throws<ArgumentOutOfRangeException>(() => axis.DecadeTicks(0));
    }

    // ---------- markup ----------

    [Fact]
    public void Escape_ReplacesEveryXmlEntityAndDoesNotDoubleTheAmpersand()
    {
        // The ampersand has to go first. Replacing it last would escape the ampersands the other
        // four replacements had just introduced, giving &amp;lt; where &lt; was meant.
        Assert.Equal("&amp;lt;", Svg.Escape("&lt;"));
        Assert.Equal("&lt;a b=&quot;c&quot;&gt;", Svg.Escape("<a b=\"c\">"));
        Assert.Equal("it&apos;s", Svg.Escape("it's"));
    }

    [Fact]
    public void Escape_LeavesOrdinaryTextAlone()
    {
        Assert.Equal("pi^2 / zeta(2)", Svg.Escape("pi^2 / zeta(2)"));
    }

    [Fact]
    public void Number_IsInvariantAndFixedWidth()
    {
        // A decimal comma from a machine's locale is not valid SVG, and a varying digit count
        // makes the file undiffable between runs that drew the same picture.
        Assert.Equal("1.50", Svg.Number(1.5));
        Assert.Equal("-0.33", Svg.Number(-1.0 / 3.0));
        Assert.Equal("1000.00", Svg.Number(1000));
    }

    [Fact]
    public void Polyline_DrawsNothingWithFewerThanTwoPoints()
    {
        using var one = new StringWriter(CultureInfo.InvariantCulture);
        Svg.Polyline(one, [(1, 2)], "#000000", 1);

        Assert.Equal(string.Empty, one.ToString());
    }

    [Fact]
    public void Polyline_WritesEveryPointInOrder()
    {
        using var line = new StringWriter(CultureInfo.InvariantCulture);
        Svg.Polyline(line, [(1, 2), (3, 4)], "#000000", 1.5);

        Assert.Contains("points=\"1.00,2.00 3.00,4.00\"", line.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Open_AndClose_ProduceWellFormedXml()
    {
        using var document = new StringWriter(CultureInfo.InvariantCulture);

        Svg.Open(document, 100, 50, "a title with <angles> & an ampersand");
        Svg.Text(document, 1, 2, "it's \"quoted\"", "body");
        Svg.Line(document, 0, 0, 1, 1, "#000000", 1, "2 2");
        Svg.Dot(document, 5, 5, 2, "#ffffff");
        Svg.Close(document);

        // Parsing is the assertion. An escaping bug produces markup that still looks like SVG in a
        // diff and fails to render, which is exactly the failure a string comparison would miss.
        XDocument parsed = XDocument.Parse(document.ToString());

        Assert.Equal("svg", parsed.Root?.Name.LocalName);
        Assert.Equal("http://www.w3.org/2000/svg", parsed.Root?.Name.NamespaceName);
    }
}
