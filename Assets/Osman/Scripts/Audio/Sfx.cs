using System.Collections.Generic;
using UnityEngine;

public static class Sfx
{
    public const float OtherVolume = 0.5f;
    const int RecentMax = 32;

    static readonly List<SfxId> recent = new List<SfxId>();
    static readonly SfxGate gate = new SfxGate();
    static readonly HashSet<SfxId> warned = new HashSet<SfxId>();
    static SfxLibrary library;
    static AudioSource source, jitterSource;
    static bool quitting, quitHooked;
    // UnityEngine.Random dağıtım/koltuk tohumunu paylaşır; ses ayrı üreteç kullanır.
    static readonly System.Random rng = new System.Random();

    static void OnQuit() => quitting = true;

    // Düzenleyicide oynatma durunca alan yeniden yüklenmeyebilir: bayrak ve kanca sıfırlanır.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetQuit()
    {
        Application.quitting -= OnQuit;
        quitting = quitHooked = false;
    }

    public static IReadOnlyList<SfxId> Recent => recent;

    public static void ClearRecent() => recent.Clear();

    public static SfxId PenaltyFor(int playerQue, int myQue) => playerQue == myQue ? SfxId.PenaltyTaken : SfxId.PenaltyGiven;

    public static SfxId FinishFor(int winnerActor, int myActor) => winnerActor == myActor ? SfxId.Win : SfxId.Lose;

    public static bool CrossedBelow(float previous, float current, float threshold) => previous > threshold && current <= threshold;

    // null: elle verilen listeyi unut, Resources'tan yeniden yükle.
    public static void UseLibrary(SfxLibrary lib)
    {
        library = lib;
        quitting = false;
        gate.Reset();
    }

    public static void Play(SfxId id, float volumeScale = 1f) => PlayAt(id, Time.unscaledTime, volumeScale);

    public static void PlayAt(SfxId id, float now, float volumeScale = 1f)
    {
        if (SyncAutoTest.Instance != null) return;
        if (!quitHooked) { quitHooked = true; Application.quitting += OnQuit; }
        if (quitting && Application.isPlaying) return;
        if (library == null) library = Resources.Load<SfxLibrary>("SfxLibrary");
        var entry = library != null ? library.Find(id) : null;
        if (entry == null)
        {
#if UNITY_EDITOR
            if (warned.Add(id)) Debug.LogWarning("Sfx: listede satır yok: " + id);
#endif
            return;
        }
        if (!gate.Allow((int)id, now, entry.minInterval)) return;

        recent.Add(id);
        if (recent.Count > RecentMax) recent.RemoveAt(0);

        var clip = PickClip(entry.clips);
        if (clip == null || !Application.isPlaying) return;

        var src = GetSource(entry.pitchJitter > 0f);
        if (entry.pitchJitter > 0f) src.pitch = 1f + (float)(rng.NextDouble() * 2 - 1) * entry.pitchJitter;
        src.PlayOneShot(clip, Mathf.Clamp01(entry.volume * volumeScale));
    }

    static AudioClip PickClip(AudioClip[] clips)
    {
        if (clips == null) return null;
        int n = 0;
        foreach (var c in clips) if (c != null) n++;
        if (n == 0) return null;
        int k = rng.Next(n);
        foreach (var c in clips) if (c != null && k-- == 0) return c;
        return null;
    }

    // Perde oynayan sesler eğilmesin: perdesi oynayanlar ayrı kaynakta.
    static AudioSource GetSource(bool jitter)
    {
        if (source == null || jitterSource == null)
        {
            var go = new GameObject("Sfx");
            Object.DontDestroyOnLoad(go);
            source = NewSource(go);
            jitterSource = NewSource(go);
        }
        return jitter ? jitterSource : source;
    }

    static AudioSource NewSource(GameObject go)
    {
        var a = go.AddComponent<AudioSource>();
        a.playOnAwake = false;
        a.spatialBlend = 0f;
        return a;
    }
}
