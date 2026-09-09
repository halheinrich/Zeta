using System.Globalization;
using System.Numerics;

namespace HalHeinrich.Numerics.Experiments;

/// <summary>
/// How a run identifies itself on the chart: what was run, with what, and where the denominator
/// bound came from.
/// </summary>
/// <param name="Title">The quantity, as <c>pi^2 / zeta(2)</c>.</param>
/// <param name="Providers">The two providers, named.</param>
/// <param name="Searcher">The searcher <see cref="RatioRun.Execute"/> was given.</param>
/// <param name="Schedule">The schedule and how many distinct enclosures it realised.</param>
/// <param name="BoundDerivation">Where <c>Q</c> came from, in one line.</param>
/// <remarks>
/// <b>Every field here is on the picture rather than beside it.</b> § 1 requires a bound to name
/// the searcher that produced it, and a chart travels: the exploration's second graph was
/// misleading because the cap it was drawn under was not on it. A file that carries its own basis
/// can be checked by whoever opens it next.
/// </remarks>
internal sealed record ChartCaption(
    string Title,
    string Providers,
    string Searcher,
    string Schedule,
    string BoundDerivation);

/// <summary>
/// The two charts the survivor command writes: the collapse of the survivor set, and each tracked
/// candidate's distance against the enclosure half-width that refutes it.
/// </summary>
/// <remarks>
/// <para>
/// <b>Two charts because they answer different questions, and the second is the one that shows a
/// proof.</b> The collapse is the headline - a count falling by orders of magnitude to a handful
/// or to nothing. But a count is a summary, and a reader who wants to know <i>why</i> a candidate
/// died has to be shown the moment it died. Drawing the half-width as its own series does that:
/// a candidate is excluded exactly where its distance rises above that line, which turns reading
/// a trend into seeing an exclusion.
/// </para>
/// <para>
/// <b>The lines can cross back, and that is the picture being honest.</b> Enclosures need not
/// nest, so a candidate refuted by an early enclosure can sit inside a later one and its distance
/// can fall back under the half-width. Refutation is permanent all the same - one enclosure
/// excluding it is enough - which is why the survivor set is an intersection across all of them
/// and never a filter on the latest. A chart that hid the crossing back would make the
/// intersection look like needless caution.
/// </para>
/// <para>
/// <b>Nothing here decides anything.</b> These are presentation, on the far side of the line
/// <c>../SPEC-rational-ratio.md</c> § 2 draws: the survivor counts were settled by enclosure
/// membership in exact arithmetic before any of it reached a pixel.
/// </para>
/// </remarks>
internal static class SurvivorChart
{
    private const double Width = 980;
    private const double PlotLeft = 78;
    private const double PlotRight = 946;
    private const double PanelHeight = 250;

    private const string Ink = "#1c1c1c";
    private const string Grid = "#dcdcdc";
    private const string HalfWidthInk = "#111111";

    /// <summary>The series colours, cycled when there are more candidates than colours.</summary>
    private static readonly string[] Palette =
    [
        "#1f6feb", "#c9401f", "#1a7f5a", "#8250df", "#b38600", "#0f6a7a",
    ];

    /// <summary>Writes the whole document.</summary>
    /// <param name="svg">Where the document goes - standard output, in this command.</param>
    /// <param name="report">The completed report.</param>
    /// <param name="caption">What the run was.</param>
    /// <exception cref="ArgumentNullException">Any argument is null.</exception>
    public static void Write(TextWriter svg, SurvivorReport report, ChartCaption caption)
    {
        ArgumentNullException.ThrowIfNull(svg);
        ArgumentNullException.ThrowIfNull(report);
        ArgumentNullException.ThrowIfNull(caption);

        double legend = 18 + (report.Tracked.Count * 15);
        double collapseTop = 202;
        double distanceTop = collapseTop + PanelHeight + 84;
        double height = distanceTop + PanelHeight + 62 + legend + 96;

        Svg.Open(svg, Width, height, caption.Title + " - survivors under a denominator bound");

        Heading(svg, report, caption);
        Collapse(svg, report, collapseTop);
        Distances(svg, report, distanceTop, legend);
        Caveat(svg, report, height - 60);

        Svg.Close(svg);
    }

