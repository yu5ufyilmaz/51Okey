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
    static readonly Regex UsedKey = new Regex("\"((?:" + Sections + ")\\.(?!(?:cs|json|csv|txt|png|jpg|prefab|asset|unity|meta|log|xml|dat)\")[a-z0-9_]+)\"");
    static readonly Regex LangHeader = new Regex("^[a-z]{2,3}(-[a-z0-9]+)?$");
    // Excel ANSI kaydında ş ı ğ İ Ş Ğ harfleri "?" olur: harfe yapışık soru işareti
    static readonly Regex QuestionDamage = new Regex(@"\p{L}\?\p{L}|(^|\s)\?\p{L}");
    static readonly Regex AssetKeyLine = new Regex(@"(?m)^\s+key: (\S+)\s*$");
    static readonly Regex AssetKeyOverride = new Regex(@"propertyPath: key\r?\n\s+value: (\S+)");
    static readonly Regex Placeholder = new Regex(@"\{(\d+)(?::[^}]*)?\}");
    static readonly Regex TurkishLiteral = new Regex("\"[^\"]*[çğıöşüÇĞİÖŞÜ][^\"]*\"");

    // Saf: dosya ya da Unity erişimi yok.
    // assets: sahne ve prefab metinleri; LocText anahtarları buradan okunur.
    public static Report Check(string csv, IEnumerable<(string path, string text)> sources, IEnumerable<(string path, string text)> assets = null)
    {
        var r = new Report();
        var t = LocTable.Parse(csv);
        foreach (var d in t.DuplicateKeys) r.Errors.Add($"Yinelenen anahtar: {d}");
        foreach (var d in t.DuplicateLanguages) r.Errors.Add($"Yinelenen dil sütunu: {d}");
        foreach (var l in t.Languages)
            if (!LangHeader.IsMatch(l)) r.Errors.Add($"Dil sütunu başlığı bozuk: {l}");

        var used = new HashSet<string>();
        foreach (var s in sources)
            foreach (Match m in UsedKey.Matches(s.text ?? "")) used.Add(m.Groups[1].Value);

        foreach (var a in assets ?? new (string, string)[0])
            foreach (var m in new[] { AssetKeyLine, AssetKeyOverride })
                foreach (Match x in m.Matches(a.text ?? "")) used.Add(x.Groups[1].Value);

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
                if (QuestionDamage.IsMatch(v)) r.Errors.Add($"{key}: '{lang}' harfe yapışık '?' var (kodlama bozulması)");
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
        v == null ? null : string.Join(",", Placeholder.Matches(v).Cast<Match>().Select(m => m.Groups[1].Value).Distinct().OrderBy(x => x));

    public static Report CheckProject()
    {
        string root = Application.dataPath.Replace('\\', '/');
        string csv = File.ReadAllText(root + "/Resources/Localization.csv");
        var files = Directory.GetFiles(root + "/Osman", "*.cs", SearchOption.AllDirectories)
            .Select(f => "Assets" + f.Replace('\\', '/').Substring(root.Length))
            .Where(p => !p.StartsWith("Assets/Osman/Editor/Tests/")).ToList();
        var assets = new List<string>();
        assets.AddRange(Directory.GetFiles(root + "/Osman", "*.unity", SearchOption.AllDirectories));
        assets.AddRange(Directory.GetFiles(root + "/Osman", "*.prefab", SearchOption.AllDirectories));
        assets.AddRange(Directory.GetFiles(root + "/Resources", "*.prefab", SearchOption.TopDirectoryOnly));
        var r = Check(csv, files.Select(p => (p, File.ReadAllText(root + p.Substring("Assets".Length)))),
            assets.Select(f => (f, File.ReadAllText(f))));
        foreach (var p in files.Where(IsMigratable))
        {
            r.LeftoverLiterals.AddRange(LeftoverIn(p, File.ReadAllText(root + p.Substring("Assets".Length))));
        }
        return r;
    }

    // Log/throw ifadeleri `;` satırına kadar (çok satırlı olanlar dahil) atlanır.
    internal static List<string> LeftoverIn(string path, string text)
    {
        var found = new List<string>();
        var lines = text.Split((char)10);
        bool skipping = false;
        for (int i = 0; i < lines.Length; i++)
        {
            var l = lines[i].Trim();
            if (l.StartsWith("//") || l.StartsWith("[MenuItem")) continue;
            if (l.Contains("Debug.Log") || l.Contains("throw")) skipping = true;
            if (skipping) { if (l.Contains(";")) skipping = false; continue; }
            if (TurkishLiteral.IsMatch(l)) found.Add($"{path}:{i + 1}: {l}");
        }
        return found;
    }

    // Kaynak fontta olmayan harfler; <...> etiketleri, {n} ve boşluk/kontrol karakterleri sayılmaz.
    internal static string MissingChars(TMP_FontAsset font, string v)
    {
        v = Regex.Replace(Regex.Replace(v, "<[^>]*>", ""), @"\{\d+\}", "");
        var chars = v.Distinct().Where(c => !char.IsWhiteSpace(c) && !char.IsControl(c));
        var src = font.sourceFontFile;
        // Font.HasCharacter her harf için true döner; gerçek yüze FontEngine ile bakılır.
        if (src != null && UnityEngine.TextCore.LowLevel.FontEngine.LoadFontFace(src, 90) == UnityEngine.TextCore.LowLevel.FontEngineError.Success)
            return new string(chars.Where(c => !UnityEngine.TextCore.LowLevel.FontEngine.TryGetGlyphIndex(c, out _)).ToArray());
        return font.HasCharacters(v, out List<char> missing) ? "" : new string(missing.Distinct().ToArray());
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
                    var miss = v == null ? "" : MissingChars(font, v);
                    if (miss.Length > 0) r.MissingGlyphs.Add($"{key} ({lang}): {miss}");
                }
        foreach (var e in r.Errors) Debug.LogError("[Dil] " + e);
        foreach (var w in r.Warnings) Debug.LogWarning("[Dil] " + w);
        foreach (var l in r.LeftoverLiterals) Debug.Log("[Dil] Kalan metin: " + l);
        foreach (var g in r.MissingGlyphs) Debug.LogWarning("[Dil] Eksik harf: " + g);
        Debug.Log($"[Dil] Denetim: {r.Errors.Count} hata, {r.Warnings.Count} uyarı, {r.LeftoverLiterals.Count} kalan metin, {r.MissingGlyphs.Count} eksik harf");
    }
}
