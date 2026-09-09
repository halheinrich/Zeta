using System.Globalization;

namespace HalHeinrich.Numerics.Experiments;

/// <summary>
/// A linear map from a range of values onto a range of pixels.
/// </summary>
/// <remarks>
/// <para>
/// <b>This is the exactness boundary, and it is deliberately the whole of it.</b>
/// <c>../SPEC-rational-ratio.md</c> § 2 permits decimals at presentation and nowhere else, and a
/// log axis needs one: a pixel is a <see cref="double"/> whatever the value behind it was. So
/// every quantity this repository plots is computed in exact rationals and reaches a chart as an
/// exact value; <see cref="Presentation.DecimalExponent"/> turns it into a decimal exponent and
/// this type turns that exponent into a coordinate. Nothing downstream of here feeds back into a
/// value, a bound or a decision - the output is a picture.
/// </para>
/// <para>
/// A log axis is this type over exponents rather than a second type: the caller takes the
/// logarithm and hands over the result, so the only arithmetic here is the affine map that a
/// linear axis needs anyway. That also keeps the logarithm at the one call site where the
/// conversion from exact to inexact is visible in the reading, instead of hidden inside a scale.
/// </para>
/// <para>
/// <see cref="LowPixel"/> may exceed <see cref="HighPixel"/>, which is the ordinary case for a
/// vertical axis: SVG's y grows downward, so a low value maps to a large pixel.
/// </para>
/// </remarks>
/// <param name="LowValue">The value at <paramref name="LowPixel"/>.</param>
/// <param name="HighValue">The value at <paramref name="HighPixel"/>.</param>
/// <param name="LowPixel">The pixel <paramref name="LowValue"/> maps to.</param>
/// <param name="HighPixel">The pixel <paramref name="HighValue"/> maps to.</param>
internal readonly record struct Axis(double LowValue, double HighValue, double LowPixel, double HighPixel)
{
    /// <summary>Maps a value to its pixel, extrapolating outside the range rather than clamping.</summary>
    /// <param name="value">The value.</param>
    /// <returns>The pixel.</returns>
    /// <remarks>
    /// <b>A degenerate range maps everything to the middle of the pixel range</b> rather than
    /// dividing by zero. It is reached by real data and not only by a malformed axis: a run whose
    /// survivor count is 1 in every column has one distinct value to plot, and a flat line down
    /// the centre is the honest picture of it. Clamping instead of extrapolating would be the
    /// wrong choice at the other end - a point outside the range is a range chosen too small, and
    /// drawing it on the edge would hide that.
    /// </remarks>
    public double At(double value)
    {
        double span = HighValue - LowValue;

        return span == 0.0
            ? (LowPixel + HighPixel) / 2.0
            : LowPixel + (((value - LowValue) / span) * (HighPixel - LowPixel));
    }

    /// <summary>The whole decade exponents this axis spans, thinned to at most a given count.</summary>
    /// <param name="most">The largest number of ticks to return. At least 1.</param>
    /// <returns>The exponents, ascending, always including the first one in range.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="most"/> is below 1.</exception>
    /// <remarks>
    /// Thinning by a stride rather than by rounding the range outward: the axis is built from the
    /// data, so widening it to land on round numbers would misreport how much of the picture is
    /// data and how much is margin.
    /// </remarks>
    public IReadOnlyList<int> DecadeTicks(int most)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(most, 1);

        int first = (int)Math.Ceiling(Math.Min(LowValue, HighValue));
        int last = (int)Math.Floor(Math.Max(LowValue, HighValue));

        if (last < first)
        {
            return [];
        }

        int stride = ((last - first) / most) + 1;
        var ticks = new List<int>();

        for (int exponent = first; exponent <= last; exponent += stride)
        {
            ticks.Add(exponent);
        }

        return ticks;
    }
}

