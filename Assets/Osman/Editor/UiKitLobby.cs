using System.Collections.Generic;
using System.Text.RegularExpressions;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

// UI kiti: lobi ekranı (LobbyMenu). Canvas'ın çocukları ProfilePanel ve SettingsPanel dışında silinip kitten kurulur;
// kökteki nesnelere (ShopCanvas dahil) dokunulmaz. RoomCard.prefab da burada üretilir.
public static partial class UiKitBuilder
{
    const string LobbyPath = "Assets/Osman/Scenes/LobbyMenu.unity";
    const string RoomCardPath = PrefabDir + "RoomCard.prefab";
    const float TopMargin = 20f; // üst bar ekran kenarına yapışmasın
    static readonly Color32 AvatarColor = new Color32(0xE0, 0x8A, 0x2E, 0xFF);
    static readonly Color32 SoftText = new Color32(0xCF, 0xD6, 0xDE, 0xFF);

    // Masa sahnesinde koltuk 1'deki oyuncunun gördüğü düzen (SeatMath.RelativeIndex, SeatManager.seatTextFields):
    // koltuk 1 altta, 2 sağda, 3 üstte, 4 solda. Kart merkezine göre konumlar.
    static readonly Vector2[] SeatPos = { new Vector2(0, -175), new Vector2(322, -68), new Vector2(0, 40), new Vector2(-322, -68) };

    [MenuItem("Okey/UI Kit/Lobiyi Kur")]
    public static void BuildLobbyScreen() => Guarded(LobbyPath, BuildLobby);