    private static void Heading(TextWriter svg, SurvivorReport report, ChartCaption caption)
    {
        Svg.Text(svg, PlotLeft, 34, caption.Title + " - what the enclosures leave standing", "h1");

        Svg.Text(svg, PlotLeft, 56, "providers  " + caption.Providers + "     search  " + caption.Searcher, "body");
        Svg.Text(svg, PlotLeft, 73, "schedule   " + caption.Schedule, "body");
        Svg.Text(svg, PlotLeft, 90, "bound      " + caption.BoundDerivation, "body");

        string survivors = report.SurvivorCount == 0
            ? "empty - every rational of denominator at or below the bound is refuted"
            : string.Join("  ", report.Survivors.Select(Label)) +
                (report.SurvivorCount > report.Survivors.Count
                    ? string.Create(CultureInfo.InvariantCulture,
                        $"  ... and {report.SurvivorCount - report.Survivors.Count} more")
                    : string.Empty);

        Svg.Text(svg, PlotLeft, 114, string.Create(CultureInfo.InvariantCulture,
            $"SURVIVOR SET - {report.SurvivorCount:N0}"), "h2");
        Svg.Text(svg, PlotLeft, 132, survivors, "body");

        Svg.Text(svg, PlotLeft, 152, string.Create(CultureInfo.InvariantCulture,
            $"Of every rational whose denominator is at most {report.DenominatorBound}, these and only " +
            $"these are consistent with the enclosures."), "small");
    }

    private static void Collapse(TextWriter svg, SurvivorReport report, double top)
    {
        IReadOnlyList<long> counts = report.Counts;

        // The floor is 10^0, one survivor, because a log axis has no room for none. A count of
        // zero is drawn on that floor and labelled, rather than dropped - it is the strongest
        // outcome this bench produces and must not be the one the chart cannot show.
        double highest = counts.Count == 0 ? 0 : counts.Max(count => Exponent(count));
        var y = new Axis(0, Math.Max(highest, 1), top + PanelHeight, top);
        var x = Columns(counts.Count);

        Svg.Text(svg, PlotLeft, top - 26, "1. THE COLLAPSE - rationals still standing after each enclosure", "h2");
        Svg.Text(svg, PlotLeft, top - 10, "log scale; one point per distinct enclosure, not per target", "small");

        Frame(svg, top, y, x, counts.Count, "survivors");

        var points = new List<(double X, double Y)>();
        for (int index = 0; index < counts.Count; index++)
        {
            points.Add((x.At(index), y.At(Exponent(counts[index]))));
        }

        Svg.Polyline(svg, points, Palette[0], 2.0);

        for (int index = 0; index < counts.Count; index++)
        {
            (double px, double py) = points[index];
            Svg.Dot(svg, px, py, 3.5, Palette[0]);
            Svg.Text(svg, px, py - 9, Count(counts[index]), "tick", "middle");
        }
    }

