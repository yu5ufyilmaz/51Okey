using NUnit.Framework;

public class JoinErrorTextTests
{
    [TestCase((short)32765, "Masa dolu")]                 // ErrorCode.GameFull
    [TestCase((short)32764, "Masa kapanmış")]             // ErrorCode.GameClosed
    [TestCase((short)32758, "Masa artık yok")]            // ErrorCode.GameDoesNotExist
    [TestCase((short)32766, "Bu isimde bir masa var")]    // ErrorCode.GameIdAlreadyExists
    [TestCase((short)-1, "Masaya girilemedi")]
    public void MapsCodes(short code, string expected) => Assert.AreEqual(expected, JoinErrorText.For(code));
}
