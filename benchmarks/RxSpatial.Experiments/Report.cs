using System.Globalization;
using System.Text;

namespace RxSpatial.Experiments;

/// <summary>
/// Collects one CSV per figure and a Markdown summary in the output folder. The summary is
/// rewritten after every experiment, so a long run that is interrupted keeps what it measured.
/// </summary>
internal sealed class Report(string directory)
{
    private readonly StringBuilder summary = new();

    public string Directory { get; } = directory;

    public void WriteCsv(string name, IReadOnlyList<string> header, IEnumerable<IReadOnlyList<object>> rows)
    {
        StringBuilder csv = new();
        csv.AppendJoin(',', header).AppendLine();
        foreach (IReadOnlyList<object> row in rows)
            csv.AppendJoin(',', row.Select(Format)).AppendLine();

        File.WriteAllText(Path.Combine(Directory, name + ".csv"), csv.ToString());
    }

    public void Section(string markdown)
    {
        summary.Append(markdown.TrimEnd()).AppendLine().AppendLine();
        File.WriteAllText(Path.Combine(Directory, "summary.md"), summary.ToString());
    }

    /// <summary>A Markdown table; cells are formatted invariantly.</summary>
    public static string Table(IReadOnlyList<string> header, IEnumerable<IReadOnlyList<object>> rows)
    {
        StringBuilder table = new();
        table.Append("| ").AppendJoin(" | ", header).AppendLine(" |");
        table.Append('|').AppendJoin('|', header.Select(_ => "---")).AppendLine("|");
        foreach (IReadOnlyList<object> row in rows)
            table.Append("| ").AppendJoin(" | ", row.Select(Format)).AppendLine(" |");

        return table.ToString();
    }

    public static string Format(object value) => value switch
    {
        double d when double.IsNaN(d) => "n/a",
        double d when d % 1 == 0 => d.ToString("F0", CultureInfo.InvariantCulture),
        double d => d.ToString(Math.Abs(d) >= 100 ? "F1" : Math.Abs(d) >= 1 ? "F2" : "F4", CultureInfo.InvariantCulture),
        IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString() ?? string.Empty,
    };
}
