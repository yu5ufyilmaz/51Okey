using System.Linq;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

// Dil tablosu hatalıysa derleme başlamadan durur (BuildPipeline.BuildPlayer de bunu çalıştırır).
class LocBuildGate : IPreprocessBuildWithReport
{
    public int callbackOrder => 0;

    public void OnPreprocessBuild(BuildReport report)
    {
        var errors = LocAudit.CheckProject().Errors;
        if (errors.Count > 0)
            throw new BuildFailedException("Dil tablosu hatalı, derleme durduruldu: " + string.Join(" | ", errors.Take(5)));
    }
}
