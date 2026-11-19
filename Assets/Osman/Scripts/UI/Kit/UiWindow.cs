using UnityEngine;
using UnityEngine.Events;

// Geri tuşu yığınına kendini kaydeden pencere. Kapatılamaz pencere (isim sorma) tuşu yutar.
public class UiWindow : MonoBehaviour
{
    public bool closable = true;
    public UnityEvent onBack = new UnityEvent();

    void OnEnable() => UIRoot.Register(this);
    void OnDisable() => UIRoot.Unregister(this);
}
