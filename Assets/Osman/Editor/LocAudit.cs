using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using TMPro;
using UnityEditor;
using UnityEngine;

// Dil tablosu denetimi: tablo kuralları, kullanılan anahtarlar, kalan Türkçe metinler.
public static class LocAudit
{
    public sealed class Report
    {
        public List<string> Errors = new List<string>();
        public List<string> Warnings = new List<string>();
        public List<string> LeftoverLiterals = new List<string>();
        public List<string> MissingGlyphs = new List<string>();
    }

    const string Sections = "common|login|lobby|room|settings|profile|account|shop|table|penalty|toast|error|lang";
    static readonly Regex KeyFormat = new Regex("^(?:" + Sections + @")\.[a-z0-9_]+$");
    static readonly Regex UsedKey = new Regex("\"((?:" + Sections + ")\\.[a-z0-9_]+)\"");
    static readonly Regex Placeholder = new Regex(@"\{\d+\}");
    static readonly Regex TurkishLiteral = new Regex("\"[^\"]*[çğıöşüÇĞİÖŞÜ][^\"]*\"");

    // Saf: dosya ya da Unity erişimi yok.
    public static Report Check(string csv, IEnumerable<(string path, string text)> sources)
    {
        var r = new Report();
        var t = LocTable.Parse(csv);
        foreach (var d in t.DuplicateKeys) r.Errors.Add($"Yinelenen anahtar: {d}");

        var used = new HashSet<string>();
        foreach (var s in sources)
            foreach (Match m in UsedKey.Matches(s.text ?? "")) used.Add(m.Groups[1].Value);

        foreach (var key in t.Keys)
        {
            if (!KeyFormat.IsMatch(key)) r.Errors.Add($"Anahtar biçimi bozuk: {key}");
            foreach (var lang in new[] { "tr", "en" })
                if (t.Get(lang, key) == null) r.Errors.Add($"{key}: '{lang}' boş");

            var trSet = PlaceholderSet(t.Get("tr", key));
            foreach (var lang in t.Languages)
            {
                var v = t.Get(lang, key);
                if (v == null)
                {
                    if (lang != "tr" && lang != "en") r.Warnings.Add($"{key}: '{lang}' boş");
                    continue;
                }
                if (v.Contains("\uFFFD")) r.Errors.Add($"{key}: '{lang}' içinde U+FFFD var");
                if (lang != "tr" && trSet != null && PlaceholderSet(v) != trSet)
                    r.Errors.Add($"{key}: '{lang}' {{n}} kümesi tr'den farklı");
            }
            if (!used.Contains(key) && key != "lang.name") r.Warnings.Add($"Kullanılmayan anahtar: {key}");
        }

        foreach (var u in used.OrderBy(x => x))
            if (!t.Keys.Contains(u)) r.Errors.Add($"Kullanılan anahtar tabloda yok: {u}");
        return r;
    }

    static string PlaceholderSet(string v) =>
        v == null ? null : string.Join(",", Placeholder.Matches(v).Cast<Match>().Select(m => m.Value).Distinct().OrderBy(x => x));

    public static Report CheckProject()
    {
        string root = Application.dataPath.Replace('\\', '/');
        string csv = File.ReadAllText(root + "/Resources/Localization.csv");
        var files = Directory.GetFiles(root + "/Osman", "*.cs", SearchOption.AllDirectories)
            .Select(f => "Assets" + f.Replace('\\', '/').Substring(root.Length))
            .Where(p => !p.StartsWith("Assets/Osman/Editor/Tests/")).ToList();
        var r = Check(csv, files.Select(p => (p, File.ReadAllText(root + p.Substring("Assets".Length)))));
        foreach (var p in files.Where(IsMigratable))
        {
            var lines = File.ReadAllLines(root + p.Substring("Assets".Length));
            for (int i = 0; i < lines.Length; i++)
            {
                var l = lines[i].Trim();
                if (l.StartsWith("//") || l.Contains("Debug.Log") || l.Contains("throw") || l.StartsWith("[MenuItem")) continue;
                if (TurkishLiteral.IsMatch(l)) r.LeftoverLiterals.Add($"{p}:{i + 1}: {l}");
            }
        }
        return r;
    }

    // Çevrilmeyen dosyalar (test, otomatik test, denetçi) ve Dil altyapısı hariç.
    static bool IsMigratable(string p)
    {
        if (p.StartsWith("Assets/Osman/Scripts/GamePlay/AutoTest/") || p.StartsWith("Assets/Osman/Scripts/Localization/")) return false;
        string f = Path.GetFileName(p);
        if (f.StartsWith("SyncAutoTest") || f == "DesyncDetector.cs") return false;
        return p.StartsWith("Assets/Osman/Scripts/") || (p.StartsWith("Assets/Osman/Editor/UiKit") && f.StartsWith("UiKit"));
    }

    [MenuItem("Okey/Dil/Tabloyu Denetle")]
    static void Menu()
    {
        var r = CheckProject();
        var t = LocTable.Parse(File.ReadAllText(Application.dataPath + "/Resources/Localization.csv"));
        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/Changa-Bold SDF.asset");
        if (font != null)
            foreach (var key in t.Keys)
                foreach (var lang in t.Languages)
                {
                    var v = t.Get(lang, key);
                    if (v != null && !font.HasCharacters(v, out List<char> missing))
                        r.MissingGlyphs.Add($"{key} ({lang}): {new string(missing.Distinct().ToArray())}");
                }
        foreach (var e in r.Errors) Debug.LogError("[Dil] " + e);
        foreach (var w in r.Warnings) Debug.LogWarning("[Dil] " + w);
        foreach (var l in r.LeftoverLiterals) Debug.Log("[Dil] Kalan metin: " + l);
        foreach (var g in r.MissingGlyphs) Debug.LogWarning("[Dil] Eksik harf: " + g);
        Debug.Log($"[Dil] Denetim: {r.Errors.Count} hata, {r.Warnings.Count} uyarı, {r.LeftoverLiterals.Count} kalan metin, {r.MissingGlyphs.Count} eksik harf");
    }
}
