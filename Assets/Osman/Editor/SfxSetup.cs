using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

public static class SfxSetup
{
    const string AssetPath = "Assets/Resources/SfxLibrary.asset";
    const string AudioDir = "Assets/Osman/Audio/Sfx/";

    public static readonly (SfxId id, float volume, float minInterval, float pitchJitter, string[] files)[] Defaults =
    {
        (SfxId.TilePickUp, 0.6f, 0.05f, 0.06f, new[] { "tile_pickup_1.ogg", "tile_pickup_2.ogg" }),
        (SfxId.TileDrop, 0.7f, 0.05f, 0.06f, new[] { "tile_drop_1.ogg", "tile_drop_3.ogg" }),
        (SfxId.TileSnapBack, 0.15f, 0.05f, 0.06f, new[] { "tile_snap_back.ogg" }),
        (SfxId.TileDraw, 0.95f, 0.05f, 0.06f, new[] { "tile_draw_1.ogg", "tile_draw_2.ogg" }),
        (SfxId.TileTakeSide, 0.95f, 0.05f, 0.06f, new[] { "tile_take_side.ogg" }),
        (SfxId.TileDiscard, 1f, 0.05f, 0.06f, new[] { "tile_discard_1.ogg", "tile_discard_2.ogg", "tile_discard_3.ogg" }),
        (SfxId.TilesDeal, 1f, 0.5f, 0f, new[] { "tiles_deal_1.ogg", "tiles_deal_2.ogg" }),
        (SfxId.OpenRuns, 0.7f, 0.3f, 0f, new[] { "open_runs.ogg" }),
        (SfxId.OpenPairs, 0.35f, 0.3f, 0f, new[] { "open_pairs.ogg" }),
        (SfxId.MeldShown, 1f, 0.3f, 0f, new[] { "meld_shown.ogg" }),
        (SfxId.TileProcess, 0.6f, 0.3f, 0.06f, new[] { "tile_process_1.ogg", "tile_process_2.ogg" }),
        (SfxId.TilesCollect, 1f, 0.3f, 0f, new[] { "tiles_collect.ogg" }),
        (SfxId.PutBack, 1f, 0.05f, 0.06f, new[] { "put_back.ogg" }),
        (SfxId.Error, 0.6f, 0.2f, 0f, new[] { "error_1.ogg" }),
        (SfxId.PenaltyTaken, 0.3f, 0.3f, 0f, new[] { "penalty_taken.ogg" }),
        (SfxId.PenaltyGiven, 0.5f, 0.3f, 0f, new[] { "penalty_given.ogg" }),
        (SfxId.MyTurn, 0.85f, 0.5f, 0f, new[] { "my_turn.ogg" }),
        (SfxId.TimeLow, 0.45f, 1f, 0f, new[] { "time_low.ogg" }),
        (SfxId.Win, 0.3f, 1f, 0f, new[] { "win.ogg" }),
        (SfxId.Lose, 0.3f, 1f, 0f, new[] { "lose.ogg" }),
        (SfxId.SeatTaken, 0.65f, 0.3f, 0f, new[] { "seat_taken.ogg" }),
        (SfxId.UiClick, 0.15f, 0.05f, 0f, new[] { "ui_click.ogg" }),
        (SfxId.UiClose, 0.15f, 0.05f, 0f, new[] { "ui_close.ogg" }),
        (SfxId.UiToggle, 0.15f, 0.05f, 0f, new[] { "ui_toggle.ogg" }),
        (SfxId.UiPlayNow, 0.2f, 0.2f, 0f, new[] { "ui_play_now.ogg" }),
        (SfxId.UiCreateTable, 0.15f, 0.2f, 0f, new[] { "ui_create_table.ogg" }),
        (SfxId.UiPlayBots, 0.3f, 0.2f, 0f, new[] { "ui_play_bots.ogg" }),
        (SfxId.UiEnterRoom, 0.15f, 0.2f, 0f, new[] { "ui_enter_room.ogg" }),
    };