    private static void Distances(TextWriter svg, SurvivorReport report, double top, double legend)
    {
        IReadOnlyList<Approximation> enclosures = report.Enclosures;
        IReadOnlyList<BigRational> tracked = report.Tracked;

        var exponents = new List<double>();
        foreach (Approximation enclosure in enclosures)
        {
            exponents.Add(Presentation.DecimalExponent(enclosure.MaxError));
            exponents.AddRange(tracked.Select(candidate => Distance(candidate, enclosure)));
        }

        // A distance of exactly zero has no logarithm. It means a candidate sitting on an
        // enclosure's centre, which no provider in this bench produces but which the axis must not
        // depend on: the finite exponents set the range and a zero is drawn on the floor.
        double[] finite = [.. exponents.Where(double.IsFinite)];
        double low = finite.Length == 0 ? -1 : finite.Min();
        double high = finite.Length == 0 ? 0 : finite.Max();

        var y = new Axis(low, high, top + PanelHeight, top);
        var x = Columns(enclosures.Count);

        Svg.Text(svg, PlotLeft, top - 26,
            "2. WHY - each candidate's distance from the ratio, against the half-width that refutes it", "h2");
        Svg.Text(svg, PlotLeft, top - 10,
            "a candidate is excluded exactly where its line rises above the dashed half-width", "small");

        Frame(svg, top, y, x, enclosures.Count, "distance");

        var halfWidths = new List<(double X, double Y)>();
        for (int index = 0; index < enclosures.Count; index++)
        {
            halfWidths.Add((x.At(index), y.At(Floored(Presentation.DecimalExponent(enclosures[index].MaxError), low))));
        }

        Svg.Polyline(svg, halfWidths, HalfWidthInk, 2.5, "6 4");

        for (int series = 0; series < tracked.Count; series++)
        {
            BigRational candidate = tracked[series];
            string colour = Palette[(series + 1) % Palette.Length];

            var line = new List<(double X, double Y)>();
            for (int index = 0; index < enclosures.Count; index++)
            {
                line.Add((x.At(index), y.At(Floored(Distance(candidate, enclosures[index]), low))));
            }

            Svg.Polyline(svg, line, colour, 1.8);

            for (int index = 0; index < enclosures.Count; index++)
            {
                if (!enclosures[index].Contains(candidate))
                {
                    Cross(svg, line[index].X, line[index].Y, colour);
                }
            }
        }

        Legend(svg, report, top + PanelHeight + 62);
    }

    private static void Legend(TextWriter svg, SurvivorReport report, double top)
    {
        IReadOnlyList<Approximation> enclosures = report.Enclosures;

        Svg.Text(svg, PlotLeft, top,
            "the survivors, then the candidates nearest the final ratio - the ones every enclosure refuted last",
            "small");

        Svg.Line(svg, PlotLeft, top + 15, PlotLeft + 26, top + 15, HalfWidthInk, 2.5, "6 4");
        Svg.Text(svg, PlotLeft + 34, top + 19, "enclosure half-width", "small");

        for (int series = 0; series < report.Tracked.Count; series++)
        {
            BigRational candidate = report.Tracked[series];
            string colour = Palette[(series + 1) % Palette.Length];
            double line = top + 34 + (series * 15);

            int refuted = -1;
            for (int index = 0; index < enclosures.Count && refuted < 0; index++)
            {
                if (!enclosures[index].Contains(candidate))
                {
                    refuted = index;
                }
            }

            Svg.Line(svg, PlotLeft, line - 4, PlotLeft + 26, line - 4, colour, 1.8);
            Svg.Text(svg, PlotLeft + 34, line, Label(candidate) + "   " + (refuted < 0
                ? "survives every enclosure"
                : string.Create(CultureInfo.InvariantCulture, $"refuted at enclosure {refuted}")), "small");
        }
    }

    private static void Caveat(TextWriter svg, SurvivorReport report, double top)
    {
        Svg.Text(svg, PlotLeft, top,
            "Numerics refute a rational relation and bound the height of one. Nothing finite establishes it.",
            "caveat");

        Svg.Text(svg, PlotLeft, top + 16, report.SurvivorCount == 0
            ? "An empty survivor set is a refutation, and it is the strongest result this bench produces."
            : "A short survivor set poses a conjecture and is not evidence: a deeper run refutes it and offers another.",
            "caveat");

        Svg.Text(svg, PlotLeft, top + 34,
            "The bound's axis is denominators, which is SurvivorSearch's own axis and not the run searcher's.",
            "small");
    }

