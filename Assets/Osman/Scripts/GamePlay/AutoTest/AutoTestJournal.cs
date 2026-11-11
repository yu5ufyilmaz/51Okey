using System;
using System.IO;

// Test oyuncusunun hamle günlüğü: "anahtar | kim | metin". Anahtar DesyncDetector'ın el.hamle sayacı,
// böylece bir desync ya da [BUG] satırı diğer client'ların aynı andaki satırlarıyla eşleşir.
public class AutoTestJournal : IDisposable
{
    readonly StreamWriter writer;

    public string Path { get; }

    public AutoTestJournal(string path)
    {
        Path = path;
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path));
        writer = new StreamWriter(path) { AutoFlush = true };
    }

    public void Line(string key, string who, string text) => writer.WriteLine($"{key} | {who} | {text}");

    public void Check(string key, string who, string what, string expected, string observed, bool ok) =>
        Line(key, who, $"{what} | beklenen: {expected} | gözlenen: {observed} | {(ok ? "OK" : "[BUG]")}");

    public void Dispose() => writer.Dispose();
}
