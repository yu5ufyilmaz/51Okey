using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

// SyncAutoTest ile Tools/SyncTest/Run-SyncTest.ps1 arasındaki dosyalar.
// Runner config.json yazar, client status.json yazar; ikisi de <proje>/Temp/SyncAutoTest altında.
[Serializable]
public class SyncAutoTestConfig
{
    public string runId,
        role,
        roomName,
        nickName;
    public int deals = 1,
        seed,
        injectDesyncAtMove;
    public float moveDelaySec = 0.3f;

    // "simple": çek-at otopilot. "smart": test oyuncusu (TestPlayerBrain).
    public string mode = "simple";
    public float mistakeRate;
    public int injectOracleBugAtMove; // öz-test: bu hamlede beklenen değeri bilerek boz
    public bool fillWithBots; // tek editör: host odayı kurar, boş koltukları oyunun botları doldurur

    public string displayName; // boş değilse Photon takma adı bu; iç kimlik (nickName) değişmez
    public float botFillDelaySec; // > 0: host oyunun bot doldurma bekleme süresini bununla değiştirir (üst sınır)
    public int humans; // > 0: bu kadar insan oturunca host bot beklemesini bitirir (0 = kullanılmaz)

    public string PhotonName => string.IsNullOrEmpty(displayName) ? nickName : displayName;
    public bool IsHost => role == "host";
    public bool IsSmart => mode == "smart";
}

[Serializable]
public class SyncCounts
{
    public int ok,
        desync;
    public string first;
}

[Serializable]
public class SyncAutoTestStatus
{
    public string runId,
        role,
        nickName,
        state;
    public int actor,
        que,
        deal,
        myMoves;
    public bool isMyTurn; // Zaman aşımında runner takılanı bununla bulur.
    public int opens,
        processes,
        sidePicks,
        mistakes,
        bugs;
    public string finishType;
    public string journal;
    public List<string> errors = new List<string>();
    public string desyncLog;
    public SyncCounts sync = new SyncCounts();
}

public static class SyncAutoTestFiles
{
    public static string Dir(string projectRoot) => Path.Combine(projectRoot, "Temp", "SyncAutoTest");

    public static bool TryLoadConfig(string projectRoot, out SyncAutoTestConfig config)
    {
        config = null;
        string path = Path.Combine(Dir(projectRoot), "config.json");
        if (!File.Exists(path))
            return false;
        try
        {
            config = JsonUtility.FromJson<SyncAutoTestConfig>(File.ReadAllText(path));
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[AUTOTEST] config okunamadı: {e.Message}");
            config = null;
        }
        return config != null && !string.IsNullOrEmpty(config.runId);
    }

    // Runner dosyayı her an okuyabilir: önce .tmp'ye yaz, sonra tek hamlede yerine koy.
    public static void WriteStatus(string projectRoot, SyncAutoTestStatus status)
    {
        string dir = Dir(projectRoot);
        Directory.CreateDirectory(dir);
        string path = Path.Combine(dir, "status.json");
        string tmp = path + ".tmp";
        File.WriteAllText(tmp, JsonUtility.ToJson(status, true));
        if (File.Exists(path))
            File.Replace(tmp, path, null);
        else
            File.Move(tmp, path);
    }
}
