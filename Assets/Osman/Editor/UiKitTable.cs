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

    static void BindOpenBadges()
    {
        var ui = Object.FindFirstObjectByType<UIManager>(FindObjectsInactive.Include);
        if (ui == null || ui.turnIndicators == null || ui.turnIndicators.Length < 4) { Debug.LogError("[UiKitBuilder] UIManager.turnIndicators eksik, açış etiketleri eklenemedi."); return; }
        var labels = new TMP_Text[4];
        for (int i = 0; i < 4; i++)
        {
            var plate = ui.turnIndicators[i].transform.parent;
            TMP_Text name = null;
            foreach (var t in plate.GetComponentsInChildren<TMP_Text>(true)) if (t.gameObject.name != "OpenBadge") { name = t; break; }
            if (name == null) { Debug.LogError("[UiKitBuilder] İsimlikte ad yazısı yok: " + plate.name); return; }
            var existing = plate.Find("OpenBadge");
            if (existing != null) { labels[i] = existing.GetComponent<TMP_Text>(); continue; }
            var go = new GameObject("OpenBadge", typeof(RectTransform));
            go.transform.SetParent(plate, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(1f, 0.5f);
            rt.pivot = new Vector2(1f, 0.5f);
            rt.anchoredPosition = new Vector2(-10f, 5.7f);
            rt.sizeDelta = new Vector2(130f, 26f);
            var tx = go.AddComponent<TextMeshProUGUI>();
            float max = name.enableAutoSizing ? name.fontSizeMax : name.fontSize;
            tx.font = name.font;
            tx.fontSharedMaterial = name.fontSharedMaterial;
            tx.fontStyle = name.fontStyle;
            tx.color = name.color;
            tx.alignment = TextAlignmentOptions.MidlineRight;
            tx.enableAutoSizing = true;
            tx.fontSizeMax = max;
            tx.fontSizeMin = Mathf.Round(max * 0.6f);
            tx.fontSize = max;
            tx.textWrappingMode = TextWrappingModes.NoWrap;
            tx.overflowMode = TextOverflowModes.Ellipsis;
            tx.raycastTarget = false;
            tx.text = "";
            labels[i] = tx;
        }
        var host = ui.turnIndicators[0].transform.parent.parent.gameObject;
        var badges = host.GetComponent<OpenBadges>() ?? host.AddComponent<OpenBadges>();
        bool same = badges.labels != null && badges.labels.Length == 4;
        for (int i = 0; same && i < 4; i++) same = badges.labels[i] == labels[i];
        if (!same) { badges.labels = labels; EditorUtility.SetDirty(badges); }
    }

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

        // Çıkış ve TABLO düğmeleri tıklama sesi alır; eylem düğmeleri almaz (sesleri sonuçtan gelir)
        foreach (var btnName in new[] { "Çıkış Butonu", "CurrentScoreButton" })
        {
            UnityEngine.UI.Button found = null;
            foreach (var b in Object.FindObjectsByType<UnityEngine.UI.Button>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (b.gameObject.name == btnName) { found = b; break; }
            if (found == null) { Debug.LogError("[UiKitBuilder] Masada düğme bulunamadı: " + btnName); continue; }
            Sound(found.gameObject, SfxId.UiClick);
        }

        // Skor geçmişi: GameManager nesnesinde bir kez (ikinci çalıştırmada çoğalmaz)
        var gm = Object.FindFirstObjectByType<GameManager>(FindObjectsInactive.Include);
        if (gm == null) Debug.LogError("[UiKitBuilder] Masada GameManager yok, MatchScores eklenemedi.");
        else if (Object.FindFirstObjectByType<MatchScores>(FindObjectsInactive.Include) == null) gm.gameObject.AddComponent<MatchScores>();

        BindOpenBadges();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[UiKitBuilder] Masa metinleri bağlandı: " + TablePath);
        if (!string.IsNullOrEmpty(previous) && previous != TablePath) EditorSceneManager.OpenScene(previous, OpenSceneMode.Single);
    }
}
