using Photon.Pun;
using UnityEngine;
using UnityEngine.EventSystems;

public class Placeholder : MonoBehaviour, IDropHandler
{
    public bool isRight = false; // Sağ taraf (Atma alanı) mı?
    public bool isDrop = false; // Çöp/Atma kutusu mu?
    public bool available = false; // İşlek (Meld) için uygun mu?
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

                    // Yeni gelen taşı buraya oturt
                    droppedTile.transform.SetParent(transform, false);
                    droppedTile.transform.localPosition = Vector3.zero;
                }
            }
        }
        // Eğer burası bir "Drop" (Çöp/Atma) alanı ise
        else
        {
            droppedTile.transform.SetParent(transform, false);
            droppedTile.transform.localPosition = Vector3.zero;
        }
    }

    // Uygun boş placeholder arar ve mevcut taş için yeni yer sağlar
    private Transform FindEmptyPlaceholder(
        Transform currentPlaceholder,
        Transform existingTile,
        GameObject droppedTile
    )
    {
        // *Bu metodda değişiklik yapmana gerek yok, mantığı doğru*
        // Sadece PlayerTileContainer içinde çalışacağı için masadaki taşları bozmaz.

        Transform newPlaceholder = null;

        // Sağ tarafı tara
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

        // Sağda yoksa solu tara
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