    // Uygun ses bulunamayan olaylar (şu an hepsinin sesi var).
    public static readonly SfxId[] SilentOnPurpose = new SfxId[0];

    public sealed class Report { public List<string> NoClip = new List<string>(), UnusedIds = new List<string>(), OrphanFiles = new List<string>(); }

    public static Report Check()
    {
        var r = new Report();
        var lib = AssetDatabase.LoadAssetAtPath<SfxLibrary>(AssetPath);
        foreach (var e in lib.entries)
            if ((e.clips == null || e.clips.All(c => c == null)) && !SilentOnPurpose.Contains(e.id)) r.NoClip.Add(e.id.ToString());
        var text = string.Concat(new[] { "Assets/Osman/Scripts", "Assets/Osman/Editor" }
            .SelectMany(d => Directory.GetFiles(d, "*.cs", SearchOption.AllDirectories))
            .Select(f => f.Replace('\\', '/'))
            .Where(f => !f.EndsWith("/Audio/SfxId.cs") && !f.EndsWith("/Editor/SfxSetup.cs") && !f.Contains("/Editor/Tests/"))
            .Select(File.ReadAllText));
        foreach (SfxId id in System.Enum.GetValues(typeof(SfxId)))
            if (!Regex.IsMatch(text, @"\bSfxId\." + id + @"\b")) r.UnusedIds.Add(id.ToString());
        var used = new HashSet<string>(lib.entries.Where(e => e.clips != null).SelectMany(e => e.clips).Where(c => c != null).Select(AssetDatabase.GetAssetPath));
        foreach (var g in AssetDatabase.FindAssets("t:AudioClip", new[] { AudioDir.TrimEnd('/') }))
        {
            var path = AssetDatabase.GUIDToAssetPath(g);
            if (!used.Contains(path)) r.OrphanFiles.Add(path);
        }
        return r;
    }

    [MenuItem("Okey/Ses/Ses Listesini Denetle")]
    static void CheckMenu()
    {
        var r = Check();
        Debug.Log("Sfx denetim\nSessiz (klipsiz): " + string.Join(", ", r.NoClip) + "\nKullanılmayan id: " + string.Join(", ", r.UnusedIds) + "\nSahipsiz dosya: " + string.Join(", ", r.OrphanFiles));
        Debug.Log("Sfx denetim özeti: klipsiz=" + r.NoClip.Count + " kullanılmayan=" + r.UnusedIds.Count + " sahipsiz=" + r.OrphanFiles.Count);
    }

    // Win/Lose/TilesDeal uzun: bellekte sıkışık kalır, diğerleri açık yüklenir.
    static bool IsLong(string file) => Defaults.Any(d => (d.id == SfxId.Win || d.id == SfxId.Lose || d.id == SfxId.TilesDeal) && d.files.Contains(file));

    static IEnumerable<string> AudioPaths() =>
        AssetDatabase.FindAssets("t:AudioClip", new[] { AudioDir.TrimEnd('/') }).Select(AssetDatabase.GUIDToAssetPath).OrderBy(p => p);

    // Değişen yoksa yeniden içe aktarmaz.
    static void ApplyImportSettings()
    {
        foreach (var path in AudioPaths())
        {
            var imp = (AudioImporter)AssetImporter.GetAtPath(path);
            var s = imp.defaultSampleSettings;
            var load = IsLong(Path.GetFileName(path)) ? AudioClipLoadType.CompressedInMemory : AudioClipLoadType.DecompressOnLoad;
            if (imp.forceToMono && s.preloadAudioData && s.loadType == load && s.compressionFormat == AudioCompressionFormat.Vorbis && Mathf.Approximately(s.quality, 0.7f)) continue;
            s.loadType = load; s.compressionFormat = AudioCompressionFormat.Vorbis; s.quality = 0.7f;
            s.preloadAudioData = true; imp.forceToMono = true; imp.defaultSampleSettings = s;
            imp.SaveAndReimport();
        }
    }

