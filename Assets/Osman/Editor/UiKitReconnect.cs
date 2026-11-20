using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Nötr yeniden bağlanma sahnesi (ReconnectFlow): yalnız Main Camera; katman UIRoot'tan gelir. Sahne varsa yeniden yazılmaz (ikinci çalıştırma = değişiklik yok).
public static class UiKitReconnect
{
    const string ScenePath = "Assets/Osman/Scenes/Reconnect.unity";

    [MenuItem("Okey/Sahne/Yeniden Bağlanma Sahnesi")]
    public static void Build()
    {
        if (EditorApplication.isPlaying) { Debug.LogWarning("[UiKitReconnect] Play modunda kurulmaz."); return; }
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null)
        {
            var prev = EditorSceneManager.GetActiveScene();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            EditorSceneManager.SetActiveScene(scene); // kamera açık sahneyi kirletmesin
            var cam = new GameObject("Main Camera") { tag = "MainCamera" };
            var c = cam.AddComponent<Camera>();
            c.clearFlags = CameraClearFlags.SolidColor;
            c.backgroundColor = Color.black;
            c.orthographic = true;
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorSceneManager.SetActiveScene(prev);
            EditorSceneManager.CloseScene(scene, true);
            Debug.Log("[UiKitReconnect] sahne yazıldı: " + ScenePath);
        }
        var list = EditorBuildSettings.scenes.ToList();
        if (!list.Any(s => s.path == ScenePath))
        {
            list.Add(new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = list.ToArray();
            AssetDatabase.SaveAssets(); // ProjectSettings/EditorBuildSettings.asset diske
            Debug.Log("[UiKitReconnect] Build Settings'e eklendi");
        }
    }
}
