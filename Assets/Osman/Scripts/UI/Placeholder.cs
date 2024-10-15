using UnityEngine;
using UnityEngine.EventSystems;

public class Placeholder : MonoBehaviour, IDropHandler
{
    public void OnDrop(PointerEventData eventData)
    {
        GameObject droppedTile = eventData.pointerDrag;
        
        if (droppedTile == null) return;

        if (transform.childCount == 0) // Placeholder boşsa taşı bırak
        {
            droppedTile.transform.SetParent(transform, false);
            droppedTile.transform.localPosition = Vector3.zero; // Taşı ortalayarak yerleştir
        }
        else if (transform.childCount == 1) // Placeholder doluysa kaydırma yap
        {
            Transform existingTile = transform.GetChild(0);
            Transform newPlaceholder = FindEmptyPlaceholder(transform, existingTile, droppedTile);

            if (newPlaceholder != null)
            {
                existingTile.SetParent(newPlaceholder, false);
                existingTile.localPosition = Vector3.zero; // Eski taşı yeni placeholder'a taşı
                droppedTile.transform.SetParent(transform, false); // Yeni taşı mevcut placeholder'a yerleştir
                droppedTile.transform.localPosition = Vector3.zero;
            }
        }
    }

    // Uygun boş placeholder arar ve mevcut taş için yeni yer sağlar
    private Transform FindEmptyPlaceholder(Transform currentPlaceholder, Transform existingTile, GameObject droppedTile)
    {
        Transform newPlaceholder = null;

        // Öncelikle sağ tarafta boş yer arar
        int currentIndex = currentPlaceholder.GetSiblingIndex();
        int maxIndex = currentPlaceholder.parent.childCount - 1;
        
        for (int i = currentIndex + 1; i <= maxIndex; i++)
        {
            Transform placeholder = currentPlaceholder.parent.GetChild(i);
            if (placeholder.childCount == 0)
            {
                newPlaceholder = placeholder;
                break;
            }
        }
        
        // Sağda boş yer yoksa, sol tarafa bak
        if (newPlaceholder == null)
        {
            for (int i = currentIndex - 1; i >= 0; i--)
            {
                Transform placeholder = currentPlaceholder.parent.GetChild(i);
                if (placeholder.childCount == 0)
                {
                    newPlaceholder = placeholder;
                    break;
                }
            }
        }
        return newPlaceholder;
    }
}
