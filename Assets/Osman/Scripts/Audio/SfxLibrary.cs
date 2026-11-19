using System;
using System.Collections.Generic;
using UnityEngine;

public class SfxLibrary : ScriptableObject
{
    [Serializable]
    public class Entry
    {
        public SfxId id;
        public AudioClip[] clips;
        [Range(0f, 1f)] public float volume = 1f;
        [Range(0f, 0.2f)] public float pitchJitter;
        [Min(0f)] public float minInterval = 0.05f;
    }

    public List<Entry> entries = new List<Entry>();

    public Entry Find(SfxId id)
    {
        if (entries == null) return null;
        foreach (var e in entries) if (e.id == id) return e;
        return null;
    }
}
