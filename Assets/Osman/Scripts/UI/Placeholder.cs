using Photon.Pun;
using UnityEngine;
using UnityEngine.EventSystems;

public class Placeholder : MonoBehaviour, IDropHandler
{
    public bool isRight = false; // Sağ taraf (Atma alanı) mı?
    public bool isDrop = false; // Çöp/Atma kutusu mu?
    public bool available = false; // İşlek (Meld) için uygun mu?
    public bool isMeldArea = false;
    public bool willInstantiate = false; // Otomatik oluşturma flag'i
    public Tiles AvailableTileInfo; // Beklenen taş verisi

    // Bu Placeholder'ın bağlı olduğu ana konteyner (Örn: PlayerTileContainer, MeldContainer)
    private Transform parentContainer;

    private void Start()
    {
        // Ebeveynin ebeveyni genellikle ana konteynerdir (örn: PlayerTileContainer -> Placeholder(Clone))
        // Eğer hiyerarşin farklıysa burayı ona göre ayarla.
        // Genelde: PlayerTileContainer -> Placeholder
        parentContainer = transform.parent;
    }

    public void OnDrop(PointerEventData eventData)
    {
        GameObject droppedTile = eventData.pointerDrag;
        if (droppedTile == null)
            return;
        if (isMeldArea)
            return;
        // ---------------------------------------------------------------
        // [YENİ GÜVENLİK KONTROLÜ]
        // Sadece oyuncunun kendi ıstakasındaki (PlayerTileContainer)
        // veya Çöp/Atma (isDrop/isRight) alanlarındaki placeholderlara taş bırakılabilir.
        // Masadaki (Meld) veya Ortadaki (Middle) placeholderlara taş bırakılamaz.
        // ---------------------------------------------------------------

        // Ana konteynerin ismini kontrol ederek güvenliği sağlıyoruz.
        // (Not: Hiyerarşine göre "PlayerTileContainer" ismini doğru yazdığından emin ol)
        bool isPlayerRack =
            parentContainer.name == "PlayerTileContainer"
            || transform.parent.name == "PlayerTileContainer";

        // Eğer burası bir atma alanı değilse VE oyuncunun ıstakası da değilse
        // (Yani masada açılmış bir yerse), işlemi anında iptal et.
        if (!isDrop && !isPlayerRack)
        {
            Debug.LogWarning("Bu alana (Masa/Meld) elle taş bırakamazsın!");
            return;
        }
        // ---------------------------------------------------------------


        if (isDrop == false)
        {
            // 1. DURUM: Placeholder BOŞSA -> Taşı direkt koy
            if (transform.childCount == 0)
            {
                droppedTile.transform.SetParent(transform, false);
                droppedTile.transform.localPosition = Vector3.zero;

                if (droppedTile.GetComponent<TileUI>())
                    droppedTile.GetComponent<TileUI>().FitToParent();
            }
            // 2. DURUM: Placeholder DOLUYSA -> Kaydırma (Shift) yapmaya çalış
            else if (transform.childCount == 1)
            {
                Transform existingTileTransform = transform.GetChild(0);
                TileUI existingTileUI = existingTileTransform.GetComponent<TileUI>();

                // --- [YENİ EKLENEN GÖSTERGE KORUMASI] ---
                // Eğer içerideki taş "Gösterge Taşı" ise, sakın dokunma!
                if (existingTileUI != null && existingTileUI.isIndicatorTile)
                {
                    Debug.LogWarning("Gösterge taşının olduğu yere taş koyamazsın!");
                    return; // Hiçbir şey yapma, taş TileUI.OnEndDrag ile geri dönecek.
                }
                // ----------------------------------------

                Debug.Log("Placeholder dolu, kaydırma deneniyor...");

                Transform newPlaceholder = FindEmptyPlaceholder(
                    transform,
                    existingTileTransform,
                    droppedTile
                );

                if (newPlaceholder != null)
                {
                    // Eski taşı yeni boş yere taşı
                    existingTileTransform.SetParent(newPlaceholder, false);
                    existingTileTransform.localPosition = Vector3.zero;

                    if (droppedTile.GetComponent<TileUI>())
                        droppedTile.GetComponent<TileUI>().FitToParent();

                    // Yeni gelen taşı buraya oturt
                    droppedTile.transform.SetParent(transform, false);
                    droppedTile.transform.localPosition = Vector3.zero;

                    if (droppedTile.GetComponent<TileUI>())
                        droppedTile.GetComponent<TileUI>().FitToParent();
                }
            }
        }
        // Eğer burası bir "Drop" (Çöp/Atma) alanı ise
        else
        {
            droppedTile.transform.SetParent(transform, false);
            droppedTile.transform.localPosition = Vector3.zero;
            // BURAYA EKLE:
            if (droppedTile.GetComponent<TileUI>())
                droppedTile.GetComponent<TileUI>().FitToParent();
        }
    }

    // Uygun boş placeholder arar ve mevcut taş için yeni yer sağlar
    private Transform FindEmptyPlaceholder(
        Transform currentPlaceholder,
        Transform existingTile,
        GameObject droppedTile
    )
    {
        Transform newPlaceholder = null;

        int currentIndex = currentPlaceholder.GetSiblingIndex();
        int maxIndex = currentPlaceholder.parent.childCount - 1;

        // --- 1. SAĞ TARAFI TARA ---
        for (int i = currentIndex + 1; i <= maxIndex; i++)
        {
            Transform placeholder = currentPlaceholder.parent.GetChild(i);
            Placeholder phScript = placeholder.GetComponent<Placeholder>();

            // --- [KRİTİK DÜZELTME] DUVAR KONTROLÜ ---
            // Eğer baktığımız yer "Atma Yeri" (isRight) ise, oraya taş koyamayız.
            // Ayrıca sağ taraf bitmiş demektir, döngüyü kır.
            if (phScript != null && phScript.isRight)
            {
                // Debug.Log("Sağ tarafta boş yer ararken Duvara (isRight) çarpıldı. Arama durduruluyor.");
                break;
            }

            if (placeholder.childCount == 0)
            {
                newPlaceholder = placeholder;
                break;
            }
        }

        // --- 2. SOL TARAFI TARA (Eğer sağda yer yoksa) ---
        if (newPlaceholder == null)
        {
            for (int i = currentIndex - 1; i >= 0; i--)
            {
                Transform placeholder = currentPlaceholder.parent.GetChild(i);
                Placeholder phScript = placeholder.GetComponent<Placeholder>();

                // Sol tarafta isRight olma ihtimali düşük ama yine de kontrol edelim
                if (phScript != null && phScript.isRight)
                    continue;

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
