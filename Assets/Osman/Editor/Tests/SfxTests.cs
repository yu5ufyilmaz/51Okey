using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public class SfxTests
{
    [Test] public void Gate_Blocks_Repeat_Inside_Interval_And_Allows_After()
    {
        var g = new SfxGate();
        Assert.IsTrue(g.Allow(5, 10f, 0.3f));
        Assert.IsFalse(g.Allow(5, 10.1f, 0.3f));
        Assert.IsTrue(g.Allow(5, 10.31f, 0.3f));
    }
    [Test] public void Gate_Ids_Do_Not_Block_Each_Other()
    {
        var g = new SfxGate();
        Assert.IsTrue(g.Allow(5, 10f, 0.3f));
        Assert.IsTrue(g.Allow(6, 10f, 0.3f));
    }
    [Test] public void Gate_Zero_Interval_Still_Blocks_Same_Timestamp()
    {
        var g = new SfxGate();
        Assert.IsTrue(g.Allow(1, 3f, 0f));
        Assert.IsFalse(g.Allow(1, 3f, 0f));   // aynı karede ikinci çağrı
        Assert.IsTrue(g.Allow(1, 3.02f, 0f));
    }
    [Test] public void Gate_Reset_Forgets()
    {
        var g = new SfxGate(); g.Allow(1, 3f, 5f); g.Reset();
        Assert.IsTrue(g.Allow(1, 3.1f, 5f));
    }
    [Test] public void SfxId_Numbers_Are_Fixed()
    {
        Assert.AreEqual(0, (int)SfxId.TilePickUp);
        Assert.AreEqual(5, (int)SfxId.TileDiscard);
        Assert.AreEqual(14, (int)SfxId.PenaltyTaken);
        Assert.AreEqual(21, (int)SfxId.UiClick);
        Assert.AreEqual(27, (int)SfxId.UiEnterRoom);
        Assert.AreEqual(28, Enum.GetValues(typeof(SfxId)).Length);
    }
    [Test] public void Play_With_Missing_Entry_Does_Not_Throw()
    {
        var lib = ScriptableObject.CreateInstance<SfxLibrary>(); lib.entries = new List<SfxLibrary.Entry>();
        Sfx.UseLibrary(lib); Sfx.ClearRecent(); LogAssert.ignoreFailingMessages = true;
        Assert.DoesNotThrow(() => Sfx.Play(SfxId.TileDraw));
        Assert.IsEmpty(Sfx.Recent);
    }
    [Test] public void Play_With_Entry_But_No_Clip_Is_Recorded_And_Silent()
    {
        var lib = ScriptableObject.CreateInstance<SfxLibrary>();
        lib.entries = new List<SfxLibrary.Entry> { new SfxLibrary.Entry { id = SfxId.TileDraw, clips = new AudioClip[] { null } } };
        Sfx.UseLibrary(lib); Sfx.ClearRecent();
        Assert.DoesNotThrow(() => Sfx.Play(SfxId.TileDraw));
        CollectionAssert.AreEqual(new[] { SfxId.TileDraw }, Sfx.Recent);
    }
    [Test] public void Recent_Keeps_Last_32()
    {
        var lib = ScriptableObject.CreateInstance<SfxLibrary>();
        lib.entries = new List<SfxLibrary.Entry> { new SfxLibrary.Entry { id = SfxId.UiClick, minInterval = 0f } };
        Sfx.UseLibrary(lib); Sfx.ClearRecent();
        for (int i = 0; i < 40; i++) Sfx.PlayAt(SfxId.UiClick, i);
        Assert.AreEqual(32, Sfx.Recent.Count);
    }
    [Test] public void Project_Library_Has_Exactly_One_Entry_Per_Id()
    {
        var lib = Resources.Load<SfxLibrary>("SfxLibrary");
        Assert.IsNotNull(lib);
        foreach (SfxId id in Enum.GetValues(typeof(SfxId)))
            Assert.AreEqual(1, lib.entries.Count(e => e.id == id), id.ToString());
    }
    static GameObject ButtonWith(bool interactable)
    {
        var go = new GameObject("b", typeof(RectTransform), typeof(UnityEngine.UI.Image), typeof(UnityEngine.UI.Button), typeof(UiSfx));
        go.GetComponent<UnityEngine.UI.Button>().interactable = interactable; return go;
    }
    static SfxLibrary AllIds()
    {
        var lib = ScriptableObject.CreateInstance<SfxLibrary>(); lib.entries = new List<SfxLibrary.Entry>();
        foreach (SfxId id in Enum.GetValues(typeof(SfxId))) lib.entries.Add(new SfxLibrary.Entry { id = id, minInterval = 0f });
        return lib;
    }
    [Test] public void UiSfx_Plays_Its_Id_On_Click()
    {
        Sfx.UseLibrary(AllIds()); Sfx.ClearRecent();
        var go = ButtonWith(true); go.GetComponent<UiSfx>().id = SfxId.UiPlayNow;
        go.GetComponent<UiSfx>().OnPointerClick(new UnityEngine.EventSystems.PointerEventData(null));
        CollectionAssert.AreEqual(new[] { SfxId.UiPlayNow }, Sfx.Recent);
        UnityEngine.Object.DestroyImmediate(go);
    }
    [Test] public void UiSfx_Is_Silent_On_Disabled_Button()
    {
        Sfx.UseLibrary(AllIds()); Sfx.ClearRecent();
        var go = ButtonWith(false);
        go.GetComponent<UiSfx>().OnPointerClick(new UnityEngine.EventSystems.PointerEventData(null));
        Assert.IsEmpty(Sfx.Recent);
        UnityEngine.Object.DestroyImmediate(go);
    }
    [TestCase("Btn_Green", SfxId.UiClick)] [TestCase("Btn_Orange", SfxId.UiClick)] [TestCase("Btn_Teal", SfxId.UiClick)]
    [TestCase("Btn_Red", SfxId.UiClick)] [TestCase("Btn_Close", SfxId.UiClose)] [TestCase("Toggle", SfxId.UiToggle)]
    public void Kit_Prefab_Has_UiSfx(string prefab, SfxId expected)
    {
        var go = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Osman/Prefabs/UI/" + prefab + ".prefab");
        var s = go.GetComponentInChildren<UiSfx>(true);
        Assert.IsNotNull(s, prefab); Assert.AreEqual(expected, s.id);
    }
    [TearDown] public void Restore() { Sfx.UseLibrary(null); Sfx.ClearRecent(); LogAssert.ignoreFailingMessages = false; }
}
