using NUnit.Framework;

public class GameConfigTests
{
    [TestCase(42, "Misafir-00042")]
    [TestCase(123456, "Misafir-23456")]
    public void GuestName_IsPrefixPlusFiveDigits(int number, string name) =>
        Assert.AreEqual(name, GameConfig.GuestName(number));

    [TestCase("Misafir-00042", true)]
    [TestCase("Misafir-42", false)]
    [TestCase("Misafir-abcde", false)]
    [TestCase("misafir-00042", false)]
    [TestCase("Ahmet", false)]
    [TestCase(null, false)]
    public void IsGuestName_OnlyExactFormat(string name, bool guest) =>
        Assert.AreEqual(guest, GameConfig.IsGuestName(name));

    // Uzunluk durumları GameServicesTests.IsValidName'de.
    [TestCase("Misafir-12345", false)]
    [TestCase("misafir-x", false)]
    [TestCase("MISAFIR-1", false)]
    [TestCase("a/b", false)]
    [TestCase("Ahmet", true)]
    [TestCase("Misafirim", true)]
    public void IsValidName_RejectsGuestPrefixAndSlash(string name, bool valid) =>
        Assert.AreEqual(valid, GameConfig.IsValidName(name));

    // Firestore belge kimliği olamayan ve görünmez karakterli isimler; misafir isimleri de bu denetimden geçer.
    [TestCase(".", false)]
    [TestCase("..", false)]
    [TestCase("__ab__", false)]
    [TestCase("a\u200Bb", false)]
    [TestCase("a\0b", false)]
    [TestCase("a\tb", false)]
    [TestCase("a.b", true)]
    [TestCase("_ab_", true)]
    [TestCase("Ali Veli", true)]
    public void IsStorableName_RejectsBadDocumentIds(string name, bool storable) =>
        Assert.AreEqual(storable, GameConfig.IsStorableName(name));

    // Karar tablosu: misafire isim atanır, Google hesabının kayıtsız ismi kaydedilmeye çalışılır.
    [TestCase(true, false, "Misafir-00042", NameFlow.Step.AssignGuest)]
    [TestCase(true, false, "Deneme Oyuncu", NameFlow.Step.AssignGuest)]
    [TestCase(true, true, "Ahmet", NameFlow.Step.AssignGuest)]
    [TestCase(true, true, "Misafir-00042", NameFlow.Step.None)]
    [TestCase(false, false, "Ahmet", NameFlow.Step.ClaimCurrent)]
    [TestCase(false, false, "Misafir-00042", NameFlow.Step.None)]
    [TestCase(false, true, "Misafir-00042", NameFlow.Step.None)]
    [TestCase(false, true, "Ahmet", NameFlow.Step.None)]
    public void NameFlow_Next(bool isGuest, bool claimed, string name, NameFlow.Step step) =>
        Assert.AreEqual(step, NameFlow.Next(isGuest, claimed, name));

    [TestCase(NameResult.Ok, "")]
    [TestCase(NameResult.Taken, "Bu isim alınmış")]
    [TestCase(NameResult.Invalid, "İsim 2-16 karakter olmalı")]
    [TestCase(NameResult.Failed, "Bağlanılamadı")]
    public void NameFlow_ErrorText(NameResult result, string text) =>
        Assert.AreEqual(text, NameFlow.ErrorText(result));

    [TestCase(0, 1)]
    [TestCase(99, 1)]
    [TestCase(100, 2)]
    [TestCase(250, 3)]
    public void LevelForXp_OneLevelPerHundredXp(int xp, int level) =>
        Assert.AreEqual(level, GameConfig.LevelForXp(xp));

    [Test]
    public void CoinTx_KeepsItsFields()
    {
        var tx = new CoinTx("t1", -40, CoinReason.TableFee, "oda-7");
        Assert.AreEqual("t1", tx.Id);
        Assert.AreEqual(-40, tx.Amount);
        Assert.AreEqual(CoinReason.TableFee, tx.Reason);
        Assert.AreEqual("oda-7", tx.Reference);
    }
}
