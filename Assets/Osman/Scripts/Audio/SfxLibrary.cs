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
        public float volume = 1f;
        public float pitchJitter;
        public float minInterval = 0.05f;
    }

    public List<Entry> entries = new List<Entry>();

    public Entry Find(SfxId id)
    {
        if (entries == null) return null;
        foreach (var e in entries) if (e.id == id) return e;
        return null;
    }
}
