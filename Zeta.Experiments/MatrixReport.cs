using System.Globalization;
using System.Numerics;

namespace HalHeinrich.Numerics.Experiments;

/// <summary>
/// Renders a <see cref="TrendMatrix"/>: the table <c>../SPEC-rational-ratio.md</c> § 2 keeps as
/// presentation, and the admitted-or-excluded view of the same matrix that a ladder of rival
/// candidates is read from.
/// </summary>
/// <remarks>
/// <para>
/// <b>This lives in the runner rather than in the library, decided in step 6c.</b> Both consumers
/// of it are here - the zeta(2) walk's rival panel and the pi^3/zeta(3) run - so there is no
/// cross-assembly consumer to serve; <c>Zeta</c>'s stated identity is the composition of § 2 and
/// nothing else; and the nearest sibling, <c>RealConstants.Experiments</c>, puts its own
/// presentation in its runner for the same reason. If a consumer ever appears outside this
/// project, moving this is additive.
/// </para>
/// <para>
/// <b>Neither table is an instrument for detecting progress, and the presentation must not imply
/// it is.</b> § 2 was amended on 2026-09-08 to keep the matrix as presentation and stop letting it
/// decide; these renderers are on the presentation side of that line. Reading a row falling
/// towards zero as the answer is true in the limit and dangerous read operationally: step 6d
/// measured a near-miss row falling exactly as a genuine find would, flooring only below any
/// reachable precision, and nothing in the matrix distinguishes the two.
/// </para>
/// <para>
/// <b>"One row survives" is not the survivor set, and the resemblance is the trap.</b> A row left
/// standing is a claim about this matrix's own candidates - whichever ones some column's search
/// happened to surface - so a run that refuted nothing still leaves exactly one, and by § 4 a
/// pipeline that has stopped narrowing leaves one trivially, every column being identical. A
/// survivor is a claim about <i>every</i> rational under a denominator bound fixed in advance,
/// decided by enclosure membership rather than by a trend. Counting rows here settles nothing
/// about that set. So what is printed beneath these tables is § 1's bound and what survives it,
/// with the searcher whose axis it is named alongside - never a verdict read off a row.
/// </para>
/// </remarks>
internal static class MatrixReport
{
    /// <summary>Above this, a height is printed as a magnitude rather than as digits.</summary>
    /// <remarks>
    /// A search's own candidates stay well below it - pi^3/zeta(3) reaches about 1.0e8 after
    /// twelve columns - so they print in full, and only a deliberately planted rival such as
    /// <c>6 + 1e-12</c> is abbreviated. That is the right way round: the abbreviation loses
    /// nothing about a rung whose height the reader chose.
    /// </remarks>
    private static readonly BigInteger DigitsLimit = BigInteger.Pow(10, 9);

    /// <summary>Writes the trend matrix: rows are candidates, columns iterations, cells the distance.</summary>
    /// <param name="data">Where the table goes.</param>
    /// <param name="matrix">The matrix.</param>
    public static void WriteDistances(TextWriter data, TrendMatrix matrix)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(matrix);

        int labels = Math.Max(9, matrix.Rows.Count == 0 ? 0 : matrix.Rows.Max(row => Label(row).Length));

        data.Write("candidate".PadRight(labels));
        data.Write("  height      seen");
        for (int column = 0; column < matrix.Ratios.Count; column++)
        {
            data.Write(string.Create(CultureInfo.InvariantCulture, $"  {"c" + column,-9}"));
        }

        data.WriteLine();

