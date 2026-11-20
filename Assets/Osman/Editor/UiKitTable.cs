using System.Collections.Generic;
using System.Text.RegularExpressions;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

// UI kiti: masa sahnesi (Table). Sahne elle yapılmış; sabit etiketler bugünkü metinlerinden bulunup LocText alır.
public static partial class UiKitBuilder
{
    const string TablePath = "Assets/Osman/Scenes/Table.unity";

    // anahtar, sahnedeki eski metin, beklenen adet
    static readonly (string key, string old, int count)[] TableLabels =
    {
        ("table.collect_tiles", "TASLARI TOPLA", 1),
        ("table.add_tile", "TAS ISLE", 1),
        ("table.scoreboard", "TABLO", 1),
        ("table.put_back", "GERI BIRAK", 1),
        ("table.exit", "Çıkıs", 1),
        ("table.open_pairs", "ÇIFT AÇ", 1),
        ("table.open_runs", "SERI AÇ", 1),
    };

    static string Norm(string s) => Regex.Replace(s ?? "", @"\s+", " ").Trim();

    // Sahnede yeni kurulan nesne üst nesnesinin katmanını (UI) alır
    static void SameLayer(GameObject g, Transform parent)
    {
        if (g.layer == parent.gameObject.layer) return;
        g.layer = parent.gameObject.layer;
        EditorUtility.SetDirty(g);
    }

    static void SameLayerAll(GameObject g, int layer)
    {
        foreach (var t in g.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = layer;
    }

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
            if (existing != null) { labels[i] = existing.GetComponent<TMP_Text>(); SameLayer(existing.gameObject, plate); continue; }
            var go = new GameObject("OpenBadge", typeof(RectTransform));
            go.transform.SetParent(plate, false);
            SameLayer(go, plate);
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
        var badges = host.GetComponent<OpenBadges>();
        if (badges == null) badges = host.AddComponent<OpenBadges>();
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

        var view = BindScoreboard();

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
            if (btnName == "CurrentScoreButton" && view != null) RebindScoreButton(found, view);
        }

        // Skor geçmişi: GameManager nesnesinde bir kez (ikinci çalıştırmada çoğalmaz)
        var gm = Object.FindFirstObjectByType<GameManager>(FindObjectsInactive.Include);
        if (gm == null) Debug.LogError("[UiKitBuilder] Masada GameManager yok, MatchScores eklenemedi.");
        else if (Object.FindFirstObjectByType<MatchScores>(FindObjectsInactive.Include) == null) gm.gameObject.AddComponent<MatchScores>();

