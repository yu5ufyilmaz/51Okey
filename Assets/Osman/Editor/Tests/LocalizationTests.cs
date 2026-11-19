using NUnit.Framework;

public class LocalizationTests
{
    static LocAudit.Report Run(string csv, string code = "") => LocAudit.Check(csv, new[] { ("A.cs", code) });

    [Test] public void Clean_Table_Has_No_Errors() =>
        Assert.IsEmpty(Run("key,tr,en\nlang.name,Türkçe,English\nlobby.a,A,B\n", "Loc.T(\"lobby.a\")").Errors);
    [Test] public void Missing_English_Is_Error() => StringAssert.Contains("lobby.a", Run("key,tr,en\nlobby.a,A,\n").Errors[0]);
    [Test] public void Placeholder_Mismatch_Is_Error() => Assert.IsNotEmpty(Run("key,tr,en\nlobby.a,{0} el,rounds\n").Errors);
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
}
