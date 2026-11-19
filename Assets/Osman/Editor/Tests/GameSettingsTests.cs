using NUnit.Framework;
using UnityEngine;

// Editörün gerçek PlayerPrefs'ini kullanır: her test iki anahtarı silerek başlar ve biter.
public class GameSettingsTests
{
    static void DeleteKeys()
    {
        PlayerPrefs.DeleteKey("Okey.Sound");
        PlayerPrefs.DeleteKey("Okey.Music");
        PlayerPrefs.Save();
    }

    [SetUp]
    public void SetUp()
    {
        DeleteKeys();
        AudioListener.volume = 1f;
    }

    [TearDown]
    public void TearDown()
    {
        DeleteKeys();
        GameSettings.Apply();
    }

    [Test]
    public void Defaults_AreOn()
    {
        Assert.IsTrue(GameSettings.SoundOn);
        Assert.IsTrue(GameSettings.MusicOn);
    }

    [Test]
    public void SoundOff_PersistsAndMutesListener()
    {
        GameSettings.SoundOn = false;
        Assert.IsFalse(GameSettings.SoundOn);
        Assert.AreEqual(0, PlayerPrefs.GetInt("Okey.Sound", 1));
        Assert.AreEqual(0f, AudioListener.volume);
        GameSettings.SoundOn = true;
        Assert.AreEqual(1f, AudioListener.volume);
    }

    [Test]
    public void MusicOff_PersistsAndLeavesListener()
    {
        GameSettings.MusicOn = false;
        Assert.IsFalse(GameSettings.MusicOn);
        Assert.AreEqual(0, PlayerPrefs.GetInt("Okey.Music", 1));
        Assert.AreEqual(1f, AudioListener.volume);
    }
}
