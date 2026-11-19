using System.Collections.Generic;

// Aynı sesin art arda çalmasını engeller.
public sealed class SfxGate
{
    readonly Dictionary<int, float> last = new Dictionary<int, float>();

    public bool Allow(int id, float now, float minInterval)
    {
        if (last.TryGetValue(id, out var t) && now >= t && now - t <= minInterval) return false;
        last[id] = now;
        return true;
    }

    public void Reset() => last.Clear();
}
