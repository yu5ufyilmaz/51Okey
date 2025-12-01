using UnityEngine;
using UnityEngine.UI;

public class SidePickUIManager : MonoBehaviour
{
    [Header("UI References")]
    public Button btnCancelPick; // Inspector'dan butonu sürükle

    private void Start()
    {
        // Butona tıklama görevini ekle
        btnCancelPick.onClick.AddListener(OnCancelButtonClicked);

        // Başlangıçta gizle
        btnCancelPick.gameObject.SetActive(false);

        // Olayları Dinle
        EventDispatcher.RegisterFunction<Tiles>("OnSideTilePicked", ShowCancelButton);
        EventDispatcher.RegisterFunction<Tiles>("OnTileThrown", HideButtonOnAction);

        // Eğer oyuncu bir şekilde per açarsa buton kaybolmalı
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

    // Yandan taş çekilince buton görünür
    private void ShowCancelButton(Tiles pickedTile)
    {
        if (GameManager.Instance.turnManager.IsPlayerTurn())
        {
            btnCancelPick.gameObject.SetActive(true);
            // Buton üzerindeki yazıyı değiştirmek istersen:
            // btnCancelPick.GetComponentInChildren<Text>().text = "Taşı Geri Bırak";
        }
    }

    private void HideButtonOnAction(Tiles tile)
    {
        btnCancelPick.gameObject.SetActive(false);
    }

    // Butona basılınca
    public void OnCancelButtonClicked()
    {
        // GameManager'daki CEZASIZ fonksiyonu çağır
        GameManager.Instance.CancelSidePickAction();

        // Butonu gizle
        btnCancelPick.gameObject.SetActive(false);
    }
}
