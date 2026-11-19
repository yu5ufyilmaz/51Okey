using NUnit.Framework;

public class LocTableTests
{
    const string Basic = "key,tr,en,note\nlang.name,Türkçe,English,ad\nlobby.play_now,HEMEN OYNA,PLAY NOW,\n";

    [Test] public void Reads_Languages_And_Cells()
    {
        var t = LocTable.Parse(Basic);
        CollectionAssert.AreEqual(new[] { "tr", "en" }, t.Languages);
        CollectionAssert.AreEqual(new[] { "lang.name", "lobby.play_now" }, t.Keys);
        Assert.AreEqual("PLAY NOW", t.Get("en", "lobby.play_now"));
    }

    [Test] public void Semicolon_And_Tab_Delimiters()
    {
        Assert.AreEqual("B", LocTable.Parse("key;tr;en\na.b;A;B\n").Get("en", "a.b"));
        Assert.AreEqual("B", LocTable.Parse("key\ttr\ten\na.b\tA\tB\n").Get("en", "a.b"));
    }

    [Test] public void Quoted_Cell_With_Delimiter_Quote_And_Newline()
    {
        var t = LocTable.Parse("key,tr,en\na.b,\"Bir, iki\",\"He said \"\"hi\"\"\nline2\"\n");
        Assert.AreEqual("Bir, iki", t.Get("tr", "a.b"));
        Assert.AreEqual("He said \"hi\"\nline2", t.Get("en", "a.b"));
    }

    [Test] public void Bom_Crlf_Blank_And_Comment_Lines()
    {
        var t = LocTable.Parse("\uFEFFkey,tr,en\r\n\r\n# bölüm\r\na.b,A,B\r\n");
        CollectionAssert.AreEqual(new[] { "a.b" }, t.Keys);
        Assert.AreEqual("A", t.Get("tr", "a.b"));
    }

    [Test] public void Header_Is_Trimmed_And_Lowercased_Note_Excluded()
    {
        var t = LocTable.Parse("Key, TR ,En,Note\na.b,A,B,açıklama\n");
        CollectionAssert.AreEqual(new[] { "tr", "en" }, t.Languages);
    }

    [Test] public void Empty_Cell_Short_Row_Unknown_Key_Or_Language_Are_Null()
    {
        var t = LocTable.Parse("key,tr,en\na.b,A,\nc.d,C\n");
        Assert.IsNull(t.Get("en", "a.b"));
        Assert.IsNull(t.Get("en", "c.d"));
        Assert.IsNull(t.Get("tr", "x.y"));
        Assert.IsNull(t.Get("de", "a.b"));
    }

    [Test] public void Duplicate_Key_Is_Reported_First_Wins()
    {
        var t = LocTable.Parse("key,tr\na.b,İlk\na.b,İkinci\n");
        CollectionAssert.AreEqual(new[] { "a.b" }, t.DuplicateKeys);
        Assert.AreEqual("İlk", t.Get("tr", "a.b"));
    }

    [Test] public void Empty_Or_Null_Input_Gives_Empty_Table()
    {
        Assert.AreEqual(0, LocTable.Parse("").Keys.Count);
        Assert.AreEqual(0, LocTable.Parse(null).Languages.Count);
    }

    [Test] public void Duplicate_Language_Column_Is_Reported_First_Wins()
    {
        var t = LocTable.Parse("key,tr,en,en\na.b,A,B,C\n");
        CollectionAssert.AreEqual(new[] { "tr", "en" }, t.Languages);
        CollectionAssert.AreEqual(new[] { "en" }, t.DuplicateLanguages);
        Assert.AreEqual("B", t.Get("en", "a.b"));
    }
}