    // dosya | süre | tepe dBFS | RMS dBFS | güncel ses düzeyi x RMS; RMS -60 dBFS üstü örneklerden.
    [MenuItem("Okey/Ses/Ses Düzeylerini Ölç")]
    static void MeasureMenu() => Debug.Log("Sfx ölçüm\ndosya | süre | tepe dBFS | RMS dBFS | düzey x RMS dB\n" + MeasureAll());

    static string MeasureAll()
    {
        var lib = AssetDatabase.LoadAssetAtPath<SfxLibrary>(AssetPath);
        var lines = new List<string>();
        foreach (var path in AudioPaths())
        {
            var imp = (AudioImporter)AssetImporter.GetAtPath(path);
            var orig = imp.defaultSampleSettings;
            bool swap = orig.loadType != AudioClipLoadType.DecompressOnLoad; // GetData için geçici
            try
            {
                if (swap) { var t = orig; t.loadType = AudioClipLoadType.DecompressOnLoad; imp.defaultSampleSettings = t; imp.SaveAndReimport(); }
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                var data = new float[clip.samples * clip.channels];
                clip.GetData(data, 0);
                double peak = 0, sum = 0; int n = 0;
                foreach (var v in data)
                {
                    var a = Mathf.Abs(v);
                    if (a > peak) peak = a;
                    if (a > 0.001f) { sum += (double)a * a; n++; }
                }
                double rms = n > 0 ? System.Math.Sqrt(sum / n) : 0;
                var entry = lib != null ? lib.entries.FirstOrDefault(e => e.clips != null && e.clips.Contains(clip)) : null;
                string vol = entry != null ? (20 * System.Math.Log10(rms * entry.volume)).ToString("0.0") : "-";
                lines.Add(Path.GetFileName(path) + " | " + clip.length.ToString("0.00") + " s | " + (20 * System.Math.Log10(peak)).ToString("0.0") + " | " + (20 * System.Math.Log10(rms)).ToString("0.0") + " | " + vol);
            }
            finally
            {
                if (swap) { imp.defaultSampleSettings = orig; imp.SaveAndReimport(); }
            }
        }
        return string.Join("\n", lines);
    }

    // Satırı olmayan id'lere Defaults'tan satır ekler; var olan satıra (boş bile olsa) dokunmaz.
    internal static void AddMissingRows(SfxLibrary lib)
    {
        foreach (var d in Defaults)
        {
            if (lib.Find(d.id) != null) continue;
            lib.entries.Add(new SfxLibrary.Entry
            {
                id = d.id,
                clips = d.files.Select(f => AssetDatabase.LoadAssetAtPath<AudioClip>(AudioDir + f)).Where(c => c != null).ToArray(),
                volume = d.volume, minInterval = d.minInterval, pitchJitter = d.pitchJitter,
            });
        }
    }

    // Var olan satırlara dokunmaz; yalnız eksik id'leri ekler.
    [MenuItem("Okey/Ses/Ses Listesini Kur")]
    public static void Build()
    {
        ApplyImportSettings();
        var lib = AssetDatabase.LoadAssetAtPath<SfxLibrary>(AssetPath);
        if (lib == null)
        {
            if (File.Exists(AssetPath)) { Debug.LogError("Sfx: " + AssetPath + " var ama yüklenemedi; üzerine yazılmadı."); return; }
            if (!AssetDatabase.IsValidFolder("Assets/Resources")) AssetDatabase.CreateFolder("Assets", "Resources");
            lib = ScriptableObject.CreateInstance<SfxLibrary>();
            AssetDatabase.CreateAsset(lib, AssetPath);
        }
        AddMissingRows(lib);
        EditorUtility.SetDirty(lib);
        AssetDatabase.SaveAssets();
    }
}
