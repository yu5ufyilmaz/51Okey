using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

// Dil tablosuna erişim: seçili dil, çeviri, biçim. Tablo ilk kullanımda Resources/Localization'dan yüklenir.
public static class Loc
{
    public const string Fallback = "tr";
    public static event Action Changed;

    static LocTable table;
    static string language = "";
    static CultureInfo culture = CultureInfo.InvariantCulture;
    static readonly HashSet<string> warned = new HashSet<string>();

    public static IReadOnlyList<string> Languages => Table.Languages;

    public static string Language
    {
        get { _ = Table; return language; }
        set
        {
            if (value == null || !Has(Table.Languages, value) || value == language) return;
            language = value;
            culture = MakeCulture(value);
            Changed?.Invoke();
        }
    }

    public static CultureInfo Culture { get { _ = Table; return culture; } }

    static LocTable Table
    {
        get
        {
            if (table == null)
            {
                var asset = Resources.Load<TextAsset>("Localization");
                Set(LocTable.Parse(asset != null ? asset.text : null), GameSettings.Language);
            }
            return table;
        }
    }

    static void Set(LocTable t, string saved)
    {
        table = t;
        language = Pick(saved, CodeOf(Application.systemLanguage), t.Languages);
        culture = MakeCulture(language);
    }

    static CultureInfo MakeCulture(string code)
    {
        try { return CultureInfo.GetCultureInfo(code); }
        catch (Exception) { return CultureInfo.InvariantCulture; }
    }

    // csv ve language null ise tablo unutulur, sonraki kullanımda yeniden yüklenir.
    public static void Load(string csv, string lang)
    {
        warned.Clear();
        if (csv == null && lang == null) { table = null; return; }
        Set(LocTable.Parse(csv), lang);
    }

    public static string T(string key)
    {
        var t = Table;
        var s = t.Get(language, key) ?? t.Get(Fallback, key);
        if (s != null) return s;
        Warn(key, "Loc: eksik anahtar " + key);
        return key;
    }

    public static string T(string key, params object[] args)
    {
        var s = T(key);
        try { return string.Format(culture, s, args); }
        catch (FormatException) { Warn(key, "Loc: biçim hatası " + key); return s; }
    }

    public static string NameOf(string lang) => Table.Get(lang, "lang.name") ?? lang;

    public static string Pick(string saved, string systemCode, IReadOnlyList<string> available)
    {
        foreach (var c in new[] { saved, systemCode, "en", Fallback })
            if (!string.IsNullOrEmpty(c) && Has(available, c)) return c;
        return available.Count > 0 ? available[0] : "";
    }

    static bool Has(IReadOnlyList<string> list, string c) { foreach (var l in list) if (l == c) return true; return false; }

    public static string CodeOf(SystemLanguage lang)
    {
        switch (lang)
        {
            case SystemLanguage.Turkish: return "tr";
            case SystemLanguage.English: return "en";
            case SystemLanguage.German: return "de";
            case SystemLanguage.French: return "fr";
            case SystemLanguage.Spanish: return "es";
            case SystemLanguage.Italian: return "it";
            case SystemLanguage.Portuguese: return "pt";
            case SystemLanguage.Russian: return "ru";
            case SystemLanguage.Arabic: return "ar";
            default: return "";
        }
    }

    [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
    static void Warn(string key, string msg) { if (warned.Add(key)) Debug.LogWarning(msg); }
}
