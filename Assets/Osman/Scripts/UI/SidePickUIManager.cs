using System.Linq; // List işlemleri için gerekli
using Photon.Pun; // Photon için gerekli
using UnityEngine;
using UnityEngine.UI;

public class SidePickUIManager : MonoBehaviour
{
    [Header("UI References")]
    public Button btnCancelPick;

    // İptal ederken hangi taşı sileceğimizi bilmek için tutuyoruz
    private Tiles currentPickedTile;
    private TileDistrubite tileDistrubite;

    private void Start()
    {
        btnCancelPick.onClick.AddListener(OnCancelButtonClicked);
        btnCancelPick.gameObject.SetActive(false);

        // Referansı al
        tileDistrubite = FindObjectOfType<TileDistrubite>();

        // Olayları Dinle
        EventDispatcher.RegisterFunction<Tiles>("OnSideTilePicked", ShowCancelButton);
        EventDispatcher.RegisterFunction<Tiles>("OnTileThrown", HideButtonOnAction);
    }

    private void Update()
    {
        // Oyuncu per açtıysa buton kaybolsun (Açtıktan sonra iade edemez)
        if (btnCancelPick.gameObject.activeSelf)
        {
            if (GameManager.Instance.turnManager.hasOpenedThisTurn)
            {
                btnCancelPick.gameObject.SetActive(false);
            }
        }
    }

    private void OnDestroy()
    {
        EventDispatcher.UnregisterListener<Tiles>("OnSideTilePicked", ShowCancelButton);
        EventDispatcher.UnregisterListener<Tiles>("OnTileThrown", HideButtonOnAction);
    }

    // Yandan taş çekilince buton görünür ve taşı kaydeder
    private void ShowCancelButton(Tiles pickedTile)
    {
        if (GameManager.Instance.turnManager.IsPlayerTurn())
        {
            // Taşı hafızaya al (Silmek gerekirse bunu kullanacağız)
            currentPickedTile = pickedTile;

            btnCancelPick.gameObject.SetActive(true);
        }
    }

    private void HideButtonOnAction(Tiles tile)
    {
        btnCancelPick.gameObject.SetActive(false);
        currentPickedTile = null;
    }

    // Butona basılınca: Taşı iade et ve ELDEN SİL
    public void OnCancelButtonClicked()
    {
        if (currentPickedTile != null)
        {
            // 1. VERİ TABANINDAN SİL (Player Listesinden Çıkar)
            if (tileDistrubite != null)
            {
                // Kendi Queue numaranı al
                if (
                    PhotonNetwork.LocalPlayer.CustomProperties.TryGetValue(
                        "PlayerQue",
                        out object queueValue
                    )
                )
                {
                    int myQue = (int)queueValue;
                    // RPC ile listeden sil
                    tileDistrubite.photonView.RPC(
                        "RemoveTileFromPlayerListByValue",
                        RpcTarget.AllBuffered,
                        myQue,
                        currentPickedTile
                    );
                }
            }

            // 2. GÖRSELİ ELDEN SİL (Visual Destroy)
            Transform playerContainer = GameObject.Find("PlayerTileContainer").transform;
            foreach (Transform placeholder in playerContainer)
            {
                if (placeholder.childCount > 0)
                {
                    TileUI tileUI = placeholder.GetChild(0).GetComponent<TileUI>();
                    // Eğer bu taş, iptal ettiğimiz taş ise
                    if (
                        tileUI != null
                        && tileUI.tileDataInfo.color == currentPickedTile.color
                        && tileUI.tileDataInfo.number == currentPickedTile.number
                        && tileUI.tileDataInfo.type == currentPickedTile.type
                    )
                    {
                        Destroy(tileUI.gameObject);
                        break; // Bulduk ve sildik, döngüden çık
                    }
                }
            }
        }

        // 3. OYUN MANTIĞINI SIFIRLA (GameManager)
        // Bu fonksiyon taşı yana geri koyar ve "dropped" durumunu sıfırlar
        GameManager.Instance.CancelSidePickAction();

        // Butonu gizle ve veriyi temizle
        btnCancelPick.gameObject.SetActive(false);
        currentPickedTile = null;
    }
}
