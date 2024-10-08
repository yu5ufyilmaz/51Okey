using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class TileUI : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    public Image tileImage;  // Taşın görseli
    private RectTransform rectTransform;
    private CanvasGroup canvasGroup;
    private Transform originalParent;  // Taşın sürüklenmeden önceki ebeveyni

    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        canvasGroup = GetComponent<CanvasGroup>();
        originalParent = transform.parent;  // Başlangıçta taşın orijinal ebeveyni
    }

    public void SetTileData(Tile tile)
    {
        // Sprite ve numarayı ayarlayın
        tileImage.sprite = Resources.Load<Sprite>($"Sprites/Tiles/{tile.color}_{tile.number}");

        // Debugging: Hangi tile'ın atandığını göstermek için log ekleyin
        Debug.Log($"Tile set: Color={tile.color}, Number={tile.number}");
    }

    // Sürüklemeyi başlatma
    public void OnBeginDrag(PointerEventData eventData)
    {
        originalParent = transform.parent;
        canvasGroup.blocksRaycasts = false;  // Sürüklenen objenin placeholder'lara takılmasını engeller
        transform.SetParent(transform.root); // Taşı root seviyeye çıkar (en üst seviye)
    }

    // Sürükleme
    public void OnDrag(PointerEventData eventData)
    {
        // Taşın sürüklenmesi esnasında pozisyonunu fareye göre ayarlar
        rectTransform.anchoredPosition += eventData.delta / transform.lossyScale.x;
    }

    // Sürüklemeyi bitirme
    public void OnEndDrag(PointerEventData eventData)
    {
        canvasGroup.blocksRaycasts = true;  // Taş placeholder'larla tekrar etkileşime girebilir

        // Eğer taş bir placeholder'a bırakılmadıysa, orijinal pozisyonuna dönsün
        if (transform.parent == transform.root)
        {
            transform.SetParent(originalParent, false);
        }
    }
}