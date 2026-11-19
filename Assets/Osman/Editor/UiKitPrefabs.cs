using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// UI kiti: prefab bölümü. Butonlar, pencere, satır, anahtar ve kalıcı UIRoot katmanı; her çağrıda baştan kurulur.
// Ölçüler 1920x1080 referans biriminde. Nesneler önizleme sahnesinde kurulur, açık sahne kirlenmez.
public static partial class UiKitBuilder
{
    const string PrefabDir = "Assets/Osman/Prefabs/UI/";
    const string SpriteDir = "Assets/Sprites/ui_kit/";
    const string UIRootPath = "Assets/Resources/UIRoot.prefab";
    const float DimBleed = 300f; // karartma güvenli alanın dışına (çentik şeridi) taşsın, arkası tıklanmasın

    static Scene preview;
    static TMP_FontAsset bold, extraBold;
    static Material outline;

    [MenuItem("Okey/UI Kit/Prefabları Kur")]
    public static void BuildPrefabs()
    {
        if (EditorApplication.isPlaying) { Debug.LogWarning("[UiKitBuilder] Play modunda prefab kurulmaz."); return; }
        bold = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(BoldPath);
        extraBold = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(ExtraBoldPath);
        outline = AssetDatabase.LoadAssetAtPath<Material>(OutlinePath);
        if (bold == null || extraBold == null || outline == null) { Debug.LogWarning("[UiKitBuilder] Önce BuildFonts çalıştırılmalı."); return; }
        foreach (var n in new[] { "btn_green", "btn_orange", "btn_teal", "btn_red", "panel_slate", "row_slate", "coin" })
            if (LoadSprite(n) == null) { Debug.LogWarning("[UiKitBuilder] Görsel yok: " + SpriteDir + n + ".png"); return; }
        if (!AssetDatabase.IsValidFolder("Assets/Osman/Prefabs")) AssetDatabase.CreateFolder("Assets/Osman", "Prefabs");
        if (!AssetDatabase.IsValidFolder("Assets/Osman/Prefabs/UI")) AssetDatabase.CreateFolder("Assets/Osman/Prefabs", "UI");

        preview = EditorSceneManager.NewPreviewScene();
        try
        {
            var green = Save(Button("Btn_Green", "btn_green", 420, 140, ""), "Btn_Green");
            Save(Button("Btn_Orange", "btn_orange", 420, 140, ""), "Btn_Orange");
            var teal = Save(Button("Btn_Teal", "btn_teal", 420, 140, ""), "Btn_Teal");
            var red = Save(Button("Btn_Red", "btn_red", 420, 140, ""), "Btn_Red");
            var closeBtn = Save(CloseButton(), "Btn_Close");
            var window = Save(Window(closeBtn), "Window");
            Save(Row(), "Row");
            Save(Toggle(), "Toggle");
            SaveAt(BuildUIRoot(window, green, red, teal), UIRootPath);
        }
        finally
        {
            EditorSceneManager.ClosePreviewScene(preview);
        }
        AssetDatabase.SaveAssets();
        Debug.Log("[UiKitBuilder] UI kiti prefab'ları kuruldu.");
    }

    static GameObject Button(string name, string sprite, float w, float h, string label)
    {
        var g = Root(name);
        Size(g, w, h);
        var img = Sliced(g, sprite);
        g.AddComponent<Button>().targetGraphic = img;
        g.AddComponent<UiPressScale>();
        var t = Text(g.transform, "Label", label, 48, extraBold);
        t.fontSharedMaterial = outline; // renkli butonda okunurluk için
        Fill(t.gameObject, 16, 14, -16, 0); // alttaki gölge payı kadar yukarıda
        return g;
    }

    // Görünen kırmızı düğme 96x96, dokunma alanı 120x120 (48dp tabanı): şeffaf kök görsel hedef alanı olur
    static GameObject CloseButton()
    {
        var g = Root("Btn_Close");
        Size(g, 120, 120);
        g.AddComponent<Image>().color = Color.clear;
        var v = Child("Visual", g.transform);
        Size(v, 96, 96);
        var img = Sliced(v, "btn_red");
        img.raycastTarget = false;
        g.AddComponent<Button>().targetGraphic = img;
        g.AddComponent<UiPressScale>();
        var t = Text(v.transform, "Label", "X", 48, extraBold);
        t.fontSharedMaterial = outline;
        Fill(t.gameObject, 16, 14, -16, 0);
        return g;
    }

