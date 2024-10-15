using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class TileUI : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    private Transform originalParent;
    private CanvasGroup canvasGroup;
    public Image tileImage; // Tile'in görüntüsünü göstermek için Image bileşeni

    private void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();
        originalParent = transform.parent;
    }

    public void SetTileData(TileData tileData)
    {
        if (tileData != null && tileImage != null)
        {
            tileImage.sprite = tileData.tileSprite; // TileData'dan alınan sprite'ı tileImage'a ayarla
        }
        else
        {
            Debug.LogError("TileData veya TileImage eksik!");
        }
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        originalParent = transform.parent;
        canvasGroup.blocksRaycasts = false; // Sürüklenen taşı bırakılabilir hale getir
        transform.SetParent(transform.root, true); // Root seviyeye çıkar
    }

    public void OnDrag(PointerEventData eventData)
    {
        transform.position = Input.mousePosition;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        canvasGroup.blocksRaycasts = true;

        Transform parentContainer = originalParent.parent;
        Transform closestPlaceholder = null;
        float closestDistance = float.MaxValue;

        // En yakın placeholder'ı bul
        foreach (Transform placeholder in parentContainer)
        {
            if (placeholder.CompareTag("Placeholder"))
            {
                float distance = Vector3.Distance(placeholder.position, transform.position);
                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    closestPlaceholder = placeholder;
                }
            }
        }

        // Eğer en yakın placeholder'a yakınsa oraya yerleştir
        if (closestPlaceholder != null && closestDistance < 100f)
        {
            // Taşımızı hedef placeholder'a yerleştir
            transform.SetParent(closestPlaceholder, false);
            transform.localPosition = Vector3.zero;
            transform.localScale = Vector3.one; // Boyutu orijinal hale getir

            int targetIndex = closestPlaceholder.GetSiblingIndex();

            // Sağ veya sol kaydırma kontrolü
            if (closestPlaceholder.childCount > 1)
            {
                Transform displacedTile = closestPlaceholder.GetChild(0);
            
                if (targetIndex < originalParent.GetSiblingIndex()) // Sola kaydırma
                {
                    ShiftTilesLeft(parentContainer, displacedTile, targetIndex - 1);
                }
                else // Sağa kaydırma
                {
                    ShiftTilesRight(parentContainer, displacedTile, targetIndex + 1);
                }
            }
        }
        else
        {
            // Eğer uygun bir yere bırakılmadıysa eski yerine dön
            transform.SetParent(originalParent, false);
            transform.localPosition = Vector3.zero;
            transform.localScale = Vector3.one; // Boyutu orijinal hale getir
        }
    }




    private void ShiftTilesRight(Transform parentContainer, Transform tileToShift, int startIndex)
    {
        for (int i = startIndex; i < parentContainer.childCount; i++)
        {
            Transform currentPlaceholder = parentContainer.GetChild(i);

            // Eğer mevcut placeholder boşsa taşı buraya yerleştir ve işlemi durdur
            if (currentPlaceholder.childCount == 0)
            {
                tileToShift.SetParent(currentPlaceholder, false);
                tileToShift.localPosition = Vector3.zero;
                tileToShift.localScale = Vector3.one; // Boyutu orijinal hale getir
                return;
            }
            else
            {
                // Mevcut placeholder doluysa, bir sonrakine geç
                Transform nextTileToShift = currentPlaceholder.GetChild(0);
                tileToShift.SetParent(currentPlaceholder, false);
                tileToShift.localPosition = Vector3.zero;
                tileToShift.localScale = Vector3.one; // Boyutu orijinal hale getir

                // Bir sonraki taşı sağa kaydırma için ayarla
                tileToShift = nextTileToShift;
            }
        }
    }






    private void ShiftTilesLeft(Transform parentContainer, Transform tileToShift, int startIndex)
    {
        for (int i = startIndex; i >= 0; i--)
        {
            Transform currentPlaceholder = parentContainer.GetChild(i);

            // Eğer mevcut placeholder boşsa taşı buraya yerleştir ve işlemi durdur
            if (currentPlaceholder.childCount == 0)
            {
                tileToShift.SetParent(currentPlaceholder, false);
                tileToShift.localPosition = Vector3.zero;
                tileToShift.localScale = Vector3.one; // Boyutu orijinal hale getir
                return;
            }
            else
            {
                // Mevcut placeholder doluysa, bir sonrakine geç
                Transform nextTileToShift = currentPlaceholder.GetChild(0);
                tileToShift.SetParent(currentPlaceholder, false);
                tileToShift.localPosition = Vector3.zero;
                tileToShift.localScale = Vector3.one; // Boyutu orijinal hale getir

                // Bir sonraki taşı sola kaydırma için ayarla
                tileToShift = nextTileToShift;
            }
        }
    }


    // En yakın boş placeholder'ı bulur veya boş bir yere bırakılmasını sağlar
    private Transform FindEmptyPlaceholder()
    {
        Transform parentContainer = originalParent.parent; // Placeholderların bağlı olduğu parent
        int currentIndex = originalParent.GetSiblingIndex();
        
        // Sağdaki boş yeri bul
        for (int i = currentIndex + 1; i < parentContainer.childCount; i++)
        {
            Transform placeholder = parentContainer.GetChild(i);
            if (placeholder.childCount == 0)
                return placeholder;
        }

        // Solda boş yer yoksa, sola bak
        for (int i = currentIndex - 1; i >= 0; i--)
        {
            Transform placeholder = parentContainer.GetChild(i);
            if (placeholder.childCount == 0)
                return placeholder;
        }

        return null; // Boş placeholder bulunamadı
    }
}
