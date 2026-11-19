using TMPro;
using UnityEngine;

// Metni anahtarına göre yazar, dil değişince yeniler.
[RequireComponent(typeof(TMP_Text))]
public class LocText : MonoBehaviour
{
    public string key;

    void OnEnable() { Refresh(); Loc.Changed += Refresh; }
    void OnDisable() => Loc.Changed -= Refresh;

    void Refresh()
    {
        if (string.IsNullOrEmpty(key)) return;
        GetComponent<TMP_Text>().text = Loc.T(key);
    }
}
