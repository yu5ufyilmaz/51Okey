using System;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public class LocTests
{
    const string Csv = "key,tr,en,de\nlang.name,Türkçe,English,Deutsch\na.hi,Merhaba,Hello,\na.n,{0} el,{0} rounds,\na.bad,{0} ve {1},{0} and {,\n";

    [TearDown] public void TearDown() { LogAssert.ignoreFailingMessages = false; Loc.Load(null, null); }

    [Test] public void T_Returns_Selected_Language() { Loc.Load(Csv, "en"); Assert.AreEqual("Hello", Loc.T("a.hi")); }
    [Test] public void Empty_Cell_Falls_Back_To_Turkish() { Loc.Load(Csv, "de"); Assert.AreEqual("Merhaba", Loc.T("a.hi")); }
    [Test] public void Unknown_Key_Returns_Key() { Loc.Load(Csv, "en"); LogAssert.ignoreFailingMessages = true; Assert.AreEqual("x.y", Loc.T("x.y")); }
    [Test] public void Args_Are_Formatted() { Loc.Load(Csv, "en"); Assert.AreEqual("3 rounds", Loc.T("a.n", 3)); }
    [Test] public void Broken_Format_Returns_Raw_Text_Without_Throwing()
    {
        Loc.Load(Csv, "en"); LogAssert.ignoreFailingMessages = true;
        Assert.AreEqual("{0} and {", Loc.T("a.bad", 1, 2));
    }
    [Test] public void Too_Few_Args_Returns_Raw_Text()
    {
        Loc.Load(Csv, "tr"); LogAssert.ignoreFailingMessages = true;
        Assert.AreEqual("{0} ve {1}", Loc.T("a.bad", 1));
    }
    [Test] public void Setting_Language_Fires_Changed_Once_And_Ignores_Unknown()
    {
        Loc.Load(Csv, "tr"); int n = 0; Action h = () => n++; Loc.Changed += h;
        Loc.Language = "en"; Loc.Language = "en"; Loc.Language = "xx";
        Loc.Changed -= h;
        Assert.AreEqual(1, n); Assert.AreEqual("en", Loc.Language);
    }
    [Test] public void NameOf_Reads_Lang_Name_Row() { Loc.Load(Csv, "tr"); Assert.AreEqual("Deutsch", Loc.NameOf("de")); }
    [Test] public void Culture_Follows_Language() { Loc.Load(Csv, "tr"); Assert.AreEqual("İ", "i".ToUpper(Loc.Culture)); Loc.Language = "en"; Assert.AreEqual("I", "i".ToUpper(Loc.Culture)); }

    static readonly string[] TrEn = { "tr", "en" };
    [TestCase("en", "tr", "en")]
    [TestCase("", "tr", "tr")]
    [TestCase("", "de", "en")]
    [TestCase("fr", "tr", "tr")]
    [TestCase(null, null, "en")]
    public void Pick_Cases(string saved, string system, string expected) => Assert.AreEqual(expected, Loc.Pick(saved, system, TrEn));
    [Test] public void Pick_Without_English_Uses_Turkish() => Assert.AreEqual("tr", Loc.Pick("", "de", new[] { "tr" }));
    [Test] public void CodeOf_Maps_Known_Languages()
    {
        Assert.AreEqual("tr", Loc.CodeOf(SystemLanguage.Turkish));
        Assert.AreEqual("en", Loc.CodeOf(SystemLanguage.English));
        Assert.AreEqual("de", Loc.CodeOf(SystemLanguage.German));
    }
}