    static void BuildLobby()
    {
        if (EditorApplication.isPlaying) { Debug.LogWarning("[UiKitBuilder] Play modunda lobi kurulmaz."); return; }
        bold = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(BoldPath);
        extraBold = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(ExtraBoldPath);
        outline = AssetDatabase.LoadAssetAtPath<Material>(OutlinePath);
        var green = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabDir + "Btn_Green.prefab");
        var orange = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabDir + "Btn_Orange.prefab");
        var teal = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabDir + "Btn_Teal.prefab");
        var closeBtn = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabDir + "Btn_Close.prefab");
        var window = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabDir + "Window.prefab");
        var row = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabDir + "Row.prefab");
        var toggle = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabDir + "Toggle.prefab");
        if (bold == null || extraBold == null || outline == null || green == null || orange == null || teal == null || closeBtn == null
            || window == null || row == null || toggle == null || toggle.GetComponent<Toggle>() == null)
        { Debug.LogWarning("[UiKitBuilder] Lobi için font ya da prefab eksik; sahneye dokunulmadı."); return; }
        foreach (var n in new[] { "row_slate", "panel_slate", "btn_green", "coin" })
            if (LoadSprite(n) == null) { Debug.LogWarning("[UiKitBuilder] Görsel yok: " + SpriteDir + n + ".png; sahneye dokunulmadı."); return; }
        foreach (var path in new[] { "Body", "Body/Title", "Body/Close", "Body/Content", "Body/Buttons" })
            if (window.transform.Find(path) == null) { Debug.LogWarning("[UiKitBuilder] Window prefab'ında " + path + " yok; sahneye dokunulmadı."); return; }
        if (closeBtn.transform.Find("Visual") == null || row.transform.Find("Label") == null || row.transform.Find("Value") == null)
        { Debug.LogWarning("[UiKitBuilder] Btn_Close/Visual ya da Row/Label, Row/Value yok; sahneye dokunulmadı."); return; }

        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        var scene = EditorSceneManager.OpenScene(LobbyPath, OpenSceneMode.Single);
        ButtonFuncs handler = null;
        foreach (var r in scene.GetRootGameObjects())
            if (r.name == "Canvas") handler = r.GetComponent<ButtonFuncs>();
        var account = Object.FindFirstObjectByType<LobbyAccountUI>(FindObjectsInactive.Include);
        var shop = Object.FindFirstObjectByType<ShopController>(FindObjectsInactive.Include);
        if (handler == null || account == null || shop == null)
        { Debug.LogWarning("[UiKitBuilder] LobbyMenu'de ButtonFuncs'lı Canvas, LobbyAccountUI ya da ShopController yok."); return; }
        var canvas = handler.transform;
        var profile = canvas.Find("ProfilePanel");
        var settings = canvas.Find("SettingsPanel");
        // İkinci çalıştırmada eski nesneler yok: değerler önceki kurulumdan okunur
        var oldBack = canvas.Find("SafeArea/MenuBackground") ?? canvas.Find("Background");
        var bg = oldBack != null && oldBack.GetComponent<Image>() != null ? oldBack.GetComponent<Image>().sprite : null;
        var rounds = RoundOptions(canvas, handler);
        if (profile == null || settings == null || bg == null || rounds == null)
        { Debug.LogWarning("[UiKitBuilder] ProfilePanel, SettingsPanel, arka plan görseli ya da el seçenekleri bulunamadı; sahneye dokunulmadı."); return; }
        if (!OnlyKnownRefsIntoCanvas(scene, canvas, profile, settings)) return;

        preview = EditorSceneManager.NewPreviewScene();
        GameObject cardPrefab;
        try { cardPrefab = SaveAt(BuildRoomCard(green), RoomCardPath); }
        finally { EditorSceneManager.ClosePreviewScene(preview); }

        deleting = true;
        for (int i = canvas.childCount - 1; i >= 0; i--)
        {
            var c = canvas.GetChild(i);
            if (c != profile && c != settings)
                Object.DestroyImmediate(c.gameObject);
        }

        var cs = canvas.GetComponent<CanvasScaler>();
        cs.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        cs.referenceResolution = new Vector2(1920, 1080);
        cs.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        cs.matchWidthOrHeight = 0.5f;

        var back = Child("Background", canvas);
        Fill(back, 0, 0, 0, 0);
        var backImg = back.AddComponent<Image>();
        backImg.sprite = bg;
        backImg.raycastTarget = false;

        var safe = Child("Safe", canvas);
        Fill(safe, 0, 0, 0, 0);
        safe.AddComponent<Crystal.SafeArea>();

        // --- üst bar ---
        var top = Child("TopBar", safe.transform);
        Anchor(top, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -TopMargin), new Vector2(0, 130));
        var left = new Vector2(0, 0.5f);
        var right = new Vector2(1, 0.5f);

        var chip = Child("ProfileChip", top.transform);
        Anchor(chip, left, left, left, new Vector2(30, 0), new Vector2(520, 120));
        var chipButton = chip.AddComponent<Button>();
        chipButton.targetGraphic = Sliced(chip, "row_slate");
        chip.AddComponent<UiPressScale>();
        var chipInitial = Avatar(chip.transform, 96, 52);
        var chipText = Text(chip.transform, "ChipText", "", 36, bold);
        chipText.alignment = TextAlignmentOptions.MidlineLeft;
        Fill(chipText.gameObject, 126, 0, -24, 0);
        ShrinkToFit(chipText, 24);
        var initialSo = new SerializedObject(chip.AddComponent<ChipInitial>());
        initialSo.FindProperty("source").objectReferenceValue = chipText;
        initialSo.FindProperty("target").objectReferenceValue = chipInitial;
        initialSo.ApplyModifiedPropertiesWithoutUndo();

        var coin = Child("CoinChip", top.transform);
        Anchor(coin, right, right, right, new Vector2(-280, 0), new Vector2(360, 120));
        Sliced(coin, "row_slate");
        var icon = Child("CoinIcon", coin.transform);
        Anchor(icon, left, left, left, new Vector2(20, 0), new Vector2(80, 80));
        var iconImg = icon.AddComponent<Image>();
        iconImg.sprite = LoadSprite("coin");
        iconImg.preserveAspect = true;
        iconImg.raycastTarget = false;
        var coinText = Text(coin.transform, "CoinText", "0", 40, extraBold);
        coinText.alignment = TextAlignmentOptions.MidlineLeft;
        Fill(coinText.gameObject, 112, 0, -124, 0);
        ShrinkToFit(coinText, 26);
        var plus = RoundButton(closeBtn, coin.transform, "Plus", "btn_green", "+");
        Anchor(plus, right, right, right, Vector2.zero, new Vector2(120, 120));

        var settingsButton = ChoiceButton(teal, top.transform, "SettingsButton", "Ayarlar", 220, 120, 40);
        Anchor(settingsButton, right, right, right, new Vector2(-30, 0), new Vector2(220, 120));

        // --- masa kartları ---
        var cards = Child("Cards", safe.transform);
        Fill(cards, 0, 170, 0, -(130 + TopMargin));
        var viewport = Child("Viewport", cards.transform);
        Fill(viewport, 0, 0, 0, 0);
        viewport.AddComponent<RectMask2D>();
        viewport.AddComponent<Image>().color = Color.clear; // kartlar arasından da sürüklensin
        var content = Child("Content", viewport.transform);
        Anchor(content, new Vector2(0, 0), new Vector2(0, 1), new Vector2(0, 0.5f), Vector2.zero, Vector2.zero);
        var hl = content.AddComponent<HorizontalLayoutGroup>();
        hl.spacing = 60;
        hl.childAlignment = TextAnchor.MiddleLeft;
        hl.childControlWidth = hl.childControlHeight = false;
        hl.childForceExpandWidth = hl.childForceExpandHeight = false;
        content.AddComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        var scroll = cards.AddComponent<ScrollRect>();
        scroll.viewport = (RectTransform)viewport.transform;
        scroll.content = (RectTransform)content.transform;
        scroll.horizontal = true;
        scroll.vertical = false;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.inertia = false;
        var snap = cards.AddComponent<UiSnapScroll>();

        var empty = Child("EmptyCard", cards.transform);
        Size(empty, 1000, 520);
        Sliced(empty, "panel_slate");
        var emptyTitle = Text(empty.transform, "Title", "Açık masa yok", 56, extraBold);
        Anchor(emptyTitle.gameObject, new Vector2(0, 0.5f), new Vector2(1, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 45), new Vector2(-80, 80));
        var emptyBody = Text(empty.transform, "Body", "İlk masayı sen aç.", 40, bold);
        emptyBody.color = SoftText;
        Anchor(emptyBody.gameObject, new Vector2(0, 0.5f), new Vector2(1, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -45), new Vector2(-80, 60));

        // --- alt bar ---
        var bottom = Child("BottomBar", safe.transform);
        Anchor(bottom, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0), Vector2.zero, new Vector2(0, 170));
        var bl = bottom.AddComponent<HorizontalLayoutGroup>();
        bl.spacing = 30;
        bl.childAlignment = TextAnchor.MiddleCenter;
        bl.childControlWidth = bl.childControlHeight = false;
        bl.childForceExpandWidth = bl.childForceExpandHeight = false;
        var play = ChoiceButton(green, bottom.transform, "PlayNowButton", "HEMEN OYNA", 620, 150, 52);
        var create = ChoiceButton(orange, bottom.transform, "CreateButton", "MASA AÇ", 420, 150, 48);
        var bots = ChoiceButton(teal, bottom.transform, "BotsButton", "BOTLARLA OYNA", 420, 150, 44);

        // --- masa açma penceresi: ad, el sayısı (- / +), şifre anahtarı + şifre ---
        var createWin = KitWindow(window, canvas, "CreateRoomPanel", "Masa Aç", new Vector2(1100, 760));
        var createContent = createWin.transform.Find("Body/Content");
        FormLabel(createContent, "RoomNameLabel", "Masa adı", 0, 240);
        var roomName = TextInput(createContent, "RoomNameInput", "Masa adı", new Vector2(740, 120));
        Anchor(roomName.gameObject, new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1), Vector2.zero, new Vector2(740, 120));
        roomName.characterLimit = 24;
        var roundsRow = Nested(row, createContent, "RoundsRow");
        Anchor(roundsRow, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -140), new Vector2(980, 120));
        roundsRow.transform.Find("Label").GetComponent<TMP_Text>().text = "El sayısı";
        var roundsText = roundsRow.transform.Find("Value").GetComponent<TMP_Text>();
        Anchor(roundsText.gameObject, right, right, right, new Vector2(-120, 0), new Vector2(200, 120));
        roundsText.alignment = TextAlignmentOptions.Center;
        roundsText.text = rounds[0] + " el";
        var minus = RoundButton(closeBtn, roundsRow.transform, "Minus", "btn_teal", "−"); // "-" Changa'da çok küçük; eksi işareti "+" ile aynı ağırlıkta
        Anchor(minus, right, right, right, new Vector2(-320, 0), new Vector2(120, 120));
        var more = RoundButton(closeBtn, roundsRow.transform, "Plus", "btn_teal", "+");
        Anchor(more, right, right, right, Vector2.zero, new Vector2(120, 120));
        var pwRow = Child("PasswordRow", createContent);
        Anchor(pwRow, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -280), new Vector2(980, 120));
        var pwToggle = Nested(toggle, pwRow.transform, "PasswordToggle").GetComponent<Toggle>();
        Anchor(pwToggle.gameObject, left, left, left, Vector2.zero, new Vector2(150, 120));
        FormLabel(pwRow.transform, "PasswordLabel", "Şifreli masa", 160, 250);
        var pwInput = TextInput(pwRow.transform, "PasswordInput", "Şifre", new Vector2(560, 120));
        Anchor(pwInput.gameObject, right, right, right, Vector2.zero, new Vector2(560, 120));
        var createOk = Nested(green, createWin.transform.Find("Body/Buttons"), "CreateConfirmButton");
        createOk.GetComponentInChildren<TMP_Text>(true).text = "MASAYI KUR";

        // --- şifre penceresi ---
        var pwWin = KitWindow(window, canvas, "PasswordPanel", "Şifre", new Vector2(1100, 560));
        var pwCheck = TextInput(pwWin.transform.Find("Body/Content"), "PasswordCheckInput", "Masanın şifresi", new Vector2(980, 120));
        ((RectTransform)pwCheck.transform).anchoredPosition = new Vector2(0, -40);
        var pwOk = Nested(green, pwWin.transform.Find("Body/Buttons"), "PasswordConfirmButton");
        pwOk.GetComponentInChildren<TMP_Text>(true).text = "GİR";

        // --- bağlantılar ---
        handler.roomCardPrefab = cardPrefab.GetComponent<RoomCard>();
        handler.cardContent = (RectTransform)content.transform;
        handler.emptyCard = empty;
        handler.emptyTitle = emptyTitle;
        handler.emptyBody = emptyBody.gameObject;
        handler.snapScroll = snap;
        handler.createRoomPanel = createWin;
        handler.roomNameInput = roomName;
        handler.passwordInput = pwInput;
        handler.passwordToggle = pwToggle;
        handler.roundOptions = rounds;
        handler.roundsText = roundsText;
        handler.passwordPopupPanel = pwWin;
        handler.passwordCheckInput = pwCheck;
        if (handler.roomManager == null) handler.roomManager = Object.FindFirstObjectByType<RoomManager>(FindObjectsInactive.Include);
        EditorUtility.SetDirty(handler);
        account.chipText = chipText;
        account.lobbyCoinText = coinText;
        EditorUtility.SetDirty(account);
        var shopSo = new SerializedObject(shop);
        shopSo.FindProperty("coinText").objectReferenceValue = coinText;
        shopSo.ApplyModifiedPropertiesWithoutUndo();

        UnityEventTools.AddPersistentListener(chipButton.onClick, account.OpenProfile);
        UnityEventTools.AddPersistentListener(plus.GetComponent<Button>().onClick, shop.OpenShop);
        UnityEventTools.AddPersistentListener(settingsButton.GetComponent<Button>().onClick, account.OpenSettings);
        UnityEventTools.AddPersistentListener(play.GetComponent<Button>().onClick, handler.JoinRandomRoom);
        UnityEventTools.AddPersistentListener(create.GetComponent<Button>().onClick, handler.OpenCreateRoomPanel);
        UnityEventTools.AddPersistentListener(bots.GetComponent<Button>().onClick, handler.PlayWithBots);
        UnityEventTools.AddIntPersistentListener(minus.GetComponent<Button>().onClick, handler.ChangeRounds, -1);
        UnityEventTools.AddIntPersistentListener(more.GetComponent<Button>().onClick, handler.ChangeRounds, 1);
        UnityEventTools.AddVoidPersistentListener(pwToggle.onValueChanged, handler.OnPasswordToggleChanged);
        UnityEventTools.AddPersistentListener(createOk.GetComponent<Button>().onClick, handler.ConfirmCreateRoom);
        UnityEventTools.AddPersistentListener(pwOk.GetComponent<Button>().onClick, handler.ConfirmPasswordAndJoin);
        // Geri tuşu ve X kendi kapatma metodunu çağırır; pencereyi o metot kapatır
        var createUw = createWin.GetComponent<UiWindow>();
        createUw.closeOnBack = false;
        UnityEventTools.AddPersistentListener(createUw.onBack, handler.CloseCreateRoomPanel);
        var pwUw = pwWin.GetComponent<UiWindow>();
        pwUw.closeOnBack = false;
        UnityEventTools.AddPersistentListener(pwUw.onBack, handler.CancelPasswordPanel);

        FixShop(shop);
        createWin.SetActive(false);
        pwWin.SetActive(false);
        profile.SetAsLastSibling();
        settings.SetAsLastSibling();
        foreach (var c in canvas.GetComponentsInChildren<Component>(true))
            if (PrefabUtility.IsPartOfPrefabInstance(c))
                PrefabUtility.RecordPrefabInstancePropertyModifications(c);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[UiKitBuilder] Lobi kuruldu: " + LobbyPath);
    }

    [MenuItem("Okey/UI Kit/Mağazayı Düzelt")]
    public static void BuildShop() => Guarded(LobbyPath, () =>
    {
        if (EditorApplication.isPlaying) { Debug.LogWarning("[UiKitBuilder] Play modunda mağaza düzeltilmez."); return; }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        var scene = EditorSceneManager.OpenScene(LobbyPath, OpenSceneMode.Single);
        var shop = Object.FindFirstObjectByType<ShopController>(FindObjectsInactive.Include);
        var closeBtn = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabDir + "Btn_Close.prefab");
        if (shop == null || closeBtn == null) { Debug.LogWarning("[UiKitBuilder] ShopController ya da Btn_Close.prefab yok; sahneye dokunulmadı."); return; }
        FixShop(shop);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[UiKitBuilder] Mağaza düzeltildi: " + LobbyPath);
    });

    // ShopCanvas yerinde düzeltilir: kök etkin (ShopController.Start çalışsın), pencere gövdesi kapalı, ana Canvas'ın üstünde, UIRoot'un altında.
    static void FixShop(ShopController shop)
    {
        var root = shop.gameObject;
        root.SetActive(true);
        var cv = root.GetComponent<Canvas>();
        cv.sortingOrder = 10;
        var cs = root.GetComponent<CanvasScaler>();
        cs.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        cs.referenceResolution = new Vector2(1920, 1080);
        cs.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        cs.matchWidthOrHeight = 0.5f;
        if (root.GetComponent<GraphicRaycaster>() == null) root.AddComponent<GraphicRaycaster>();
        // Kapalı mağaza lobi dokunuşlarını yutmasın
        foreach (var g in root.GetComponentsInChildren<Graphic>(true))
            if (g.raycastTarget && !g.transform.IsChildOf(root.transform.Find("SafeArea/ShopBackground"))) g.raycastTarget = false;

        var win = root.transform.Find("SafeArea/ShopBackground");
        var uw = win.GetComponent<UiWindow>();
        if (win.Find("Dim") == null)
        {
            var dim = Child("Dim", win);
            Anchor(dim, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(6000, 6000)); // panelden büyük: tüm ekranı kaplar
            Dim(dim);
            dim.transform.SetAsFirstSibling();
        }
        var old = win.Find("Close Shop Button");
        if (old != null)
        {
            var rt = (RectTransform)old;
            var pos = rt.anchoredPosition;
            Object.DestroyImmediate(old.gameObject);
            var close = Nested(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabDir + "Btn_Close.prefab"), win, "Btn_Close");
            Anchor(close, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, new Vector2(120, 120));
            UnityEventTools.AddPersistentListener(close.GetComponent<Button>().onClick, uw.Close);
        }
        win.gameObject.SetActive(false);
        EditorUtility.SetDirty(shop);
        foreach (var c in root.GetComponentsInChildren<Component>(true))
            if (PrefabUtility.IsPartOfPrefabInstance(c)) PrefabUtility.RecordPrefabInstancePropertyModifications(c);
    }

    // El seçenekleri eski TMP_Dropdown'ın metinlerinden ("3 EL" -> 3) ya da önceki kurulumdan okunur.
    static int[] RoundOptions(Transform canvas, ButtonFuncs handler)
    {
        var dd = canvas.Find("SafeArea") != null ? canvas.Find("SafeArea").GetComponentInChildren<TMP_Dropdown>(true) : null;
        if (dd == null)
            return handler.roundOptions != null && handler.roundOptions.Length > 0 ? handler.roundOptions : null;
        var list = new List<int>();
        foreach (var o in dd.options)
        {
            if (int.TryParse(Regex.Match(o.text, @"\d+").Value, out int n) && n > 0) list.Add(n);
            else Debug.LogWarning("[UiKitBuilder] El seçeneğinde sayı yok, atlandı: " + o.text);
        }
        return list.Count > 0 ? list.ToArray() : null;
    }

    // Silinecek nesnelere sahnede başka bir yerden başvuru varsa (bilinen ve yeniden bağlananlar dışında) hiçbir şey silinmez.
    static bool OnlyKnownRefsIntoCanvas(UnityEngine.SceneManagement.Scene scene, Transform canvas, Transform profile, Transform settings)
    {
        var doomed = new HashSet<Transform>();
        foreach (Transform c in canvas)
            if (c != profile && c != settings)
                foreach (var t in c.GetComponentsInChildren<Transform>(true)) doomed.Add(t);
        var known = new HashSet<string> { "LobbyAccountUI.chipText", "LobbyAccountUI.lobbyCoinText", "ShopController.coinText" };
        var bad = new List<string>();
        foreach (var root in scene.GetRootGameObjects())
            foreach (var comp in root.GetComponentsInChildren<Component>(true))
            {
                if (comp == null || comp is Transform || comp is ButtonFuncs || doomed.Contains(comp.transform)) continue;
                var it = new SerializedObject(comp).GetIterator();
                while (it.Next(true))
                {
                    if (it.propertyType != SerializedPropertyType.ObjectReference || it.objectReferenceValue == null) continue;
                    var o = it.objectReferenceValue;
                    var t = o is GameObject go ? go.transform : o is Component oc ? oc.transform : null;
                    string key = comp.GetType().Name + "." + it.propertyPath;
                    if (t != null && doomed.Contains(t) && !known.Contains(key)) bad.Add(comp.name + " " + key + " -> " + t.name);
                }
            }
        if (bad.Count > 0) Debug.LogWarning("[UiKitBuilder] Silinecek lobi nesnelerine başka başvuru var; sahneye dokunulmadı:\n" + string.Join("\n", bad));
        return bad.Count == 0;
    }

    // RoomCard.prefab: 1000x520 panel; üstte ad ve etiketler, ortada dört koltuk yuvası (SeatPos).
    static GameObject BuildRoomCard(GameObject green)
    {
        var g = Root("RoomCard");
        Size(g, 1000, 520);
        Sliced(g, "panel_slate");
        var top = new Vector2(0.5f, 1);
        var name = Text(g.transform, "Name", "", 48, extraBold);
        Anchor(name.gameObject, new Vector2(0, 1), new Vector2(1, 1), top, new Vector2(0, -10), new Vector2(-80, 96)); // Changa satırı 1,84 em: kutu sığdırmazsa Ellipsis metni hiç çizmez
        name.overflowMode = TextOverflowModes.Ellipsis;
        var tags = Text(g.transform, "Tags", "", 30, bold);
        tags.color = SoftText;
        Anchor(tags.gameObject, new Vector2(0, 1), new Vector2(1, 1), top, new Vector2(0, -92), new Vector2(-80, 56));

        var taken = new GameObject[4];
        var initials = new TMP_Text[4];
        var names = new TMP_Text[4];
        var sit = new Button[4];
        for (int i = 0; i < 4; i++)
        {
            var slot = Child("Seat" + (i + 1), g.transform);
            Anchor(slot, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), SeatPos[i], new Vector2(310, 120));
            var t = Child("Taken", slot.transform);
            Fill(t, 0, 0, 0, 0);
            Sliced(t, "row_slate").raycastTarget = false;
            initials[i] = Avatar(t.transform, 80, 44);
            names[i] = Text(t.transform, "PlayerName", "", 34, bold);
            names[i].alignment = TextAlignmentOptions.MidlineLeft;
            Fill(names[i].gameObject, 104, 0, -14, 0);
            ShrinkToFit(names[i], 20);
            taken[i] = t;
            var b = ChoiceButton(green, slot.transform, "Sit", "OTUR", 220, 120, 48);
            sit[i] = b.GetComponent<Button>();
        }
        var so = new SerializedObject(g.AddComponent<RoomCard>());
        so.FindProperty("nameText").objectReferenceValue = name;
        so.FindProperty("tagsText").objectReferenceValue = tags;
        SetArray(so.FindProperty("taken"), taken);
        SetArray(so.FindProperty("initials"), initials);
        SetArray(so.FindProperty("names"), names);
        SetArray(so.FindProperty("sit"), sit);
        so.ApplyModifiedPropertiesWithoutUndo();
        return g;
    }

    // Kapatma butonunun kalıbı: 96 görsel, 120 dokunma alanı; renk ve işaret değişir
    static GameObject RoundButton(GameObject closePrefab, Transform parent, string name, string sprite, string label)
    {
        var b = Nested(closePrefab, parent, name);
        b.transform.Find("Visual").GetComponent<Image>().sprite = LoadSprite(sprite);
        b.GetComponentInChildren<TMP_Text>(true).text = label;
        return b;
    }

    // Pencere içi satır etiketi (Row/Label ile aynı: Bold 40, sola yaslı); üstte, x'ten başlar
    static void FormLabel(Transform parent, string name, string text, float x, float width)
    {
        var t = Text(parent, name, text, 40, bold);
        t.alignment = TextAlignmentOptions.MidlineLeft;
        Anchor(t.gameObject, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(x, 0), new Vector2(width, 120));
    }

    static void SetArray(SerializedProperty p, Object[] values)
    {
        p.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++) p.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
    }

    // Baş harfli daire: yerleşik yuvarlak görsel boyanır, sola çapalı
    static TMP_Text Avatar(Transform parent, float size, float fontSize)
    {
        var a = Child("Avatar", parent);
        var left = new Vector2(0, 0.5f);
        Anchor(a, left, left, left, new Vector2(12, 0), new Vector2(size, size));
        var img = a.AddComponent<Image>();
        img.sprite = KnobSprite;
        img.color = AvatarColor;
        img.raycastTarget = false;
        var t = Text(a.transform, "Initial", "", fontSize, extraBold);
        t.fontSharedMaterial = outline;
        Fill(t.gameObject, 0, 0, 0, 0);
        return t;
    }

    // Uzun ad küçülür, yine sığmazsa "..." ile kesilir
    static void ShrinkToFit(TMP_Text t, float min)
    {
        t.fontSizeMax = t.fontSize;
        t.fontSizeMin = min;
        t.enableAutoSizing = true;
        t.overflowMode = TextOverflowModes.Ellipsis;
    }
}
