using UnityEngine;
using UnityEngine.EventSystems;

// Basılıyken buton hafifçe küçülür.
public class UiPressScale : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    public void OnPointerDown(PointerEventData e) => transform.localScale = Vector3.one * 0.95f;
    public void OnPointerUp(PointerEventData e) => transform.localScale = Vector3.one;
    void OnDisable() => transform.localScale = Vector3.one; // basılıyken kapanırsa küçük kalmasın
}
