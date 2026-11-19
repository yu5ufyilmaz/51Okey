using Photon.Pun;
using TMPro;
using UnityEngine;

// İsimliklerin sağındaki "Açtı 68" / "Çift 72" etiketleri. Yalnızca gösterim; değerler GameManager.OpenValues'ta durur.
public class OpenBadges : MonoBehaviour
{
    public TMP_Text[] labels; // 0: ben, 1: sağ, 2: karşı, 3: sol (UIManager.turnIndicators ile aynı sıra)

    bool pending; // yerel sıra numarası henüz bilinmiyorsa Update'te yeniden denenir

    void OnEnable()
    {
        GameManager.OpenValueShown += OnShown;
        Loc.Changed += Refresh;
        Refresh();
    }

    void OnDisable()
    {
        GameManager.OpenValueShown -= OnShown;
        Loc.Changed -= Refresh;
    }

    void OnShown(int que, int value, bool isPairs) => Refresh();

    void Update()
    {
        if (pending) Refresh();
    }

    void Refresh()
    {
        if (labels == null) return;
        foreach (var l in labels) if (l != null) l.text = "";
        var gm = GameManager.Instance;
        if (gm == null || gm.OpenValues.Count == 0) { pending = false; return; }
        int myQue = 0;
        if (PhotonNetwork.LocalPlayer != null && PhotonNetwork.LocalPlayer.CustomProperties.TryGetValue("PlayerQue", out object v))
            myQue = (int)v;
        pending = myQue == 0;
        if (pending) return;
        foreach (var kv in gm.OpenValues)
        {
            int idx = (kv.Key - myQue + 4) % 4;
            if (idx < 0 || idx >= labels.Length || labels[idx] == null) continue;
            labels[idx].text = Loc.T(kv.Value.pairs ? "table.opened_pairs" : "table.opened_runs", kv.Value.value);
        }
    }
}
