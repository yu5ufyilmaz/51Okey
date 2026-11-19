using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Düğmeye tıklanınca sesini çalar. Basma ve bırakma aynı nesnedeyse uGUI OnPointerClick gönderir.
public class UiSfx : MonoBehaviour, IPointerDownHandler, IPointerClickHandler
{
    public SfxId id = SfxId.UiClick;

    // Basıldığı andaki durum: onClick düğmeyi kendi kapatsa da (Button önce çalışır) ses çalsın.
    // null: basma görülmedi (doğrudan çağrı), canlı denetim kullanılır.
    bool? armed;

    bool Live() => !TryGetComponent<Selectable>(out var s) || s.IsInteractable();

    // Burada ses çalmaz; yalnızca durumu not eder
    public void OnPointerDown(PointerEventData e)
    {
        if (e.button != PointerEventData.InputButton.Left) return;
        armed = Live();
    }

    public void OnPointerClick(PointerEventData e)
    {
        if (e.button != PointerEventData.InputButton.Left) return;
        bool play = armed ?? Live();
        armed = null;
        if (play) Sfx.Play(id);
    }

    void OnDisable() => armed = null;
}
