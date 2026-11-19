using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

// UI kiti: giriş ekranı (MainMenu). Canvas'ın çocukları silinip kitten yeniden kurulur; köke ve ADSManagers'a dokunulmaz.
public static partial class UiKitBuilder
{
    const string MainMenuPath = "Assets/Osman/Scenes/MainMenu.unity";
    const string MenuBackgroundPath = "Assets/Sprites/okey_ui_demo/main_menu_background.png";
    const string LegalTextValue = "Devam ederek <link=\"terms\"><u>Kullanım Şartları</u></link> ve <link=\"privacy\"><u>Gizlilik Politikası</u></link>'nı kabul edersin";

    [MenuItem("Okey/UI Kit/Giriş Ekranını Kur")]
    public static void BuildLoginScreen()
    {
        if (EditorApplication.isPlaying) { Debug.LogWarning("[UiKitBuilder] Play modunda giriş ekranı kurulmaz."); return; }
        bold = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(BoldPath);
        extraBold = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(ExtraBoldPath);
        outline = AssetDatabase.LoadAssetAtPath<Material>(OutlinePath);
        var bg = AssetDatabase.LoadAssetAtPath<Sprite>(MenuBackgroundPath);
        var uiSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        var green = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabDir + "Btn_Green.prefab");
        var window = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabDir + "Window.prefab");
        if (bold == null || extraBold == null || outline == null || bg == null || uiSprite == null || green == null || window == null || LoadSprite("row_slate") == null)
        { Debug.LogWarning("[UiKitBuilder] Giriş ekranı için font, görsel ya da prefab eksik; sahneye dokunulmadı."); return; }

        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        var scene = EditorSceneManager.OpenScene(MainMenuPath, OpenSceneMode.Single);
        MainMenuUIHandler handler = null;
        foreach (var r in scene.GetRootGameObjects())
            if (r.name == "Canvas") handler = r.GetComponent<MainMenuUIHandler>();
        if (handler == null) { Debug.LogWarning("[UiKitBuilder] MainMenu'de MainMenuUIHandler'lı Canvas yok."); return; }
        var canvas = handler.transform;

        // İkinci çalıştırmada GameName yok: başlık önceki kurulumun Title'ından okunur
        var oldTitle = canvas.Find("MenuBackgorund/GameName") ?? canvas.Find("Safe/Title");
        var oldTitleText = oldTitle != null ? oldTitle.GetComponent<TMP_Text>() : null;
        var titleText = oldTitleText != null ? oldTitleText.text : null;
        if (string.IsNullOrEmpty(titleText)) { Debug.LogWarning("[UiKitBuilder] Başlık metni bulunamadı (GameName / Safe/Title); sahneye dokunulmadı."); return; }

        for (int i = canvas.childCount - 1; i >= 0; i--)
            Object.DestroyImmediate(canvas.GetChild(i).gameObject);

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

        var title = Text(safe.transform, "Title", titleText, 170, extraBold);
        title.fontSharedMaterial = outline;
        Anchor(title.gameObject, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -150), new Vector2(0, 220));

        var choice = Child("ChoicePanel", safe.transform);
        Size(choice, 720, 150 * 2 + 36);
        var vl = choice.AddComponent<VerticalLayoutGroup>();
        vl.spacing = 36;
        vl.childAlignment = TextAnchor.MiddleCenter;
        vl.childControlWidth = vl.childControlHeight = false;
        vl.childForceExpandWidth = vl.childForceExpandHeight = false;

        // Google butonu kit dışı: beyaz zemin, koyu yazı (Google marka kuralı), dış çizgisiz
        var google = Child("GoogleButton", choice.transform);
        Size(google, 720, 150);
        var gImg = google.AddComponent<Image>();
        gImg.sprite = uiSprite;
        gImg.type = Image.Type.Sliced;
        gImg.color = Color.white;
        var googleBtn = google.AddComponent<Button>();
        googleBtn.targetGraphic = gImg;
        google.AddComponent<UiPressScale>();
        var gLabel = Text(google.transform, "Label", "Google ile Giriş", 52, extraBold);
        gLabel.color = new Color32(0x2C, 0x33, 0x3C, 0xFF);
        Fill(gLabel.gameObject, 0, 0, 0, 0);

        var guest = Nested(green, choice.transform, "GuestButton");
        ((RectTransform)guest.transform).sizeDelta = new Vector2(720, 150);
        var guestLabel = guest.GetComponentInChildren<TMP_Text>(true);
        guestLabel.text = "Misafir Olarak Oyna";
        guestLabel.fontSize = 52;

        var legal = Text(safe.transform, "LegalText", LegalTextValue, 30, bold);
        legal.color = new Color32(0xCF, 0xD6, 0xDE, 0xFF);
        legal.raycastTarget = true; // bağlantıya dokunuş LegalLinksText'e gelsin
        Anchor(legal.gameObject, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 70), new Vector2(1600, 50));
        legal.gameObject.AddComponent<LegalLinksText>();

        var version = Text(safe.transform, "VersionText", "", 28, bold);
        version.color = new Color32(0xA9, 0xB3, 0xBD, 0xFF);
        version.alignment = TextAlignmentOptions.BottomRight;
        Anchor(version.gameObject, new Vector2(1, 0), new Vector2(1, 0), new Vector2(1, 0), new Vector2(-40, 30), new Vector2(400, 40));

        // İsim penceresi: kapatılamaz, X yok; gövde içeriğe göre kısaltıldı (başlık ve butonlar gövdeye çapalı)
        var namePanel = LoginWindow(window, canvas, "NamePanel", "İsmini seç");
        var nameContent = namePanel.transform.Find("Body/Content");
        var nameInput = NameInput(nameContent);
        var nameError = Text(nameContent, "NameError", "", 34, bold);
        nameError.color = new Color32(0xFF, 0x8F, 0x85, 0xFF);
        Anchor(nameError.gameObject, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -170), new Vector2(0, 60));
        var nameOk = Nested(green, namePanel.transform.Find("Body/Buttons"), "NameOkButton");
        nameOk.GetComponentInChildren<TMP_Text>(true).text = "TAMAM";

        var retryPanel = LoginWindow(window, canvas, "RetryPanel", "Bağlanılamadı");
        var message = Text(retryPanel.transform.Find("Body/Content"), "Message", "İnternet bağlantını kontrol edip tekrar dene.", 40, bold);
        message.color = new Color32(0xCF, 0xD6, 0xDE, 0xFF);
        message.textWrappingMode = TextWrappingModes.Normal;
        Fill(message.gameObject, 0, 0, 0, 0);
        var retry = Nested(green, retryPanel.transform.Find("Body/Buttons"), "RetryButton");
        retry.GetComponentInChildren<TMP_Text>(true).text = "TEKRAR DENE";

        handler.versionText = version;
        handler.choicePanel = choice;
        handler.guestButton = guest.GetComponent<Button>();
        handler.googleButton = googleBtn;
        handler.namePanel = namePanel;
        handler.nameInput = nameInput;
        handler.nameError = nameError;
        handler.nameOkButton = nameOk.GetComponent<Button>();
        handler.retryPanel = retryPanel;
        handler.retryButton = retry.GetComponent<Button>();
        UnityEventTools.AddPersistentListener(handler.guestButton.onClick, handler.PlayAsGuest);
        UnityEventTools.AddPersistentListener(googleBtn.onClick, handler.PlayWithGoogle);
        UnityEventTools.AddPersistentListener(handler.nameOkButton.onClick, handler.ConfirmName);
        UnityEventTools.AddPersistentListener(handler.retryButton.onClick, handler.RetryConnect);
        EditorUtility.SetDirty(handler);

        namePanel.SetActive(false);
        retryPanel.SetActive(false);
        // prefab örneklerinde koddan yapılan değişiklikler override olarak kaydedilsin
        foreach (var c in canvas.GetComponentsInChildren<Component>(true))
            if (PrefabUtility.IsPartOfPrefabInstance(c))
                PrefabUtility.RecordPrefabInstancePropertyModifications(c);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[UiKitBuilder] Giriş ekranı kuruldu: " + MainMenuPath);
    }

    static GameObject LoginWindow(GameObject windowPrefab, Transform parent, string name, string title)
    {
        var w = Nested(windowPrefab, parent, name);
        w.GetComponent<UiWindow>().closable = false;
        var body = w.transform.Find("Body");
        ((RectTransform)body).sizeDelta = new Vector2(1100, 600);
        body.Find("Title").GetComponent<TMP_Text>().text = title;
        body.Find("Close").gameObject.SetActive(false);
        return w;
    }

    // TMP_InputField'in beklediği düzen: kök (zemin) > Text Area (RectMask2D) > Placeholder + Text
    static TMP_InputField NameInput(Transform parent)
    {
        var g = Child("NameInput", parent);
        Anchor(g, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), Vector2.zero, new Vector2(900, 130));
        var img = Sliced(g, "row_slate");
        var area = Child("Text Area", g.transform);
        Fill(area, 40, 10, -40, -10);
        area.AddComponent<RectMask2D>();
        var placeholder = Text(area.transform, "Placeholder", "İsim", 48, bold);
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
}
