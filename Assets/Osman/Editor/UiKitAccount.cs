using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

// UI kiti: lobideki Profil ve Ayarlar pencereleri. Yalnızca Canvas/ProfilePanel ve Canvas/SettingsPanel silinip kitten kurulur;
// Canvas'ın diğer çocuklarına, ShopCanvas'a ve kök nesnelere dokunulmaz (kurulum sonunda nesne sayılarıyla denetlenir).
public static partial class UiKitBuilder
{
    static readonly Color32 MutedText = new Color32(0xA9, 0xB3, 0xBD, 0xFF);
    static readonly Color32 DangerText = new Color32(0xFF, 0x8F, 0x85, 0xFF);
    const float SettingsBodyHeight = 900f; // 20:9'da mantıksal yükseklik ~966; beş satır, başlık ve sürüm kaydırmasız sığar

    [MenuItem("Okey/UI Kit/Hesap Pencerelerini Kur")]
    public static void BuildAccountWindows() => Guarded(LobbyPath, BuildAccount);

    static void BuildAccount()
    {
        if (EditorApplication.isPlaying) { Debug.LogWarning("[UiKitBuilder] Play modunda hesap pencereleri kurulmaz."); return; }
        bold = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(BoldPath);
        extraBold = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(ExtraBoldPath);
        outline = AssetDatabase.LoadAssetAtPath<Material>(OutlinePath);
        var green = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabDir + "Btn_Green.prefab");
        var red = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabDir + "Btn_Red.prefab");
        var window = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabDir + "Window.prefab");
        var row = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabDir + "Row.prefab");
        var toggle = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabDir + "Toggle.prefab");
        if (bold == null || extraBold == null || outline == null || green == null || red == null || window == null || row == null
            || toggle == null || toggle.GetComponent<Toggle>() == null)
        { Debug.LogWarning("[UiKitBuilder] Hesap pencereleri için font ya da prefab eksik; sahneye dokunulmadı."); return; }
        foreach (var n in new[] { "row_slate", "panel_slate", "coin" })
            if (LoadSprite(n) == null) { Debug.LogWarning("[UiKitBuilder] Görsel yok: " + SpriteDir + n + ".png; sahneye dokunulmadı."); return; }
        foreach (var path in new[] { "Body", "Body/Title", "Body/Close", "Body/Content", "Body/Buttons" })
            if (window.transform.Find(path) == null) { Debug.LogWarning("[UiKitBuilder] Window prefab'ında " + path + " yok; sahneye dokunulmadı."); return; }
        if (row.transform.Find("Label") == null || row.transform.Find("Value") == null)
        { Debug.LogWarning("[UiKitBuilder] Row/Label ya da Row/Value yok; sahneye dokunulmadı."); return; }

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
        var oldProfile = canvas.Find("ProfilePanel");
        var oldSettings = canvas.Find("SettingsPanel");
        if (oldProfile == null || oldSettings == null)
        { Debug.LogWarning("[UiKitBuilder] ProfilePanel ya da SettingsPanel yok; sahneye dokunulmadı."); return; }

        var before = Snapshot(scene, canvas, oldProfile, oldSettings);
        deleting = true;
        Object.DestroyImmediate(oldProfile.gameObject);
        Object.DestroyImmediate(oldSettings.gameObject);

        var profile = BuildProfile(window, green, red, canvas, account);
        var settings = BuildSettings(window, row, toggle, canvas, account);
        account.profilePanel = profile;
        account.settingsPanel = settings;
        EditorUtility.SetDirty(account);
        profile.SetActive(false);
        settings.SetActive(false);
        foreach (var w in new[] { profile, settings })
            foreach (var c in w.GetComponentsInChildren<Component>(true))
                if (PrefabUtility.IsPartOfPrefabInstance(c))
                    PrefabUtility.RecordPrefabInstancePropertyModifications(c);

        // Denetim: başka hiçbir şey değişmedi, pencereler son iki çocuk, LobbyAccountUI'nin tüm başvuruları dolu
        var after = Snapshot(scene, canvas, profile.transform, settings.transform);
        if (string.Join("\n", before) != string.Join("\n", after))
            throw new System.Exception("Kurulum dışı nesne sayısı değişti:\nönce\n" + string.Join("\n", before) + "\nsonra\n" + string.Join("\n", after));
        if (profile.transform.GetSiblingIndex() != canvas.childCount - 2 || settings.transform.GetSiblingIndex() != canvas.childCount - 1)
            throw new System.Exception("ProfilePanel ve SettingsPanel Canvas'ın son iki çocuğu değil.");
        var it = new SerializedObject(account).GetIterator();
        it.Next(true);
        while (it.NextVisible(false))
            if (it.propertyType == SerializedPropertyType.ObjectReference && it.objectReferenceValue == null)
                throw new System.Exception("LobbyAccountUI." + it.propertyPath + " boş.");

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[UiKitBuilder] Hesap pencereleri kuruldu: " + LobbyPath);
    }

    // Kurulum dışında kalan her alt ağacın (Canvas'ın diğer çocukları, diğer kök nesneler) nesne ve bileşen sayısı
    static List<string> Snapshot(UnityEngine.SceneManagement.Scene scene, Transform canvas, Transform profile, Transform settings)
    {
        var list = new List<string>();
        foreach (Transform c in canvas)
            if (c != profile && c != settings) list.Add(Count("Canvas/" + c.name, c));
        foreach (var r in scene.GetRootGameObjects())
            if (r.transform != canvas) list.Add(Count(r.name, r.transform));
        return list;
    }

    static string Count(string label, Transform t) =>
        label + " nesne=" + t.GetComponentsInChildren<Transform>(true).Length + " bileşen=" + t.GetComponentsInChildren<Component>(true).Length;

    static GameObject BuildProfile(GameObject window, GameObject green, GameObject red, Transform canvas, LobbyAccountUI account)
    {
        var win = KitWindow(window, canvas, "ProfilePanel", "PROFİL", new Vector2(1100, 760));
        var uw = win.GetComponent<UiWindow>();
        uw.closeOnBack = false;
        UnityEventTools.AddPersistentListener(uw.onBack, account.CloseProfile);
        var content = win.transform.Find("Body/Content");
        var left = new Vector2(0, 0.5f);
        var right = new Vector2(1, 0.5f);

        var initial = Avatar(content, 220, 110);
        ((RectTransform)initial.transform.parent).anchoredPosition = Vector2.zero;

        var info = Child("Info", content);
        Fill(info, 260, 0, 0, 0);
        var vl = info.AddComponent<VerticalLayoutGroup>();
        vl.spacing = 14;
        vl.childAlignment = TextAnchor.MiddleLeft;
        vl.childControlWidth = true;
        vl.childForceExpandWidth = true;
        vl.childControlHeight = vl.childForceExpandHeight = false;

        // İsim: değiştirebilen için kutu + KAYDET, değiştiremeyen için düz isim + ipucu (Refresh seçer)
        var nameRow = Child("NameRow", info.transform);
        Size(nameRow, 0, 120);
        var nameInput = TextInput(nameRow.transform, "NameInput", "İsim", new Vector2(480, 120));
        Fill(nameInput.gameObject, 0, 0, -240, 0);
        var nameSave = ChoiceButton(green, nameRow.transform, "NameSaveButton", "KAYDET", 220, 120, 44);
        Anchor(nameSave, right, right, right, Vector2.zero, new Vector2(220, 120));
        var nameText = Text(nameRow.transform, "NameText", "", 48, extraBold);
        nameText.alignment = TextAlignmentOptions.MidlineLeft;
        Anchor(nameText.gameObject, new Vector2(0, 1), Vector2.one, new Vector2(0.5f, 1), Vector2.zero, new Vector2(0, 76));
        ShrinkToFit(nameText, 32);
        var nameHint = Text(nameRow.transform, "NameHint", "İsim seçmek için Google'a bağlan", 30, bold);
        nameHint.color = MutedText;
        nameHint.alignment = TextAlignmentOptions.MidlineLeft;
        Anchor(nameHint.gameObject, Vector2.zero, new Vector2(1, 0), new Vector2(0.5f, 0), Vector2.zero, new Vector2(0, 44));

        var nameError = Text(info.transform, "NameError", "", 30, bold);
        nameError.color = DangerText;
        nameError.alignment = TextAlignmentOptions.MidlineLeft;
        Size(nameError.gameObject, 0, 44);

        var levelRow = Child("LevelRow", info.transform);
        Size(levelRow, 0, 96);
        var levelText = Text(levelRow.transform, "LevelText", "", 40, extraBold);
        levelText.alignment = TextAlignmentOptions.MidlineLeft;
        Anchor(levelText.gameObject, new Vector2(0, 1), new Vector2(0.5f, 1), new Vector2(0, 1), Vector2.zero, new Vector2(0, 60));
        var xpText = Text(levelRow.transform, "XpText", "", 32, bold);
        xpText.alignment = TextAlignmentOptions.MidlineRight;
        Anchor(xpText.gameObject, new Vector2(0.5f, 1), Vector2.one, new Vector2(1, 1), Vector2.zero, new Vector2(0, 60));
        var bar = Child("XpBarBack", levelRow.transform);
        Anchor(bar, Vector2.zero, new Vector2(1, 0), new Vector2(0.5f, 0), Vector2.zero, new Vector2(0, 24));
        Pill(bar, new Color32(0x3C, 0x46, 0x52, 0xFF)).raycastTarget = false;
        // Dolgulu görsel yalnız maske (görünmez); sarı hap onun içinde dolgu oranında kesilir. Gerilen yerleşik görselin
        // saydam kenarı (~32 birim) zeminin dışında kalsın diye maske yanlara taşırılır.
        // shortcut: taşma yüzünden görünen oran fillAmount'tan en çok %5 sapar; düz görsel gelince taşma kalkar
        var fillGo = Child("XpBar", bar.transform);
        Fill(fillGo, -36, -6, 36, 6);
        var xpBar = fillGo.AddComponent<Image>();
        xpBar.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Background.psd");
        xpBar.type = Image.Type.Filled;
        xpBar.fillMethod = Image.FillMethod.Horizontal;
        xpBar.fillOrigin = (int)Image.OriginHorizontal.Left;
        xpBar.fillAmount = 0;
        xpBar.raycastTarget = false;
        fillGo.AddComponent<Mask>().showMaskGraphic = false;
        var fillPill = Child("Fill", fillGo.transform);
        Fill(fillPill, 36, 6, -36, -6); // zeminle aynı alan
        Pill(fillPill, new Color32(0xF2, 0xC9, 0x4C, 0xFF)).raycastTarget = false;

        var coinRow = Child("CoinRow", info.transform);
        Size(coinRow, 0, 70);
        var icon = Child("CoinIcon", coinRow.transform);
        Anchor(icon, left, left, left, Vector2.zero, new Vector2(64, 64));
        var iconImg = icon.AddComponent<Image>();
        iconImg.sprite = LoadSprite("coin");
        iconImg.preserveAspect = true;
        iconImg.raycastTarget = false;
        var coinText = Text(coinRow.transform, "CoinText", "", 40, extraBold);
        coinText.alignment = TextAlignmentOptions.MidlineLeft;
        Anchor(coinText.gameObject, left, left, left, new Vector2(80, 0), new Vector2(300, 70));
        var accountText = Text(coinRow.transform, "AccountText", "", 32, bold);
        accountText.color = MutedText;
        accountText.alignment = TextAlignmentOptions.MidlineRight;
        Anchor(accountText.gameObject, right, right, right, Vector2.zero, new Vector2(340, 70));

        // Baş harf profilin kendi isminden okunur: lobi üst barına başvuru olmaz
        var initialSo = new SerializedObject(initial.transform.parent.gameObject.AddComponent<ChipInitial>());
        initialSo.FindProperty("source").objectReferenceValue = nameText;
        initialSo.FindProperty("target").objectReferenceValue = initial;
        initialSo.ApplyModifiedPropertiesWithoutUndo();

        var buttons = win.transform.Find("Body/Buttons");
        var link = ChoiceButton(green, buttons, "LinkGoogleButton", "Google'a Bağla, ilerlemeni koru", 620, 130, 40);
        ShrinkToFit(link.GetComponentInChildren<TMP_Text>(true), 28);
        var signOut = ChoiceButton(red, buttons, "SignOutButton", "ÇIKIŞ YAP", 360, 130, 48);

        account.nameInput = nameInput;
        account.nameSaveButton = nameSave.GetComponent<Button>();
        account.nameError = nameError;
        account.nameText = nameText;
        account.nameHint = nameHint;
        account.accountText = accountText;
        account.levelText = levelText;
        account.xpText = xpText;
        account.coinText = coinText;
        account.xpBar = xpBar;
        account.linkGoogleButton = link.GetComponent<Button>();
        account.signOutButton = signOut.GetComponent<Button>();
        UnityEventTools.AddPersistentListener(account.nameSaveButton.onClick, account.SaveName);
        UnityEventTools.AddPersistentListener(account.linkGoogleButton.onClick, account.LinkGoogle);
        UnityEventTools.AddPersistentListener(account.signOutButton.onClick, account.SignOut);
        return win;
    }

    static GameObject BuildSettings(GameObject window, GameObject row, GameObject toggle, Transform canvas, LobbyAccountUI account)
    {
        var win = KitWindow(window, canvas, "SettingsPanel", "AYARLAR", new Vector2(1100, SettingsBodyHeight));
        var uw = win.GetComponent<UiWindow>();
        uw.closeOnBack = false;
        UnityEventTools.AddPersistentListener(uw.onBack, account.CloseSettings);
        var body = win.transform.Find("Body");
        // Butonsuz pencere: içerik alttaki sürüm yazısına kadar iner; beş satır (5x120 + 4x14 = 656) kaydırmasız sığar
        var content = body.Find("Content");
        Fill(content.gameObject, 60, 84, -60, -150);
        var list = content.gameObject;
        var vl = list.AddComponent<VerticalLayoutGroup>();
        vl.spacing = 14;
        vl.childAlignment = TextAnchor.UpperCenter;
        vl.childControlWidth = true;
        vl.childForceExpandWidth = true;
        vl.childControlHeight = vl.childForceExpandHeight = false;

        // Ses ve Müzik tek satırda, yarım genişlikte iki hücre
        var audio = Child("AudioRow", list.transform);
        Size(audio, 0, 120);
        var hl = audio.AddComponent<HorizontalLayoutGroup>();
        hl.spacing = 14;
        hl.childControlWidth = true;
        hl.childForceExpandWidth = true;
        hl.childControlHeight = hl.childForceExpandHeight = false;
        var sound = ToggleRow(row, toggle, audio.transform, "SoundRow", "Ses");
        var music = ToggleRow(row, toggle, audio.transform, "MusicRow", "Müzik");
        var privacy = LinkRow(row, list.transform, "PrivacyRow", "Gizlilik Politikası");
        var terms = LinkRow(row, list.transform, "TermsRow", "Kullanım Şartları");
        var support = LinkRow(row, list.transform, "SupportRow", "Destek");
        var delete = LinkRow(row, list.transform, "DeleteRow", "Hesabımı Sil");
        delete.transform.Find("Label").GetComponent<TMP_Text>().color = DangerText;

        var version = Text(body, "VersionText", "", 30, bold);
        version.color = MutedText;
        Anchor(version.gameObject, Vector2.zero, new Vector2(1, 0), new Vector2(0.5f, 0), new Vector2(0, 24), new Vector2(-120, 50));

        account.soundToggle = sound;
        account.musicToggle = music;
        account.versionText = version;
        account.privacyRow = privacy;
        account.termsRow = terms;
        account.supportRow = support;
        account.deleteRow = delete;
        UnityEventTools.AddPersistentListener(sound.onValueChanged, account.OnSoundToggle);
        UnityEventTools.AddPersistentListener(music.onValueChanged, account.OnMusicToggle);
        UnityEventTools.AddPersistentListener(privacy.GetComponent<Button>().onClick, account.OpenPrivacy);
        UnityEventTools.AddPersistentListener(terms.GetComponent<Button>().onClick, account.OpenTerms);
        UnityEventTools.AddPersistentListener(support.GetComponent<Button>().onClick, account.OpenSupport);
        UnityEventTools.AddPersistentListener(delete.GetComponent<Button>().onClick, account.DeleteAccount);
        return win;
    }

    static GameObject KitRow(GameObject row, Transform parent, string name, string label)
    {
        var r = Nested(row, parent, name);
        ((RectTransform)r.transform).sizeDelta = new Vector2(980, 120);
        r.transform.Find("Label").GetComponent<TMP_Text>().text = label;
        return r;
    }

    static Toggle ToggleRow(GameObject row, GameObject toggle, Transform parent, string name, string label)
    {
        var r = KitRow(row, parent, name, label);
        var right = new Vector2(1, 0.5f);
        var t = Nested(toggle, r.transform, name.Replace("Row", "Toggle"));
        Anchor(t, right, right, right, new Vector2(-20, 0), new Vector2(150, 120));
        return t.GetComponent<Toggle>();
    }

    // Satırın tamamı dokunulur; sağda ">"
    static GameObject LinkRow(GameObject row, Transform parent, string name, string label)
    {
        var r = KitRow(row, parent, name, label);
        r.transform.Find("Value").GetComponent<TMP_Text>().text = ">";
        r.AddComponent<Button>().targetGraphic = r.GetComponent<Image>();
        return r;
    }
}
