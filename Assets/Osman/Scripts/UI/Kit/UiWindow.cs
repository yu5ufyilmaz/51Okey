using UnityEngine;
using UnityEngine.Events;

// Geri tuşu yığınına kendini kaydeden pencere. Kapatılamaz pencere (isim sorma) tuşu yutar.
// Geri tuşu onBack'i çağırır; kapanmayı kendisi yöneten ekran closeOnBack'i kapatır.
public class UiWindow : MonoBehaviour
{
    public bool closable = true;
    public bool closeOnBack = true;
    public UnityEvent onBack = new UnityEvent(); // her kapatma isteğinde çalışır: geri tuşu ve X butonu

    // Geri tuşu ve X butonunun ortak yolu
    public void Close()
    {
        onBack.Invoke();
        if (this != null && closeOnBack) // işleyici pencereyi yok etmiş olabilir
            gameObject.SetActive(false);
    }

    void OnEnable() => UIRoot.Register(this);
    void OnDisable() => UIRoot.Unregister(this);
}