/// <summary>
/// The SVG primitives the survivor charts are drawn from: elements written straight to a writer,
/// with the two conversions a chart needs - a coordinate to a string, and text to markup.
/// </summary>
/// <remarks>
/// <para>
/// <b>No library, no script, no external reference.</b> The output is one self-contained document
/// that a browser renders and <c>git diff</c> reads. That rules out a charting library, and it
/// also rules out anything fetched at render time, which would make the picture depend on a
/// network the run does not otherwise touch.
/// </para>
/// <para>
/// <b>Every number is formatted to two decimal places under
/// <see cref="CultureInfo.InvariantCulture"/>, so the same run produces the same bytes.</b> A
/// chart that is re-rendered on every run is a chart nobody can diff, and a decimal comma from a
/// machine's locale is not even valid SVG.
/// </para>
/// </remarks>
internal static class Svg
{
    /// <summary>Renders a coordinate.</summary>
    /// <param name="value">The coordinate.</param>
    /// <returns>The coordinate to two decimal places.</returns>
    public static string Number(double value) =>
        string.Create(CultureInfo.InvariantCulture, $"{value:F2}");

    /// <summary>Escapes text for an SVG text node or attribute value.</summary>
    /// <param name="text">The text.</param>
    /// <returns>The text with the five XML entities replaced.</returns>
    /// <remarks>
    /// The ampersand goes first, because replacing it after the others would escape the
    /// ampersands they just introduced. Apostrophe and quote are escaped although no caller
    /// currently puts either in an attribute, since which of the two contexts a string lands in
    /// is not this method's to know.
    /// </remarks>
    public static string Escape(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        return text
            .Replace("&", "&amp;", StringComparison.Ordinal)
            .Replace("<", "&lt;", StringComparison.Ordinal)
            .Replace(">", "&gt;", StringComparison.Ordinal)
            .Replace("\"", "&quot;", StringComparison.Ordinal)
            .Replace("'", "&apos;", StringComparison.Ordinal);
    }

    /// <summary>Opens the document and writes the stylesheet every element below relies on.</summary>
    /// <param name="svg">Where the document goes.</param>
    /// <param name="width">The document width in pixels.</param>
    /// <param name="height">The document height in pixels.</param>
    /// <param name="title">The document's accessible title.</param>
    public static void Open(TextWriter svg, double width, double height, string title)
    {
        ArgumentNullException.ThrowIfNull(svg);

        svg.WriteLine(string.Create(CultureInfo.InvariantCulture,
            $"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"{Number(width)}\" " +
            $"height=\"{Number(height)}\" viewBox=\"0 0 {Number(width)} {Number(height)}\">"));
        svg.WriteLine(string.Create(CultureInfo.InvariantCulture, $"<title>{Escape(title)}</title>"));
        svg.WriteLine("<style>");
        svg.WriteLine("  text { font-family: ui-monospace, 'DejaVu Sans Mono', Consolas, monospace; fill: #1c1c1c; }");
        svg.WriteLine("  .h1 { font-size: 17px; font-weight: 700; }");
        svg.WriteLine("  .h2 { font-size: 13px; font-weight: 700; }");
        svg.WriteLine("  .body { font-size: 12px; }");
        svg.WriteLine("  .small { font-size: 11px; fill: #4a4a4a; }");
        svg.WriteLine("  .tick { font-size: 10px; fill: #6a6a6a; }");
        svg.WriteLine("  .caveat { font-size: 11px; fill: #7a3b00; }");
        svg.WriteLine("</style>");
        Rect(svg, 0, 0, width, height, "#ffffff");
    }

    /// <summary>Closes the document.</summary>
    /// <param name="svg">Where the document goes.</param>
    public static void Close(TextWriter svg)
    {
        ArgumentNullException.ThrowIfNull(svg);
        svg.WriteLine("</svg>");
    }

    /// <summary>Writes a filled rectangle.</summary>
    /// <param name="svg">Where the element goes.</param>
    /// <param name="x">The left edge.</param>
    /// <param name="y">The top edge.</param>
    /// <param name="width">The width.</param>
    /// <param name="height">The height.</param>
    /// <param name="fill">The fill colour.</param>
    public static void Rect(TextWriter svg, double x, double y, double width, double height, string fill)
    {
        ArgumentNullException.ThrowIfNull(svg);

        svg.WriteLine(string.Create(CultureInfo.InvariantCulture,
            $"<rect x=\"{Number(x)}\" y=\"{Number(y)}\" width=\"{Number(width)}\" " +
            $"height=\"{Number(height)}\" fill=\"{Escape(fill)}\"/>"));
    }

