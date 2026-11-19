using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Düğmeye tıklanınca sesini çalar. Basma ve bırakma aynı nesnedeyse uGUI OnPointerClick gönderir.
public class UiSfx : MonoBehaviour, IPointerClickHandler
{
    public SfxId id = SfxId.UiClick;

    public void OnPointerClick(PointerEventData e)
    {
        // Kapalı düğme (Selectable) sessiz kalır
        if (TryGetComponent<Selectable>(out var s) && !s.IsInteractable()) return;
        Sfx.Play(id);
    }
}
