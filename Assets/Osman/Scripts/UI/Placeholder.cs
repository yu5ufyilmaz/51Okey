using UnityEngine;
using UnityEngine.EventSystems;

public class Placeholder : MonoBehaviour, IDropHandler
{
    public void OnDrop(PointerEventData eventData)
    {
        // Sürüklenen obje (taş) alınıyor
        GameObject droppedTile = eventData.pointerDrag;
        if (droppedTile != null)
        {
            // Sürüklenen objeyi bu placeholder'ın içine yerleştir
            droppedTile.transform.SetParent(transform, false);
        }
    }
}