        foreach (TrendRow row in matrix.Rows)
        {
            data.Write(Label(row).PadRight(labels));
            data.Write(string.Create(CultureInfo.InvariantCulture, $"  {Height(row.Height),-10}  {row.FirstSeenAt,4}"));

            foreach (BigRational distance in row.Distances)
            {
                data.Write(string.Create(CultureInfo.InvariantCulture, $"  {Presentation.Magnitude(distance),-9}"));
            }

            data.WriteLine();
        }
    }

    /// <summary>
    /// Writes the rival panel: one row per candidate, marked admitted or excluded by each
    /// column's enclosure.
    /// </summary>
    /// <param name="data">Where the table goes.</param>
    /// <param name="matrix">The matrix, whose rows are the ladder.</param>
    /// <param name="places">How many decimal places to label a rung with.</param>
    /// <remarks>
    /// <para>
    /// Read straight off <see cref="TrendMatrix.Ratios"/> with
    /// <see cref="Approximation.Contains"/>, which is what "admitted" means. The distance table
    /// above shows arithmetic converging; this one shows hypotheses being eliminated, and that is
    /// the method's actual content.
    /// </para>
    /// <para>
    /// Rows are labelled in decimal rather than as <c>p/q</c>, because a rung of a ladder placed
    /// near the answer has a numerator and denominator nobody can read - <c>6 + 1e-12</c> is
    /// <c>6000000000001/1000000000000</c> - while its decimal form is exactly the thing the
    /// reader chose.
    /// </para>
    /// </remarks>
    public static void WriteAdmissions(TextWriter data, TrendMatrix matrix, int places)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(matrix);

        int columns = matrix.Ratios.Count;
        int labels = Math.Max(9, places + 4);

        data.Write("candidate".PadRight(labels));
        data.Write("  height      ");
        for (int column = 0; column < columns; column++)
        {
            data.Write((column % 10).ToString(CultureInfo.InvariantCulture));
        }

        data.WriteLine("   verdict");

        foreach (TrendRow row in matrix.Rows)
        {
            data.Write(Presentation.ToDecimal(row.Candidate, places).PadRight(labels));
            data.Write(string.Create(CultureInfo.InvariantCulture, $"  {Height(row.Height),-10}  "));

            int excludedAt = -1;
            for (int column = 0; column < columns; column++)
            {
                bool admitted = matrix.Ratios[column].Contains(row.Candidate);
                data.Write(admitted ? '+' : '.');

                if (!admitted && excludedAt < 0)
                {
                    excludedAt = column;
                }
            }

            data.WriteLine(excludedAt < 0
                ? "   still admitted"
                : string.Create(CultureInfo.InvariantCulture, $"   refuted at c{excludedAt}"));
        }

        // No count row here. A column is one character wide, so a two-digit count cannot be
        // written under it without either lying or widening every column tenfold; the sequences
        // belong in prose beside the table, where they can also say which candidates they count.
    }

    /// <summary>
    /// How many of the given candidates each column's enclosure still admits.
    /// </summary>
    /// <param name="matrix">The matrix, for its per-column enclosures.</param>
    /// <param name="candidates">The candidates to count. Need not be rows of the matrix.</param>
    /// <returns>One count per column.</returns>
    /// <remarks>
    /// <b>Which candidates are counted has to be the caller's, and saying so is the point.</b> A
    /// matrix built with a ladder of planted rivals also holds every candidate the search
    /// proposed, so "how many survive" has one answer over the rivals and a different one over
    /// every row - and the two differ by exactly the candidates the search found, which is not a
    /// rounding. A sequence reported without its basis invites comparison with a sequence
    /// computed on another, and two such sequences can be the same length.
    /// </remarks>
    public static IReadOnlyList<int> AdmissionCounts(TrendMatrix matrix, IEnumerable<BigRational> candidates)
    {
        ArgumentNullException.ThrowIfNull(matrix);
        ArgumentNullException.ThrowIfNull(candidates);

        BigRational[] counted = [.. candidates];

        return [.. Enumerable
            .Range(0, matrix.Ratios.Count)
            .Select(column => counted.Count(matrix.Ratios[column].Contains))];
    }

    private static string Label(TrendRow row) =>
        string.Create(CultureInfo.InvariantCulture, $"{row.Candidate.Numerator}/{row.Candidate.Denominator}");

    private static string Height(BigInteger height) =>
        height < DigitsLimit
            ? height.ToString(CultureInfo.InvariantCulture)
            : string.Create(CultureInfo.InvariantCulture, $"1e{BigInteger.Log10(height):F1}");
}
