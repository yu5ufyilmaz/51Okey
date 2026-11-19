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
    [Test] public void UiSfx_Plays_When_Button_Disables_Itself_In_OnClick()
    {
        Sfx.UseLibrary(AllIds()); Sfx.ClearRecent();
        var go = ButtonWith(true); var u = go.GetComponent<UiSfx>(); var e = new UnityEngine.EventSystems.PointerEventData(null);
        u.OnPointerDown(e);
        go.GetComponent<UnityEngine.UI.Button>().interactable = false; // onClick önce çalıp düğmeyi kapattı
        u.OnPointerClick(e);
        CollectionAssert.AreEqual(new[] { SfxId.UiClick }, Sfx.Recent);
        UnityEngine.Object.DestroyImmediate(go);
    }
    [Test] public void UiSfx_Is_Silent_When_Pressed_While_Disabled()
    {
        Sfx.UseLibrary(AllIds()); Sfx.ClearRecent();
        var go = ButtonWith(false); var u = go.GetComponent<UiSfx>(); var e = new UnityEngine.EventSystems.PointerEventData(null);
        u.OnPointerDown(e);
        go.GetComponent<UnityEngine.UI.Button>().interactable = true;
        u.OnPointerClick(e);
        Assert.IsEmpty(Sfx.Recent);
        UnityEngine.Object.DestroyImmediate(go);
    }
    [Test] public void UiSfx_Ignores_Right_Click()
    {
        Sfx.UseLibrary(AllIds()); Sfx.ClearRecent();
        var go = ButtonWith(true); var u = go.GetComponent<UiSfx>();
        var e = new UnityEngine.EventSystems.PointerEventData(null) { button = UnityEngine.EventSystems.PointerEventData.InputButton.Right };
        u.OnPointerDown(e); u.OnPointerClick(e);
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
    [Test] public void PenaltyFor_Mine_And_Others()
    {
        Assert.AreEqual(SfxId.PenaltyTaken, Sfx.PenaltyFor(2, 2));
        Assert.AreEqual(SfxId.PenaltyGiven, Sfx.PenaltyFor(3, 2));
    }
    [Test] public void FinishFor_Win_Lose_Draw()
    {
        Assert.AreEqual(SfxId.Win, Sfx.FinishFor(7, 7));
        Assert.AreEqual(SfxId.Lose, Sfx.FinishFor(4, 7));
        Assert.AreEqual(SfxId.Lose, Sfx.FinishFor(-1, 7));
    }
    [Test] public void CrossedBelow_Fires_Once_At_The_Edge()
    {
        Assert.IsTrue(Sfx.CrossedBelow(5.2f, 4.9f, 5f));
        Assert.IsFalse(Sfx.CrossedBelow(4.9f, 4.5f, 5f));
        Assert.IsFalse(Sfx.CrossedBelow(30f, 29f, 5f));
        Assert.IsFalse(Sfx.CrossedBelow(0.2f, 30f, 5f));
    }
    [Test] public void Burst_Of_Same_Event_Plays_Once()
    {
        Sfx.UseLibrary(AllIds()); Sfx.ClearRecent();
        for (int i = 0; i < 14; i++) Sfx.PlayAt(SfxId.MeldShown, 12.5f, Sfx.OtherVolume);
        Assert.AreEqual(1, Sfx.Recent.Count);
    }
    [Test] public void Gate_Allows_When_Time_Goes_Backwards()
    {
        var g = new SfxGate();
        Assert.IsTrue(g.Allow(1, 100f, 5f));
        Assert.IsTrue(g.Allow(1, 1f, 5f));
    }
    [Test] public void Play_Does_Not_Throw_With_Null_Entries()
    {
        var lib = ScriptableObject.CreateInstance<SfxLibrary>(); lib.entries = null;
        Sfx.UseLibrary(lib); LogAssert.ignoreFailingMessages = true;
        Assert.DoesNotThrow(() => Sfx.Play(SfxId.MeldShown));
        UnityEngine.Object.DestroyImmediate(lib);
    }
    [TearDown] public void Restore() { Sfx.UseLibrary(null); Sfx.ClearRecent(); LogAssert.ignoreFailingMessages = false; }
}
