using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;

namespace DaisiProvider.Desktop;

// Turns the small Markdown subset of the Help files into a FlowDocument: headings, paragraphs,
// lists, tables, code blocks, `code` and **bold**.
public static partial class HelpDocumentBuilder
{
    private static readonly FontFamily CodeFont = new("Consolas");
    private static readonly Brush CodeBackground = new SolidColorBrush(Color.FromRgb(0xF1, 0xF5, 0xF9));
    private static readonly Brush TableLine = new SolidColorBrush(Color.FromRgb(0xD8, 0xDE, 0xE6));

    public static FlowDocument Build(string markdown)
    {
        var document = new FlowDocument
        {
            FontFamily = new FontFamily("Segoe UI"),
            FontSize = 13,
            PagePadding = new Thickness(20, 14, 20, 14),
            ColumnWidth = double.MaxValue,
            TextAlignment = TextAlignment.Left
        };

        var lines = markdown.Replace("\r\n", "\n").Split('\n');
        var index = 0;
        while (index < lines.Length)
        {
            var line = lines[index];
            if (string.IsNullOrWhiteSpace(line))
            {
                index++;
            }
            else if (line.StartsWith("```", StringComparison.Ordinal))
            {
                var code = new List<string>();
                for (index++; index < lines.Length && !lines[index].StartsWith("```", StringComparison.Ordinal); index++)
                {
                    code.Add(lines[index]);
                }

                index++;
                document.Blocks.Add(CodeBlock(code));
            }
            else if (line.StartsWith('#'))
            {
                document.Blocks.Add(Heading(line));
                index++;
            }
            else if (line.StartsWith('|'))
            {
                var rows = TakeWhile(lines, ref index, entry => entry.StartsWith('|'));
                document.Blocks.Add(TableBlock(rows));
            }
            else if (line.StartsWith("- ", StringComparison.Ordinal))
            {
                document.Blocks.Add(ListBlock(lines, ref index, BulletPattern(), TextMarkerStyle.Disc));
            }
            else if (NumberPattern().IsMatch(line))
            {
                document.Blocks.Add(ListBlock(lines, ref index, NumberPattern(), TextMarkerStyle.Decimal));
            }
            else
            {
                var text = TakeWhile(lines, ref index, IsParagraphLine);
                document.Blocks.Add(TextParagraph(string.Join(' ', text)));
            }
        }

        return document;
    }

    private static bool IsParagraphLine(string line) =>
        !string.IsNullOrWhiteSpace(line) &&
        !line.StartsWith('#') &&
        !line.StartsWith('|') &&
        !line.StartsWith("```", StringComparison.Ordinal) &&
        !line.StartsWith("- ", StringComparison.Ordinal) &&
        !NumberPattern().IsMatch(line);

    private static List<string> TakeWhile(string[] lines, ref int index, Func<string, bool> predicate)
    {
        var taken = new List<string>();
        while (index < lines.Length && predicate(lines[index]))
        {
            taken.Add(lines[index]);
            index++;
        }

        return taken;
    }

    private static Paragraph Heading(string line)
    {
        var level = line.TakeWhile(character => character == '#').Count();
        var paragraph = TextParagraph(line[level..].Trim());
        paragraph.FontWeight = FontWeights.SemiBold;
        paragraph.FontSize = level switch { 1 => 21, 2 => 16, _ => 14 };
        paragraph.Margin = new Thickness(0, level == 1 ? 0 : 14, 0, 6);
        return paragraph;
    }

    private static Paragraph TextParagraph(string text)
    {
        var paragraph = new Paragraph { Margin = new Thickness(0, 0, 0, 8), LineHeight = 18 };
        AddInlines(paragraph.Inlines, text);
        return paragraph;
    }

    private static Paragraph CodeBlock(IEnumerable<string> lines) =>
        new(new Run(string.Join('\n', lines)))
        {
            FontFamily = CodeFont,
            FontSize = 12,
            Background = CodeBackground,
            Padding = new Thickness(10, 8, 10, 8),
            Margin = new Thickness(0, 2, 0, 10)
        };

    private static List ListBlock(string[] lines, ref int index, Regex marker, TextMarkerStyle style)
    {
        var list = new List { MarkerStyle = style, Margin = new Thickness(0, 0, 0, 8), Padding = new Thickness(20, 0, 0, 0) };
        while (index < lines.Length && marker.IsMatch(lines[index]))
        {
            var text = new StringBuilder(marker.Replace(lines[index], string.Empty));
            index++;

            // Wrapped lines of the item are indented.
            while (index < lines.Length && lines[index].StartsWith("  ", StringComparison.Ordinal))
            {
                text.Append(' ').Append(lines[index].Trim());
                index++;
            }

            var paragraph = TextParagraph(text.ToString());
            paragraph.Margin = new Thickness(0, 0, 0, 3);
            list.ListItems.Add(new ListItem(paragraph));
        }

        return list;
    }

    private static Table TableBlock(IReadOnlyList<string> rows)
    {
        var cells = rows
            .Where(row => !SeparatorPattern().IsMatch(row))
            .Select(row => row.Trim().Trim('|').Split('|').Select(cell => cell.Trim()).ToArray())
            .ToArray();
        var columnCount = cells.Max(row => row.Length);

        var table = new Table { CellSpacing = 0, Margin = new Thickness(0, 2, 0, 10), BorderBrush = TableLine, BorderThickness = new Thickness(1, 1, 0, 0) };
        for (var column = 0; column < columnCount; column++)
        {
            // Wider text gets a wider column, within limits.
            var longest = cells.Max(row => column < row.Length ? row[column].Length : 0);
            table.Columns.Add(new TableColumn { Width = new GridLength(Math.Clamp(longest, 10, 45), GridUnitType.Star) });
        }

        var group = new TableRowGroup();
        for (var rowIndex = 0; rowIndex < cells.Length; rowIndex++)
        {
            var row = new TableRow();
            foreach (var text in cells[rowIndex])
            {
                var paragraph = TextParagraph(text);
                paragraph.Margin = new Thickness(0);
                paragraph.FontSize = 12;
                row.Cells.Add(new TableCell(paragraph)
                {
                    Padding = new Thickness(6, 3, 6, 3),
                    BorderBrush = TableLine,
                    BorderThickness = new Thickness(0, 0, 1, 1),
                    FontWeight = rowIndex == 0 ? FontWeights.SemiBold : FontWeights.Normal,
                    Background = rowIndex == 0 ? CodeBackground : null
                });
            }

            group.Rows.Add(row);
        }

        table.RowGroups.Add(group);
        return table;
    }

    private static void AddInlines(InlineCollection inlines, string text)
    {
        var position = 0;
        foreach (Match match in InlinePattern().Matches(text))
        {
            if (match.Index > position)
            {
                inlines.Add(new Run(text[position..match.Index]));
            }

            inlines.Add(match.Groups["code"].Success
                ? new Run(match.Groups["code"].Value) { FontFamily = CodeFont, Background = CodeBackground }
                : new Bold(new Run(match.Groups["bold"].Value)));
            position = match.Index + match.Length;
        }

        if (position < text.Length)
        {
            inlines.Add(new Run(text[position..]));
        }
    }

    [GeneratedRegex(@"^- ")]
    private static partial Regex BulletPattern();

    [GeneratedRegex(@"^\d+\. ")]
    private static partial Regex NumberPattern();

    [GeneratedRegex(@"^\|[\s:|-]+\|$")]
    private static partial Regex SeparatorPattern();

    [GeneratedRegex(@"`(?<code>[^`]+)`|\*\*(?<bold>.+?)\*\*")]
    private static partial Regex InlinePattern();
}
