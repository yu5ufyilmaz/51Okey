using System.Collections.Generic;
using System.Text;

// Masanın 32 bit özeti (FNV-1a, UTF-8 Canonical(false)); yayın yapısında da çalışır. Dönenin masası ile masa sahibininki bununla kıyaslanır.
public static class TableDigest
{
    public static uint Of(TableSnapshot s)
    {
        uint h = 2166136261u;
        foreach (byte b in Encoding.UTF8.GetBytes(s.Canonical(false)))
            h = (h ^ b) * 16777619u;
        return h;
    }
}

// Son N özet; anahtar (el, seri). Aynı anahtar üzerine yazar, dolunca en eskisi düşer.
public sealed class DigestRing
{
    readonly int capacity;
    readonly List<(int round, int serial, uint digest)> items = new List<(int, int, uint)>();

    public DigestRing(int capacity = 16) { this.capacity = capacity; }

    public void Put(int round, int serial, uint digest)
    {
        int i = items.FindIndex(e => e.round == round && e.serial == serial);
        if (i >= 0) { items[i] = (round, serial, digest); return; }
        if (items.Count >= capacity) items.RemoveAt(0);
        items.Add((round, serial, digest));
    }

    public void Clear() => items.Clear();

    public bool TryGet(int round, int serial, out uint digest)
    {
        int i = items.FindIndex(e => e.round == round && e.serial == serial);
        digest = i >= 0 ? items[i].digest : 0;
        return i >= 0;
    }
}