        BindOpenBadges();
        BindSettings();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[UiKitBuilder] Masa metinleri bağlandı: " + TablePath);
        if (!string.IsNullOrEmpty(previous) && previous != TablePath) EditorSceneManager.OpenScene(previous, OpenSceneMode.Single);
    }

    // --- ayarlar (ses, müzik) ---

    const float BtnFont = 31f, ExitW = 100f, SettingsW = 150f;

    // Çıkış düğmesinin sağına Ayarlar düğmesi ve ortada küçük pencere; ikisi de varsa dokunulmaz
    static void BindSettings()
    {
        Transform safe = null;
        foreach (var sa in Object.FindObjectsByType<Crystal.SafeArea>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (sa.transform.parent != null && sa.transform.parent.GetComponent<Canvas>() != null) { safe = sa.transform; break; }
        UnityEngine.UI.Button exit = null;
        foreach (var b in Object.FindObjectsByType<UnityEngine.UI.Button>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (b.gameObject.name == "Çıkış Butonu") { exit = b; break; }
        if (safe == null || exit == null) { Debug.LogError("[UiKitBuilder] SafeArea ya da çıkış düğmesi yok, ayarlar kurulamadı."); return; }
        // Telefonda okunsun: iki etiket de ~31 birim (2400x1080'de ~22 px büyük harf yüksekliği). Çıkış daralır, Ayarlar genişler;
        // Ayarlar'ın sağ ucu 1920'de ~331'de kalır, iskambil yuvası ~345'te başlar.
        var er = (RectTransform)exit.transform;
        er.sizeDelta = new Vector2(ExitW, er.sizeDelta.y);
        AutoSize(exit.GetComponentInChildren<TMP_Text>(true), BtnFont, 26);

        var existingWin = safe.Find("SettingsWindow");
        var existingBtn = exit.transform.parent.Find("SettingsButton");
        if (existingWin != null && existingBtn != null && ((RectTransform)existingBtn).sizeDelta.x == SettingsW) return;
        if (existingWin != null) Object.DestroyImmediate(existingWin.gameObject);
        if (existingBtn != null) Object.DestroyImmediate(existingBtn.gameObject);

        bold = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(BoldPath);
        extraBold = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(ExtraBoldPath);
        var teal = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabDir + "Btn_Teal.prefab");
        var window = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabDir + "Window.prefab");
        var row = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabDir + "Row.prefab");
        var toggle = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabDir + "Toggle.prefab");
        if (bold == null || extraBold == null || teal == null || window == null || row == null || toggle == null || toggle.GetComponent<Toggle>() == null)
        { Debug.LogError("[UiKitBuilder] Kit varlıkları eksik, önce Prefabları Kur çalıştırılmalı."); return; }

        var win = KitWindow(window, safe, "SettingsWindow", "settings.title", new Vector2(1000, 360));
        // SafeArea'nın boyu ekrandan küçük olabilir: karartma Fill ile değil, ortadan büyük bir kareyle ekranı örter
        var half = new Vector2(0.5f, 0.5f);
        Anchor(win, half, half, half, Vector2.zero, new Vector2(6000, 6000));
        var uw = win.GetComponent<UiWindow>();
        uw.closeOnBack = false;
        var ts = win.AddComponent<TableSettings>();
        UnityEventTools.AddPersistentListener(uw.onBack, ts.Close);
        var content = win.transform.Find("Body/Content").gameObject;
        Fill(content, 60, 40, -60, -150);
        var vl = content.AddComponent<VerticalLayoutGroup>();
        vl.childAlignment = TextAnchor.MiddleCenter;
        vl.childControlWidth = true;
        vl.childForceExpandWidth = true;
        vl.childControlHeight = vl.childForceExpandHeight = false;
        var audio = HalfRow(content.transform, "AudioRow");
        ts.soundToggle = ToggleRow(row, toggle, audio.transform, "SoundRow", "settings.sound");
        ts.musicToggle = ToggleRow(row, toggle, audio.transform, "MusicRow", "settings.music");
        UnityEventTools.AddPersistentListener(ts.soundToggle.onValueChanged, ts.OnSoundToggle);
        UnityEventTools.AddPersistentListener(ts.musicToggle.onValueChanged, ts.OnMusicToggle);
        EditorUtility.SetDirty(ts);

        // Çıkış düğmesiyle aynı kap, çapa ve yükseklik; sağında 8 birim boşlukla (daha sağda iskambil yuvası var, SettingsW'den geniş olamaz)
        var btn = ChoiceButton(teal, exit.transform.parent, "SettingsButton", "lobby.settings", SettingsW, er.sizeDelta.y, BtnFont);
        Anchor(btn, er.anchorMin, er.anchorMax, er.pivot, new Vector2(er.anchoredPosition.x + er.sizeDelta.x + 8f, er.anchoredPosition.y), new Vector2(SettingsW, er.sizeDelta.y));
        var label = btn.GetComponentInChildren<TMP_Text>(true);
        AutoSize(label, BtnFont, 26);
        var lr = (RectTransform)label.transform;
        lr.offsetMin = new Vector2(10, lr.offsetMin.y);
        lr.offsetMax = new Vector2(-10, lr.offsetMax.y);
        UnityEventTools.AddPersistentListener(btn.GetComponent<Button>().onClick, ts.Open);

        win.SetActive(false);
        foreach (var o in new[] { win, btn })
        {
            SameLayerAll(o, safe.gameObject.layer);
            foreach (var c in o.GetComponentsInChildren<Component>(true))
                if (PrefabUtility.IsPartOfPrefabInstance(c)) PrefabUtility.RecordPrefabInstancePropertyModifications(c);
        }
        // Skor tablosunun altında kalır: el sonu tablosu üste çıkar
        var sb = safe.Find("Scoreboard");
        win.transform.SetSiblingIndex(sb != null ? sb.GetSiblingIndex() : safe.childCount - 1);
    }

    // --- skor tablosu ---

    static readonly Color Mute = new Color(0.62f, 0.70f, 0.78f);
    const float RowH = 58f, HeadH = 52f, Pad = 50f;

    class RowBuild { public GameObject root; public Image bg; public TMP_Text[] cells; }

    // Eski paneller silinir; skor tablosu her çalışmada baştan kurulur
    static ScoreboardView BindScoreboard()
    {
        Transform safe = null;
        foreach (var sa in Object.FindObjectsByType<Crystal.SafeArea>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (sa.transform.parent != null && sa.transform.parent.GetComponent<Canvas>() != null) { safe = sa.transform; break; }
        if (safe == null) { Debug.LogError("[UiKitBuilder] Masada SafeArea bulunamadı, skor tablosu kurulamadı."); return null; }

        foreach (var n in new[] { "ScorePanel", "CurrentScorePanel" })
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (t != null && t.name == n && t.IsChildOf(safe)) Object.DestroyImmediate(t.gameObject);

        // Yeni sahne nesnelerine Unity her kayıtta rastgele fileID verir; sil-yeniden-kur her çalışmada dosyayı değiştirirdi.
        // Bu yüzden yalnız bu kaynak dosyası değiştiyse (imza) aynı sıra konumuna yeniden kurulur, yoksa dokunulmaz.
        string sig = SourceSignature();
        var existing = safe.Find("Scoreboard");
        int sibling = -1;
        if (existing != null)
        {
            var old = existing.GetComponent<ScoreboardView>();
            if (old != null && old.LayoutSignature == sig) return old;
            sibling = existing.GetSiblingIndex();
            Object.DestroyImmediate(existing.gameObject);
        }

        bold = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(BoldPath);
        extraBold = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(ExtraBoldPath);
        var closePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabDir + "Btn_Close.prefab");
        if (bold == null || extraBold == null || closePrefab == null || LoadSprite("panel_slate") == null || LoadSprite("row_slate") == null)
        { Debug.LogError("[UiKitBuilder] Kit varlıkları eksik, önce Prefabları Kur çalıştırılmalı."); return null; }

        var root = Child("Scoreboard", safe);
        Fill(root, 0, 0, 0, 0);
        var win = Child("Window", root.transform);   // karartma + gövde; kapalı başlar
        // SafeArea'nın boyu ekrandan küçük olabilir: karartma Fill ile değil, ortadan büyük bir kareyle ekranı örter (ayarlar penceresi gibi)
        Anchor(win, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(6000, 6000));
        Dim(win);
        var uw = win.AddComponent<UiWindow>();

        var body = Child("Body", win.transform);
        Size(body, 1640, 900);
        Sliced(body, "panel_slate");

        var close = Nested(closePrefab, body.transform, "Close");
        Anchor(close, new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1), new Vector2(18, 18), new Vector2(120, 120)); // köşeden dışarı taşar: sütun başlıklarını örtmesin
        UnityEventTools.AddPersistentListener(close.GetComponent<Button>().onClick, uw.Close);

        var header = Text(Band(body.transform, "Header", 22, 76, 150).transform, "Text", "", 50, extraBold);
        Fill(header.gameObject, 0, 0, 0, 0);
        header.richText = false;
        AutoSize(header, 50, 28);

        // üst tablo: bu elin dökümü
        var L = TextAlignmentOptions.CaplineLeft;
        var R = TextAlignmentOptions.CaplineRight;
        var topHead = RowObj(body.transform, "TopHead", 108, HeadH, false);
        Title(topHead, "col_player", "table.col_player", 330, 0, L);
        Title(topHead, "col_left", "table.col_left", 400, 1, R);
        Title(topHead, "col_penalties", "table.col_penalties", 250, 0, R);
        Title(topHead, "col_finish", "table.col_finish", 250, 0, R);
        Title(topHead, "col_hand", "table.col_hand", 250, 0, R);
        var top = new List<RowBuild>();
        for (int i = 0; i < 4; i++)
        {
            var r = DataRow(body.transform, "TopRow" + (i + 1), 164 + i * (RowH + 4));
            r.cells = new TMP_Text[]
            {
                DataCell(r.root.transform, "Name", L, 330, 0), DataCell(r.root.transform, "Left", R, 400, 1), DataCell(r.root.transform, "Penalty", R, 250, 0),
                DataCell(r.root.transform, "Finish", R, 250, 0), DataCell(r.root.transform, "Total", R, 250, 0),
            };
            top.Add(r);
        }

        var pen = Text(Band(body.transform, "PenaltyLine", 414, 52, Pad).transform, "Text", "", 32, bold);
        Fill(pen.gameObject, 12, 0, -12, 0);
        pen.alignment = L;
        pen.color = new Color(1f, 0.78f, 0.38f);
        pen.richText = false;
        AutoSize(pen, 32, 28);

        var sep = Band(body.transform, "Separator", 474, 3, Pad);
        var sepImg = sep.AddComponent<Image>();
        sepImg.color = new Color(1, 1, 1, 0.15f);
        sepImg.raycastTarget = false;

        // alt tablo: eller ve toplam
        var botHead = RowObj(body.transform, "BotHead", 488, HeadH, false);
        Title(botHead, "col_player", "table.col_player", 330, 0, L);
        var handHeaders = new TMP_Text[7];
        for (int n = 0; n < 7; n++)
        {
            var h = DataCell(botHead.transform, "Hand" + (n + 1), R, 100, 1);
            h.font = extraBold; h.fontSizeMax = 32; h.fontSizeMin = 28; h.fontSize = 32; h.color = Mute;
            h.margin = new Vector4(4, 0, 4, 0);
            h.overflowMode = TextOverflowModes.Overflow;   // başlık satırı alçak: üç nokta metni silmesin
            handHeaders[n] = h;
        }
        Title(botHead, "col_total", "table.col_total", 160, 0, R);
        var bot = new List<RowBuild>();
        for (int i = 0; i < 4; i++)
        {
            var r = DataRow(body.transform, "BotRow" + (i + 1), 546 + i * (RowH + 4));
            var cells = new List<TMP_Text> { DataCell(r.root.transform, "Name", L, 330, 0) };
            for (int n = 0; n < 7; n++) cells.Add(DataCell(r.root.transform, "Hand" + (n + 1), R, 100, 1));
            cells.Add(DataCell(r.root.transform, "Total", R, 160, 0));
            r.cells = cells.ToArray();
            bot.Add(r);
        }

        var foot = Text(Band(body.transform, "Footer", 818, 56, Pad).transform, "Text", "", 36, bold);
        Fill(foot.gameObject, 0, 0, 0, 0);
        foot.richText = false;

        var view = root.AddComponent<ScoreboardView>();
        var so = new SerializedObject(view);
        so.FindProperty("window").objectReferenceValue = win;
        so.FindProperty("uiWindow").objectReferenceValue = uw;
        so.FindProperty("closeButton").objectReferenceValue = close;
        so.FindProperty("header").objectReferenceValue = header;
        so.FindProperty("penaltyLine").objectReferenceValue = pen;
        so.FindProperty("footer").objectReferenceValue = foot;
        so.FindProperty("layoutSignature").stringValue = sig;
        var hh = so.FindProperty("handHeaders");
        hh.arraySize = 7;
        for (int n = 0; n < 7; n++) hh.GetArrayElementAtIndex(n).objectReferenceValue = handHeaders[n];
        SetRows(so.FindProperty("topRows"), top);
        SetRows(so.FindProperty("botRows"), bot);
        so.ApplyModifiedPropertiesWithoutUndo();

        win.SetActive(false);
        SameLayerAll(root, safe.gameObject.layer);
        if (sibling >= 0) root.transform.SetSiblingIndex(sibling); else root.transform.SetAsLastSibling();
        return view;
    }

    static string SourceSignature()
    {
        var text = System.IO.File.ReadAllText(Application.dataPath + "/Osman/Editor/UiKitTable.cs").Replace("\r\n", "\n");
        using (var md5 = System.Security.Cryptography.MD5.Create())
            return System.BitConverter.ToString(md5.ComputeHash(System.Text.Encoding.UTF8.GetBytes(text))).Replace("-", "");
    }

    static void SetRows(SerializedProperty arr, List<RowBuild> rows)
    {
        arr.arraySize = rows.Count;
        for (int i = 0; i < rows.Count; i++)
        {
            var e = arr.GetArrayElementAtIndex(i);
            e.FindPropertyRelative("root").objectReferenceValue = rows[i].root;
            e.FindPropertyRelative("bg").objectReferenceValue = rows[i].bg;
            var c = e.FindPropertyRelative("cells");
            c.arraySize = rows[i].cells.Length;
            for (int k = 0; k < rows[i].cells.Length; k++) c.GetArrayElementAtIndex(k).objectReferenceValue = rows[i].cells[k];
        }
    }

    // Üstten çapalı, yatay uzayan şerit
    static GameObject Band(Transform parent, string name, float y, float h, float margin)
    {
        var g = Child(name, parent);
        Anchor(g, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -y), new Vector2(-2 * margin, h));
        return g;
    }

    static GameObject RowObj(Transform parent, string name, float y, float h, bool background)
    {
        var g = Band(parent, name, y, h, Pad);
        if (background) { var img = Sliced(g, "row_slate"); img.raycastTarget = false; }
        var hl = g.AddComponent<HorizontalLayoutGroup>();
        hl.padding = new RectOffset(12, 12, 0, 0);
        hl.childAlignment = TextAnchor.MiddleLeft;
        hl.childControlWidth = hl.childControlHeight = true;
        hl.childForceExpandWidth = hl.childForceExpandHeight = false;
        return g;
    }

    static RowBuild DataRow(Transform parent, string name, float y)
    {
        var g = RowObj(parent, name, y, RowH, true);
        var mine = Child("Mine", g.transform);   // yerel oyuncu vurgusu, hücrelerin altında
        Fill(mine, 0, 0, 0, 0);
        mine.AddComponent<LayoutElement>().ignoreLayout = true;
        mine.transform.SetAsFirstSibling();
        var hi = Pill(mine, new Color(0.30f, 0.62f, 0.95f, 0.40f));
        hi.pixelsPerUnitMultiplier = 0.5f;
        hi.raycastTarget = false;
        hi.enabled = false;
        return new RowBuild { root = g, bg = hi };
    }

    static void Layout(GameObject g, float pref, float flex)
    {
        var le = g.AddComponent<LayoutElement>();
        le.minWidth = 0;
        le.preferredWidth = pref;
        le.flexibleWidth = flex;
    }

    static void AutoSize(TMP_Text t, float max, float min)
    {
        t.enableAutoSizing = true;
        t.fontSizeMax = max;
        t.fontSizeMin = min;
        t.fontSize = max;
    }

    // Çalışma zamanında yazılan hücre: LocText yok
    static TMP_Text DataCell(Transform row, string name, TextAlignmentOptions al, float pref, float flex)
    {
        var t = Text(row, name, "", 36, bold);
        t.alignment = al;
        t.richText = false;
        t.overflowMode = TextOverflowModes.Ellipsis;
        t.margin = new Vector4(10, 0, 10, 0);
        AutoSize(t, 36, 22);
        Layout(t.gameObject, pref, flex);
        return t;
    }

    // Sabit sütun başlığı: LocText'li
    static void Title(GameObject row, string name, string key, float pref, float flex, TextAlignmentOptions al)
    {
        var t = Label(row.transform, name, key, 32, extraBold);
        t.alignment = al;
        t.color = Mute;
        t.richText = false;
        t.margin = new Vector4(10, 0, 10, 0);
        AutoSize(t, 32, 28);
        Layout(t.gameObject, pref, flex);
    }


    static void RebindScoreButton(Button b, ScoreboardView view)
    {
        var ev = b.onClick;
        bool has = false;
        for (int i = ev.GetPersistentEventCount() - 1; i >= 0; i--)
        {
            if (ev.GetPersistentTarget(i) == view && ev.GetPersistentMethodName(i) == "Toggle") { has = true; continue; }
            if (ev.GetPersistentTarget(i) == null || ev.GetPersistentMethodName(i) == "ToggleLiveScoreboard")
                UnityEventTools.RemovePersistentListener(ev, i);
        }
        if (!has) UnityEventTools.AddPersistentListener(ev, view.Toggle);
        EditorUtility.SetDirty(b);
    }
}
