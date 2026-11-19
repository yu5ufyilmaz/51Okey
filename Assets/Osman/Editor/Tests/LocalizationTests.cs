using System.Linq;
using NUnit.Framework;

public class LocalizationTests
{
    static LocAudit.Report Run(string csv, string code = "") => LocAudit.Check(csv, new[] { ("A.cs", code) });

    [Test] public void Clean_Table_Has_No_Errors() =>
        Assert.IsEmpty(Run("key,tr,en\nlang.name,Türkçe,English\nlobby.a,A,B\n", "Loc.T(\"lobby.a\")").Errors);
    [Test] public void Missing_English_Is_Error() => StringAssert.Contains("lobby.a", Run("key,tr,en\nlobby.a,A,\n").Errors[0]);
    [Test] public void Placeholder_Mismatch_Is_Error() => Assert.IsNotEmpty(Run("key,tr,en\nlobby.a,{0} el,rounds\n").Errors);
    [Test] public void Formatted_Placeholder_Dropped_Is_Error() => Assert.IsNotEmpty(Run("key,tr,en\nlobby.a,{0:D2} el,rounds\n").Errors);
    [Test] public void Formatted_Placeholder_Matches_Plain_One() => Assert.IsEmpty(Run("key,tr,en\nlobby.a,{0:D2} el,{0} rounds\n", "\"lobby.a\"").Errors);
    [Test] public void File_Name_Is_Not_A_Used_Key()
    {
        const string table = "key,tr,en\nlobby.a,A,B\n";
        Assert.IsEmpty(Run(table, "\"common.cs\" \"lobby.a\"").Errors);
        Assert.IsNotEmpty(Run(table, "\"common.missing_key\"").Errors);
    }
    [Test] public void Replacement_Char_Is_Error() => Assert.IsNotEmpty(Run("key,tr,en\nlobby.a,Ta\uFFFD,Tile\n").Errors);
    [Test] public void Duplicate_Key_Is_Error() => Assert.IsNotEmpty(Run("key,tr,en\nlobby.a,A,B\nlobby.a,C,D\n").Errors);
    [Test] public void Bad_Key_Format_Is_Error() => Assert.IsNotEmpty(Run("key,tr,en\nLobby.PlayNow,A,B\n").Errors);
    [Test] public void Used_Key_Missing_From_Table_Is_Error() =>
        StringAssert.Contains("toast.x", Run("key,tr,en\nlobby.a,A,B\n", "UIRoot.Toast(Loc.T(\"toast.x\"));").Errors[0]);
    [Test] public void Unused_Key_Is_Warning_Not_Error()
    {
        var r = Run("key,tr,en\nlobby.a,A,B\n");
        Assert.IsEmpty(r.Errors); Assert.IsNotEmpty(r.Warnings);
    }
    [Test] public void Empty_Third_Language_Is_Warning()
    {
        var r = Run("key,tr,en,de\nlobby.a,A,B,\n", "\"lobby.a\"");
        Assert.IsEmpty(r.Errors); Assert.IsNotEmpty(r.Warnings);
    }
    [Test] public void Project_Table_Has_No_Errors()
    {
        var r = LocAudit.CheckProject();
        Assert.IsEmpty(r.Errors, string.Join("\n", r.Errors));
    }
    [Test] public void Leftover_Skips_Multiline_Log_But_Lists_Plain_Literal()
    {
        var l = LocAudit.LeftoverIn("A.cs", string.Join("\n","Debug.Log(", "  \"Şifre yanlış\");", "UIRoot.Toast(\"Şifre yanlış\");"));
        Assert.AreEqual(1, l.Count);
        StringAssert.StartsWith("A.cs:3:", l[0]);
    }

    static LocAudit.Report RunAssets(string csv, string asset) =>
        LocAudit.Check(csv, new[] { ("A.cs", "") }, new[] { ("A.unity", asset) });
    const string Table1 = "key,tr,en\nlobby.a,A,B\n";

    [Test] public void Asset_Key_In_Table_Is_No_Error() =>
        Assert.IsEmpty(RunAssets(Table1, "  - component: {fileID: 1}\n  key: lobby.a\n").Errors);
    [Test] public void Asset_Typo_Key_Is_Error()
    {
        var r = RunAssets(Table1, "  key: table.typo\n");
        Assert.IsTrue(r.Errors.Any(e => e.Contains("table.typo")));
    }
    [Test] public void Asset_Non_Key_Looking_Value_Is_Error() =>
        Assert.IsTrue(RunAssets(Table1, "  key: Oyna\n").Errors.Any(e => e.Contains("Oyna")));
    [Test] public void Prefab_Override_Pair_Is_Detected()
    {
        var r = RunAssets(Table1, "    - target: {fileID: 5}\n      propertyPath: key\n      value: lobby.gone\n      objectReference: {fileID: 0}\n");
        Assert.IsTrue(r.Errors.Any(e => e.Contains("lobby.gone")));
    }
    [Test] public void Question_Mark_Encoding_Damage_Is_Error() =>
        Assert.IsNotEmpty(Run("key,tr,en\nlobby.a,?ifre yanl??,Wrong password\n").Errors);
    [TestCase("Emin misin?", "Are you sure?")]
    [TestCase("Oyundan çıkılsın mı?", "Quit the game?")]
    public void Real_Question_Marks_Are_Fine(string tr, string en) =>
        Assert.IsEmpty(Run("key,tr,en\nlobby.a," + tr + "," + en + "\n", "\"lobby.a\"").Errors);
    [Test] public void Ansi_Saved_Csv_Is_Error()
    {
        // Windows-1254: ş = 0xFE, ı = 0xFD
        var bytes = new byte[] { 0x6B,0x65,0x79,0x2C,0x74,0x72,0x2C,0x65,0x6E,0x0A, 0x6C,0x6F,0x62,0x62,0x79,0x2E,0x61,0x2C, 0xFE,0x69,0x66,0x72,0x65,0x20,0x6B,0x61,0x70,0xFD,0x2C, 0x57,0x0A };
        Assert.IsNotEmpty(Run(System.Text.Encoding.UTF8.GetString(bytes)).Errors);
    }
    [Test] public void Bad_Language_Header_Is_Error([Values("Deutsch", "Notes", "açıklama")] string h) =>
        Assert.IsTrue(Run("key,tr,en," + h + "\nlobby.a,A,B,C\n").Errors.Any(e => e.Contains(h.ToLowerInvariant())));
    [Test] public void Regional_Language_Header_Is_Fine() =>
        Assert.IsEmpty(Run("key,tr,en,pt-br\nlobby.a,A,B,C\n", "\"lobby.a\"").Errors);
    [Test] public void Duplicate_Language_Column_Is_Error() =>
        Assert.IsTrue(Run("key,tr,en,en\nlobby.a,A,B,C\n", "\"lobby.a\"").Errors.Any(e => e.Contains("en")));
}
