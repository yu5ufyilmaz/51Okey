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

}