    static GameObject Window(GameObject closePrefab)
    {
        var g = Root("Window");
        Fill(g, -DimBleed, -DimBleed, DimBleed, DimBleed);
        Dim(g);
        g.AddComponent<UiWindow>();

        var body = Child("Body", g.transform);
        Size(body, 1100, 760);
        Sliced(body, "panel_slate");

        var title = Text(body.transform, "Title", "", 56, extraBold);
        Anchor(title.gameObject, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -40), new Vector2(-260, 90));

        var close = Nested(closePrefab, body.transform, "Close");
        Anchor(close, new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-16, -16), new Vector2(120, 120)); // 96'lık görsel eskisi gibi köşeden 28 içeride
        UnityEventTools.AddPersistentListener(close.GetComponent<Button>().onClick, g.GetComponent<UiWindow>().Close);

        Fill(Child("Content", body.transform), 60, 210, -60, -150);

        var buttons = Child("Buttons", body.transform);
        Anchor(buttons, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0), new Vector2(0, 50), new Vector2(-80, 140));
        var hl = buttons.AddComponent<HorizontalLayoutGroup>();
        hl.spacing = 40;
        hl.childAlignment = TextAnchor.MiddleCenter;
        hl.childControlWidth = hl.childControlHeight = false;
        hl.childForceExpandWidth = hl.childForceExpandHeight = false;
        return g;
    }

    static GameObject Row()
    {
        var g = Root("Row");
        Size(g, 900, 120);
        Sliced(g, "row_slate");
        var label = Text(g.transform, "Label", "", 40, bold);
        label.alignment = TextAlignmentOptions.MidlineLeft;
        Fill(label.gameObject, 40, 0, 0, 0);
        label.rectTransform.anchorMax = new Vector2(0.6f, 1);
        var value = Text(g.transform, "Value", "", 36, bold);
        value.alignment = TextAlignmentOptions.MidlineRight;
        Fill(value.gameObject, 0, 0, -40, 0);
        value.rectTransform.anchorMin = new Vector2(0.6f, 0);
        return g;
    }

    // Kapalıyken gri hap + soldaki topuz; açıkken yeşil "On" katmanı (sağda topuzuyla) üstüne açılır, UiToggleView eşler.
    static GameObject Toggle()
    {
        var g = Root("Toggle");
        Size(g, 150, 120); // dokunma alanı 48dp tabanı; görsel 150x76 ortada
        g.AddComponent<Image>().color = Color.clear;
        var vis = Child("Visual", g.transform);
        Size(vis, 150, 76);
        var bg = Pill(vis, new Color32(0x3C, 0x46, 0x52, 0xFF));
        bg.raycastTarget = false;
        Knob(vis.transform, 0f);
        var on = Child("On", vis.transform);
        Fill(on, 0, 0, 0, 0);
        Pill(on, new Color32(0x4C, 0xAF, 0x2F, 0xFF)).raycastTarget = false;
        Knob(on.transform, 1f);
        on.SetActive(false);
        var tg = g.AddComponent<Toggle>();
        tg.targetGraphic = bg;
        tg.isOn = false;
        var view = new SerializedObject(g.AddComponent<UiToggleView>());
        view.FindProperty("on").objectReferenceValue = on;
        view.ApplyModifiedPropertiesWithoutUndo();
        return g;
    }

    static GameObject BuildUIRoot(GameObject windowPrefab, GameObject green, GameObject red, GameObject teal)
    {
        var g = Root("UIRoot");
        var canvas = g.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1000;
        var cs = g.AddComponent<CanvasScaler>();
        cs.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        cs.referenceResolution = new Vector2(1920, 1080);
        cs.matchWidthOrHeight = 0.5f;
        g.AddComponent<GraphicRaycaster>();
        var root = g.AddComponent<UIRoot>();

        var safe = Child("Safe", g.transform);
        Fill(safe, 0, 0, 0, 0);
        safe.AddComponent<Crystal.SafeArea>();

        // Onay: Window örneği, gövde metni + evet (yeşil/kırmızı) + Vazgeç
        var confirm = Nested(windowPrefab, safe.transform, "Confirm");
        var body = confirm.transform.Find("Body");
        var closeBtn = body.Find("Close").GetComponent<Button>();
        UnityEventTools.RemovePersistentListener(closeBtn.onClick, 0); // UIRoot hayır ile çözer
        var bodyText = Text(body.Find("Content"), "Body", "", 44, bold);
        Fill(bodyText.gameObject, 0, 0, 0, 0);
        bodyText.textWrappingMode = TextWrappingModes.Normal;
        var buttons = body.Find("Buttons");
        var yes = Nested(green, buttons, "Yes");
        var yesRed = Nested(red, buttons, "YesDestructive");
        var no = Nested(teal, buttons, "No");
        no.GetComponentInChildren<TMP_Text>().text = "Vazgeç";
        confirm.SetActive(false);

        // Yükleme onayın üstünde: onay açıkken yükleme başlarsa butonları tıklanmasın
        var loading = Child("Loading", safe.transform);
        Fill(loading, -DimBleed, -DimBleed, DimBleed, DimBleed);
        Dim(loading);
        var ring = Child("Ring", loading.transform);
        Anchor(ring, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 50), new Vector2(96, 96));
        var ringImg = ring.AddComponent<Image>();
        ringImg.sprite = LoadSprite("coin");
        ringImg.raycastTarget = false;
        var loadingText = Text(loading.transform, "Text", "", 40, bold);
        Anchor(loadingText.gameObject, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -60), new Vector2(1200, 80));
        loading.SetActive(false);

        // Bildirim en sonda: onay ve yüklemenin de üstünde görünsün
        var toast = Child("Toast", safe.transform);
        Anchor(toast, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -60), new Vector2(1000, 110));
        Sliced(toast, "btn_orange").raycastTarget = false; // koyu panelde de bordo zeminde de seçilsin
        var toastText = Text(toast.transform, "Text", "", 40, extraBold); // dış çizgi materyali ExtraBold atlasına ait
        toastText.fontSharedMaterial = outline;
        Fill(toastText.gameObject, 30, 12, -30, 0);
        toast.SetActive(false);

        var so = new SerializedObject(root);
        so.FindProperty("toast").objectReferenceValue = toast;
        so.FindProperty("toastText").objectReferenceValue = toastText;
        so.FindProperty("loading").objectReferenceValue = loading;
        so.FindProperty("loadingText").objectReferenceValue = loadingText;
        so.FindProperty("ring").objectReferenceValue = ring.transform;
        so.FindProperty("confirm").objectReferenceValue = confirm.GetComponent<UiWindow>();
        so.FindProperty("confirmTitle").objectReferenceValue = body.Find("Title").GetComponent<TMP_Text>();
        so.FindProperty("confirmBody").objectReferenceValue = bodyText;
        so.FindProperty("yes").objectReferenceValue = yes.GetComponent<Button>();
        so.FindProperty("yesDestructive").objectReferenceValue = yesRed.GetComponent<Button>();
        so.FindProperty("no").objectReferenceValue = no.GetComponent<Button>();
        so.FindProperty("close").objectReferenceValue = closeBtn;
        so.ApplyModifiedPropertiesWithoutUndo();
        return g;
    }

    // --- yardımcılar ---

    static GameObject Root(string name)
    {
        var g = new GameObject(name, typeof(RectTransform));
        SceneManager.MoveGameObjectToScene(g, preview);
        return g;
    }

    static GameObject Child(string name, Transform parent)
    {
        var g = new GameObject(name, typeof(RectTransform));
        g.transform.SetParent(parent, false);
        return g;
    }

    static GameObject Nested(GameObject prefab, Transform parent, string name)
    {
        var g = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        g.name = name;
        return g;
    }

    static void Size(GameObject g, float w, float h) =>
        Anchor(g, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(w, h));

    static void Anchor(GameObject g, Vector2 min, Vector2 max, Vector2 pivot, Vector2 pos, Vector2 size)
    {
        var rt = (RectTransform)g.transform;
        rt.anchorMin = min;
        rt.anchorMax = max;
        rt.pivot = pivot;
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
    }

    static void Fill(GameObject g, float left, float bottom, float right, float top)
    {
        var rt = (RectTransform)g.transform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(left, bottom);
        rt.offsetMax = new Vector2(right, top);
    }

    static Sprite LoadSprite(string name) => AssetDatabase.LoadAssetAtPath<Sprite>(SpriteDir + name + ".png");

    static Image Sliced(GameObject g, string sprite)
    {
        var img = g.AddComponent<Image>();
        img.sprite = LoadSprite(sprite);
        img.type = Image.Type.Sliced;
        return img;
    }

    static void Dim(GameObject g)
    {
        var img = g.AddComponent<Image>();
        img.color = new Color(0, 0, 0, 0.6f);
        img.raycastTarget = true;
    }

    // Unity'nin yerleşik beyaz yuvarlak köşeli görseli; çarpanla köşeler hap kadar büyür
    static Image Pill(GameObject g, Color color)
    {
        var img = g.AddComponent<Image>();
        img.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Background.psd");
        img.type = Image.Type.Sliced;
        img.pixelsPerUnitMultiplier = 0.25f;
        img.color = color;
        return img;
    }

    static void Knob(Transform parent, float x)
    {
        var k = Child("Knob", parent);
        var a = new Vector2(x, 0.5f);
        Anchor(k, a, a, a, new Vector2(x == 0f ? 8 : -8, 0), new Vector2(60, 60));
        var img = k.AddComponent<Image>();
        img.sprite = KnobSprite;
        img.raycastTarget = false;
    }

    // Unity'nin yerleşik yuvarlak görseli (anahtar topuzu, lobi avatarı)
    static Sprite KnobSprite => AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");

    // Kit penceresi örneği: başlık ve gövde boyu verilir
    static GameObject KitWindow(GameObject windowPrefab, Transform parent, string name, string title, Vector2 bodySize)
    {
        var w = Nested(windowPrefab, parent, name);
        var body = w.transform.Find("Body");
        ((RectTransform)body).sizeDelta = bodySize;
        body.Find("Title").GetComponent<TMP_Text>().text = title;
        return w;
    }

    // TMP_InputField'in beklediği düzen: kök (zemin) > Text Area (RectMask2D) > Placeholder + Text. Üstte ortaya çapalı.
    static TMP_InputField TextInput(Transform parent, string name, string placeholderText, Vector2 size)
    {
        var g = Child(name, parent);
        Anchor(g, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), Vector2.zero, size);
        var img = Sliced(g, "row_slate");
        var area = Child("Text Area", g.transform);
        Fill(area, 40, 10, -40, -10);
        area.AddComponent<RectMask2D>();
        var placeholder = Text(area.transform, "Placeholder", placeholderText, 48, bold);
        placeholder.color = new Color(1, 1, 1, 0.45f);
        placeholder.alignment = TextAlignmentOptions.MidlineLeft;
        Fill(placeholder.gameObject, 0, 0, 0, 0);
        var text = Text(area.transform, "Text", "", 48, bold);
        text.alignment = TextAlignmentOptions.MidlineLeft;
        Fill(text.gameObject, 0, 0, 0, 0);
        var field = g.AddComponent<TMP_InputField>();
        field.targetGraphic = img;
        field.textViewport = (RectTransform)area.transform;
        field.textComponent = text;
        field.placeholder = placeholder;
        field.lineType = TMP_InputField.LineType.SingleLine;
        field.fontAsset = bold;
        return field;
    }

    static TextMeshProUGUI Text(Transform parent, string name, string text, float size, TMP_FontAsset font)
    {
        var t = Child(name, parent).AddComponent<TextMeshProUGUI>();
        t.font = font;
        t.fontSize = size;
        t.color = Color.white;
        t.alignment = TextAlignmentOptions.Center;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.raycastTarget = false;
        t.text = text;
        return t;
    }

    static GameObject Save(GameObject g, string name) => SaveAt(g, PrefabDir + name + ".prefab");

    static GameObject SaveAt(GameObject g, string path)
    {
        // iç içe prefab örneklerinde koddan yapılan değişiklikler kaydedilsin
        foreach (var c in g.GetComponentsInChildren<Component>(true))
            if (PrefabUtility.IsPartOfPrefabInstance(c))
                PrefabUtility.RecordPrefabInstancePropertyModifications(c);
        var prefab = PrefabUtility.SaveAsPrefabAsset(g, path);
        Object.DestroyImmediate(g);
        return prefab;
    }
}
