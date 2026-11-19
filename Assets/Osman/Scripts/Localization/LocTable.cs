using System.Collections.Generic;
using System.Text;

// CSV dil tablosu: ilk sütun key, son sütun note olabilir, aradakiler diller.
public sealed class LocTable
{
    readonly List<string> languages = new List<string>();
    readonly List<string> keys = new List<string>();
    readonly List<string> duplicateKeys = new List<string>();
    readonly List<string> duplicateLanguages = new List<string>();
    readonly Dictionary<string, Dictionary<string, string>> cells = new Dictionary<string, Dictionary<string, string>>();

    public IReadOnlyList<string> Languages => languages;
    public IReadOnlyList<string> Keys => keys;
    public IReadOnlyList<string> DuplicateKeys => duplicateKeys;
    public IReadOnlyList<string> DuplicateLanguages => duplicateLanguages;

    public string Get(string lang, string key)
    {
        if (lang == null || key == null) return null;
        return cells.TryGetValue(key, out var row) && row.TryGetValue(lang, out var v) && v.Length > 0 ? v : null;
    }

    public static LocTable Parse(string csv)
    {
        var t = new LocTable();
        if (string.IsNullOrEmpty(csv)) return t;
        if (csv[0] == '\uFEFF') csv = csv.Substring(1);
        var rows = Split(csv, DetectDelimiter(csv));
        if (rows.Count == 0) return t;

        var header = rows[0];
        var langCols = new List<int>();
        for (int c = 1; c < header.Count; c++)
        {
            var name = header[c].Trim().ToLowerInvariant();
            if (name.Length == 0 || name == "note") continue;
            if (t.languages.Contains(name))
            {
                if (!t.duplicateLanguages.Contains(name)) t.duplicateLanguages.Add(name);
                continue; // ilk sütun geçerli
            }
            t.languages.Add(name);
            langCols.Add(c);
        }

        for (int r = 1; r < rows.Count; r++)
        {
            var row = rows[r];
            var key = row[0].Trim();
            if (key.Length == 0) continue;
            if (t.cells.ContainsKey(key))
            {
                if (!t.duplicateKeys.Contains(key)) t.duplicateKeys.Add(key);
                continue;
            }
            var values = new Dictionary<string, string>();
            for (int i = 0; i < langCols.Count; i++)
                if (langCols[i] < row.Count) values[t.languages[i]] = row[langCols[i]];
            t.keys.Add(key);
            t.cells[key] = values;
        }
        return t;
    }

    // Başlık satırında tırnak dışında en çok geçen ayırıcı.
    static char DetectDelimiter(string s)
    {
        int comma = 0, semi = 0, tab = 0;
        bool q = false;
        for (int i = 0; i < s.Length; i++)
        {
            char ch = s[i];
            if (ch == '"') q = !q;
            else if (!q && (ch == '\n' || ch == '\r')) break;
            else if (!q) { if (ch == ',') comma++; else if (ch == ';') semi++; else if (ch == '\t') tab++; }
        }
        return semi > comma && semi >= tab ? ';' : tab > comma && tab > semi ? '\t' : ',';
    }

    // Boş ve yorum (#) satırları atlanır; yorum yalnızca tırnak dışı satır başında.
    static List<List<string>> Split(string s, char delim)
    {
        var rows = new List<List<string>>();
        var row = new List<string>();
        var cell = new StringBuilder();
        bool q = false, atStart = true;
        for (int i = 0; i < s.Length; i++)
        {
            char ch = s[i];
            if (q)
            {
                if (ch != '"') cell.Append(ch);
                else if (i + 1 < s.Length && s[i + 1] == '"') { cell.Append('"'); i++; }
                else q = false;
                continue;
            }
            if (atStart && ch == '#')
            {
                while (i + 1 < s.Length && s[i + 1] != '\n' && s[i + 1] != '\r') i++;
                continue;
            }
            atStart = false;
            if (ch == '"') q = true;
            else if (ch == delim) { row.Add(cell.ToString()); cell.Clear(); }
            else if (ch == '\n' || ch == '\r')
            {
                if (ch == '\r' && i + 1 < s.Length && s[i + 1] == '\n') i++;
                EndRow(rows, row, cell);
                row = new List<string>();
                atStart = true;
            }
            else cell.Append(ch);
        }
        EndRow(rows, row, cell);
        return rows;
    }

    static void EndRow(List<List<string>> rows, List<string> row, StringBuilder cell)
    {
        row.Add(cell.ToString());
        cell.Clear();
        if (row.Count > 1 || row[0].Length > 0) rows.Add(row);
    }
}