    /// <summary>The frame, the decade gridlines, and the column ticks.</summary>
    private static void Frame(TextWriter svg, double top, Axis y, Axis x, int columns, string yLabel)
    {
        double bottom = top + PanelHeight;

        Svg.Rect(svg, PlotLeft, top, PlotRight - PlotLeft, PanelHeight, "#fbfbfb");

        foreach (int exponent in y.DecadeTicks(10))
        {
            double line = y.At(exponent);
            Svg.Line(svg, PlotLeft, line, PlotRight, line, Grid, 1.0);
            Svg.Text(svg, PlotLeft - 8, line + 4, string.Create(CultureInfo.InvariantCulture,
                $"1e{exponent}"), "tick", "end");
        }

        Svg.Line(svg, PlotLeft, top, PlotLeft, bottom, Ink, 1.2);
        Svg.Line(svg, PlotLeft, bottom, PlotRight, bottom, Ink, 1.2);

        for (int index = 0; index < columns; index++)
        {
            double tick = x.At(index);
            Svg.Line(svg, tick, bottom, tick, bottom + 5, Ink, 1.0);
            Svg.Text(svg, tick, bottom + 18, string.Create(CultureInfo.InvariantCulture, $"{index}"), "tick", "middle");
        }

        Svg.Text(svg, (PlotLeft + PlotRight) / 2, bottom + 36, "enclosure", "small", "middle");
        Svg.Text(svg, PlotLeft - 8, top - 8, yLabel, "small", "end");
    }

    /// <summary>The x axis over column indices, with a margin so the end points are not on the frame.</summary>
    private static Axis Columns(int count) =>
        new(0, Math.Max(count - 1, 1), PlotLeft + 26, PlotRight - 26);

    /// <summary>A cross marking the enclosure that excluded a candidate.</summary>
    private static void Cross(TextWriter svg, double x, double y, string colour)
    {
        Svg.Line(svg, x - 4, y - 4, x + 4, y + 4, colour, 1.6);
        Svg.Line(svg, x - 4, y + 4, x + 4, y - 4, colour, 1.6);
    }

    /// <summary>
    /// The exact distance from a candidate to an enclosure's centre, as a decimal exponent.
    /// </summary>
    /// <remarks>
    /// The subtraction and the absolute value are exact rational arithmetic; only the logarithm is
    /// not, and it is a coordinate. Comparing this against the half-width's exponent would be the
    /// mistake the boundary exists to prevent - membership is decided by
    /// <see cref="Approximation.Contains"/> on exact values, and the crosses on the chart come
    /// from that and not from these two doubles.
    /// </remarks>
    private static double Distance(BigRational candidate, Approximation enclosure) =>
        Presentation.DecimalExponent(BigRational.Abs(candidate - enclosure.Value));

    /// <summary>A non-finite exponent put on the axis floor, so a zero distance still draws.</summary>
    private static double Floored(double exponent, double low) =>
        double.IsFinite(exponent) ? exponent : low;

    /// <summary>A count as a decimal exponent, with zero put on the floor of the log axis.</summary>
    private static double Exponent(long count) =>
        count <= 0 ? 0 : Presentation.DecimalExponent(BigRational.FromInteger(count));

    /// <summary>A count as a label: the figure itself, which is the number the collapse is read from.</summary>
    private static string Count(long count) =>
        count.ToString("N0", CultureInfo.InvariantCulture);

    /// <summary>A candidate as <c>p/q</c>, or as a decimal where the digits would run away.</summary>
    private static string Label(BigRational value)
    {
        BigInteger digits = BigInteger.Max(BigInteger.Abs(value.Numerator), value.Denominator);

        return digits < BigInteger.Pow(10, 12)
            ? string.Create(CultureInfo.InvariantCulture, $"{value.Numerator}/{value.Denominator}")
            : Presentation.ToDecimal(value, 12);
    }
}
