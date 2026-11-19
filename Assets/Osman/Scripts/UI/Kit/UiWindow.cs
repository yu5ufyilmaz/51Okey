using UnityEngine;
using UnityEngine.Events;

// Geri tuşu yığınına kendini kaydeden pencere. Kapatılamaz pencere (isim sorma) tuşu yutar.
// Geri tuşu onBack'i çağırır; kapanmayı kendisi yöneten ekran closeOnBack'i kapatır.
public class UiWindow : MonoBehaviour
{
    public bool closable = true;
    public bool closeOnBack = true;
    public UnityEvent onBack = new UnityEvent();

    void OnEnable() => UIRoot.Register(this);
    void OnDisable() => UIRoot.Unregister(this);
}
