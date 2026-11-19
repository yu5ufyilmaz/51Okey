using System.Collections.Generic;
using System.Text.RegularExpressions;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// UI kiti: masa sahnesi (Table). Sahne elle yapılmış; sabit etiketler bugünkü metinlerinden bulunup LocText alır.
public static partial class UiKitBuilder
{
    const string TablePath = "Assets/Osman/Scenes/Table.unity";

    // anahtar, sahnedeki eski metin, beklenen adet
    static readonly (string key, string old, int count)[] TableLabels =
    {
        ("table.play_again", "TEKRAR OYNA", 1),
        ("table.collect_tiles", "TASLARI TOPLA", 1),
        ("table.add_tile", "TAS ISLE", 1),
        ("table.scoreboard", "TABLO", 1),
        ("table.put_back", "GERI BIRAK", 1),
        ("table.exit", "Çıkıs", 1),
        ("table.open_pairs", "ÇIFT AÇ", 1),
        ("table.open_runs", "SERI AÇ", 1),
    };

    static string Norm(string s) => Regex.Replace(s ?? "", @"\s+", " ").Trim();

    [MenuItem("Okey/UI Kit/Masa Metinlerini Bağla")]
    public static void BindTableTexts()
    {
        if (EditorApplication.isPlaying) { Debug.LogWarning("[UiKitBuilder] Play modunda masa metinleri bağlanmaz."); return; }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        string previous = EditorSceneManager.GetActiveScene().path;
        var scene = EditorSceneManager.OpenScene(TablePath, OpenSceneMode.Single);
        var csv = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Resources/Localization.csv");
        var table = LocTable.Parse(csv != null ? csv.text : null);

        // Koltuk etiketlerini çalışma zamanında SeatManager yazar (isim, OTUR); LocText almaz.
        var seatFields = new HashSet<TMP_Text>();
        foreach (var sm in Object.FindObjectsByType<SeatManager>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (sm.seatTextFields != null) foreach (var f in sm.seatTextFields) if (f != null) seatFields.Add(f);

        var all = Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var (key, old, count) in TableLabels)
        {
            string tr = table.Get("tr", key);
            if (tr == null) { Debug.LogError("[UiKitBuilder] Dil tablosunda anahtar yok: " + key); continue; }
            string a = Norm(old), b = Norm(tr);
            int found = 0;
            foreach (var t in all)
            {
                var lt = t.GetComponent<LocText>();
                bool match = lt != null && lt.key == key || Norm(t.text) == a || Norm(t.text) == b;
                if (!match) continue;
                found++;
                if (seatFields.Contains(t))
                {
                    if (lt != null) Object.DestroyImmediate(lt, true);
                    if (t.text != tr) { t.text = tr; EditorUtility.SetDirty(t); }
                    continue;
                }
                SetLabel(t, key);
                EditorUtility.SetDirty(t);
                if (PrefabUtility.IsPartOfPrefabInstance(t))
                {
                    PrefabUtility.RecordPrefabInstancePropertyModifications(t);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(t.GetComponent<LocText>());
                }
            }
            if (found < count) Debug.LogError($"[UiKitBuilder] Masada metin bulunamadı: \"{old}\" ({key}) bulunan {found}/{count}");
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[UiKitBuilder] Masa metinleri bağlandı: " + TablePath);
        if (!string.IsNullOrEmpty(previous) && previous != TablePath) EditorSceneManager.OpenScene(previous, OpenSceneMode.Single);
    }
}