    /// <summary>Writes a straight line.</summary>
    /// <param name="svg">Where the element goes.</param>
    /// <param name="x1">The first x.</param>
    /// <param name="y1">The first y.</param>
    /// <param name="x2">The second x.</param>
    /// <param name="y2">The second y.</param>
    /// <param name="stroke">The stroke colour.</param>
    /// <param name="width">The stroke width.</param>
    /// <param name="dash">The dash pattern, or null for a solid line.</param>
    public static void Line(
        TextWriter svg,
        double x1,
        double y1,
        double x2,
        double y2,
        string stroke,
        double width,
        string? dash = null)
    {
        ArgumentNullException.ThrowIfNull(svg);

        string dashes = dash is null
            ? string.Empty
            : string.Create(CultureInfo.InvariantCulture, $" stroke-dasharray=\"{Escape(dash)}\"");

        svg.WriteLine(string.Create(CultureInfo.InvariantCulture,
            $"<line x1=\"{Number(x1)}\" y1=\"{Number(y1)}\" x2=\"{Number(x2)}\" y2=\"{Number(y2)}\" " +
            $"stroke=\"{Escape(stroke)}\" stroke-width=\"{Number(width)}\"{dashes}/>"));
    }

    /// <summary>Writes a run of text.</summary>
    /// <param name="svg">Where the element goes.</param>
    /// <param name="x">The anchor x.</param>
    /// <param name="y">The baseline y.</param>
    /// <param name="text">The text, escaped here.</param>
    /// <param name="style">The stylesheet class.</param>
    /// <param name="anchor">The text anchor, or null for the default start.</param>
    public static void Text(
        TextWriter svg,
        double x,
        double y,
        string text,
        string style,
        string? anchor = null)
    {
        ArgumentNullException.ThrowIfNull(svg);

        string anchored = anchor is null
            ? string.Empty
            : string.Create(CultureInfo.InvariantCulture, $" text-anchor=\"{Escape(anchor)}\"");

        svg.WriteLine(string.Create(CultureInfo.InvariantCulture,
            $"<text x=\"{Number(x)}\" y=\"{Number(y)}\" class=\"{Escape(style)}\"{anchored}>{Escape(text)}</text>"));
    }

    /// <summary>Writes an open polyline through the given points.</summary>
    /// <param name="svg">Where the element goes.</param>
    /// <param name="points">The points, in order. Fewer than two draws nothing.</param>
    /// <param name="stroke">The stroke colour.</param>
    /// <param name="width">The stroke width.</param>
    /// <param name="dash">The dash pattern, or null for a solid line.</param>
    public static void Polyline(
        TextWriter svg,
        IReadOnlyList<(double X, double Y)> points,
        string stroke,
        double width,
        string? dash = null)
    {
        ArgumentNullException.ThrowIfNull(svg);
        ArgumentNullException.ThrowIfNull(points);

        if (points.Count < 2)
        {
            return;
        }

        string dashes = dash is null
            ? string.Empty
            : string.Create(CultureInfo.InvariantCulture, $" stroke-dasharray=\"{Escape(dash)}\"");

        string path = string.Join(
            " ",
            points.Select(point => string.Create(CultureInfo.InvariantCulture,
                $"{Number(point.X)},{Number(point.Y)}")));

        svg.WriteLine(string.Create(CultureInfo.InvariantCulture,
            $"<polyline points=\"{path}\" fill=\"none\" stroke=\"{Escape(stroke)}\" " +
            $"stroke-width=\"{Number(width)}\"{dashes}/>"));
    }

    /// <summary>Writes a filled circle, for a point on a series.</summary>
    /// <param name="svg">Where the element goes.</param>
    /// <param name="x">The centre x.</param>
    /// <param name="y">The centre y.</param>
    /// <param name="radius">The radius.</param>
    /// <param name="fill">The fill colour.</param>
    public static void Dot(TextWriter svg, double x, double y, double radius, string fill)
    {
        ArgumentNullException.ThrowIfNull(svg);

        svg.WriteLine(string.Create(CultureInfo.InvariantCulture,
            $"<circle cx=\"{Number(x)}\" cy=\"{Number(y)}\" r=\"{Number(radius)}\" fill=\"{Escape(fill)}\"/>"));
    }
}
