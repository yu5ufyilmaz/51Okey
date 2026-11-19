using System.Linq;
using UnityEditor;
using UnityEngine;

public static class SfxSetup
{
    const string AssetPath = "Assets/Resources/SfxLibrary.asset";
    const string AudioDir = "Assets/Osman/Audio/Sfx/";

    public static readonly (SfxId id, float volume, float minInterval, float pitchJitter, string[] files)[] Defaults =
    {
        (SfxId.TilePickUp, 0.7f, 0.05f, 0.06f, new string[0]),
        (SfxId.TileDrop, 0.8f, 0.05f, 0.06f, new string[0]),
        (SfxId.TileSnapBack, 0.6f, 0.05f, 0.06f, new string[0]),
        (SfxId.TileDraw, 1f, 0.05f, 0.06f, new string[0]),
        (SfxId.TileTakeSide, 1f, 0.05f, 0.06f, new string[0]),
        (SfxId.TileDiscard, 1f, 0.05f, 0.06f, new string[0]),
        (SfxId.TilesDeal, 1f, 0.5f, 0f, new string[0]),
        (SfxId.OpenRuns, 1f, 0.3f, 0f, new string[0]),
        (SfxId.OpenPairs, 1f, 0.3f, 0f, new string[0]),
        (SfxId.MeldShown, 0.8f, 0.3f, 0f, new string[0]),
        (SfxId.TileProcess, 0.9f, 0.3f, 0.06f, new string[0]),
        (SfxId.TilesCollect, 0.9f, 0.3f, 0f, new string[0]),
        (SfxId.PutBack, 0.9f, 0.05f, 0.06f, new string[0]),
        (SfxId.Error, 0.8f, 0.2f, 0f, new string[0]),
        (SfxId.PenaltyTaken, 1f, 0.3f, 0f, new string[0]),
        (SfxId.PenaltyGiven, 0.9f, 0.3f, 0f, new string[0]),
        (SfxId.MyTurn, 0.9f, 0.5f, 0f, new string[0]),
        (SfxId.TimeLow, 0.9f, 1f, 0f, new string[0]),
        (SfxId.Win, 1f, 1f, 0f, new string[0]),
        (SfxId.Lose, 1f, 1f, 0f, new string[0]),
        (SfxId.SeatTaken, 0.7f, 0.3f, 0f, new string[0]),
        (SfxId.UiClick, 0.7f, 0.05f, 0f, new string[0]),
        (SfxId.UiClose, 0.7f, 0.05f, 0f, new string[0]),
        (SfxId.UiToggle, 0.7f, 0.05f, 0f, new string[0]),
        (SfxId.UiPlayNow, 0.9f, 0.2f, 0f, new string[0]),
        (SfxId.UiCreateTable, 0.9f, 0.2f, 0f, new string[0]),
        (SfxId.UiPlayBots, 0.9f, 0.2f, 0f, new string[0]),
        (SfxId.UiEnterRoom, 0.9f, 0.2f, 0f, new string[0]),
    };

    [MenuItem("Okey/Ses/Ses Listesini Kur")]
    public static void Build()
    {
        var lib = AssetDatabase.LoadAssetAtPath<SfxLibrary>(AssetPath);
        if (lib == null)
        {
            if (!AssetDatabase.IsValidFolder("Assets/Resources")) AssetDatabase.CreateFolder("Assets", "Resources");
            lib = ScriptableObject.CreateInstance<SfxLibrary>();
            AssetDatabase.CreateAsset(lib, AssetPath);
        }
        foreach (var d in Defaults)
        {
            if (lib.Find(d.id) != null) continue; // var olan satıra dokunma
            var clips = d.files.Select(f => AssetDatabase.LoadAssetAtPath<AudioClip>(AudioDir + f)).Where(c => c != null).ToArray();
            lib.entries.Add(new SfxLibrary.Entry { id = d.id, clips = clips, volume = d.volume, minInterval = d.minInterval, pitchJitter = d.pitchJitter });
        }
        EditorUtility.SetDirty(lib);
        AssetDatabase.SaveAssets();
    }
}
