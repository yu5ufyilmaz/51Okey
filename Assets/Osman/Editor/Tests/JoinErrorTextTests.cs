using NUnit.Framework;

public class JoinErrorTextTests
{
    [TestCase((short)32765, "error.room_full")]                 // ErrorCode.GameFull
    [TestCase((short)32764, "error.room_closed")]             // ErrorCode.GameClosed
    [TestCase((short)32758, "error.room_gone")]            // ErrorCode.GameDoesNotExist
    [TestCase((short)32766, "error.room_exists")]    // ErrorCode.GameIdAlreadyExists
    [TestCase((short)-1, "error.room_join_failed")]
    public void MapsCodes(short code, string expected) => Assert.AreEqual(expected, JoinErrorText.For(code));
}
