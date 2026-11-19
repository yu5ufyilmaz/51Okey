using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

// UI kiti kurulum araçları. Bu bölüm: Changa yazı tipi (Türkçe harfler) ve sahnelerdeki TMP metinlerin geçişi.
public static partial class UiKitBuilder
{
    const string FontDir = "Assets/Fonts/";
    const string BoldPath = FontDir + "Changa-Bold SDF.asset";
    const string ExtraBoldPath = FontDir + "Changa-ExtraBold SDF.asset";
    public const string OutlinePath = FontDir + "Changa-ExtraBold SDF Outline.mat";
    const string Turkish = "ğĞşŞıİçÇöÖüÜ";

    static readonly string[] Scenes =
    {
        "Assets/Osman/Scenes/MainMenu.unity",
        "Assets/Osman/Scenes/LobbyMenu.unity",
        "Assets/Osman/Scenes/Table.unity",
    };
    const string RoomItemPrefab = "Assets/Resources/RoomItem.prefab";

    [MenuItem("Okey/UI Kit/Fontları Kur")]
    public static void BuildFonts()
    {
        BuildFont(FontDir + "Changa-Bold.ttf", BoldPath);
        BuildFont(FontDir + "Changa-ExtraBold.ttf", ExtraBoldPath);
        BuildOutlineMaterial();
        AssetDatabase.SaveAssets();
    }

    // Eski ChangaOne dış çizgili materyalin değerleri; yeni font asset'inin materyali kopyalanıp üzerine yazılır (_GradientScale vb. fonta özgü kalır)
    static void BuildOutlineMaterial()
    {
        var fa = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(ExtraBoldPath);
        if (fa == null || File.Exists(OutlinePath)) return;
        var m = new Material(fa.material) { name = "Changa-ExtraBold SDF Outline" };
        m.SetFloat("_FaceDilate", 0.18f);
        m.SetFloat("_OutlineWidth", 0.22f);
        m.SetFloat("_OutlineSoftness", 0f);
        m.SetColor("_OutlineColor", new Color(0, 0, 0, 1));
        m.SetFloat("_UnderlayDilate", 0.25f);
        m.SetFloat("_UnderlayOffsetX", 0.35f);
        m.SetFloat("_UnderlayOffsetY", -0.55f);
        m.SetFloat("_UnderlaySoftness", 0.1f);
        m.SetColor("_UnderlayColor", new Color(0, 0, 0, 0.8f));
        m.EnableKeyword("UNDERLAY_ON");
        AssetDatabase.CreateAsset(m, OutlinePath);
        Debug.Log("[UiKitBuilder] Dış çizgili materyal üretildi: " + OutlinePath);
    }

    static void BuildFont(string ttfPath, string assetPath)
    {
        if (File.Exists(assetPath)) return; // varsa yeniden üretme
        var font = AssetDatabase.LoadAssetAtPath<Font>(ttfPath);
        if (font == null) { Debug.LogWarning("[UiKitBuilder] Font dosyası yok: " + ttfPath); return; }
        var fa = TMP_FontAsset.CreateFontAsset(font, 90, 9, GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic);
        fa.TryAddCharacters(Turkish, out _);
        AssetDatabase.CreateAsset(fa, assetPath);
        // atlas dokusu ve materyal ana varlığın alt nesnesi olarak kaydedilmeli
        fa.material.name = fa.name + " Material";
        fa.atlasTexture.name = fa.name + " Atlas";
        AssetDatabase.AddObjectToAsset(fa.material, fa);
        AssetDatabase.AddObjectToAsset(fa.atlasTexture, fa);
        EditorUtility.SetDirty(fa);
        Debug.Log("[UiKitBuilder] Font varlığı üretildi: " + assetPath);
    }

    [MenuItem("Okey/UI Kit/Fontları Değiştir")]
    public static void SwapFonts()
    {
        if (EditorApplication.isPlaying) { Debug.LogWarning("[UiKitBuilder] Play modunda font değiştirilmez."); return; }
        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(ExtraBoldPath);
        if (font == null) { Debug.Log("[UiKitBuilder] Önce BuildFonts çalıştırılmalı."); return; }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        foreach (var path in Scenes)
        {
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            int n = 0;
            foreach (var t in Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (t.font == font) continue;
                t.font = font; // font atamak materyali o fontun varsayılanına sıfırlar; fontSize ve renk korunur
                EditorUtility.SetDirty(t);
                n++;
            }
            // giriş alanlarının kendi global font başvurusu da ChangaOne'ı tutuyor
            foreach (var f in Object.FindObjectsByType<TMP_InputField>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (f.fontAsset != font) { f.fontAsset = font; EditorUtility.SetDirty(f); }
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[UiKitBuilder] {scene.name}: {n} metin değişti");
        }

        var root = PrefabUtility.LoadPrefabContents(RoomItemPrefab);
        int p = 0;
        foreach (var t in root.GetComponentsInChildren<TMP_Text>(true))
        {
            if (t.font == font) continue;
            t.font = font;
            p++;
        }
        // RoomItem'daki oda sayacı eski UI.Text; ChangaOne .ttf'i bırakıp Changa'ya geçir
        var legacyFont = AssetDatabase.LoadAssetAtPath<Font>(FontDir + "Changa-ExtraBold.ttf");
        foreach (var t in root.GetComponentsInChildren<UnityEngine.UI.Text>(true))
            if (t.font != legacyFont) { t.font = legacyFont; p++; }
        PrefabUtility.SaveAsPrefabAsset(root, RoomItemPrefab);
        PrefabUtility.UnloadPrefabContents(root);
        Debug.Log($"[UiKitBuilder] RoomItem.prefab: {p} metin değişti");
        AssetDatabase.SaveAssets();
    }
}
