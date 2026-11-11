using UnityEngine;
using UnityEngine.UI;

public class SidePickUIManager : MonoBehaviour
{
    [Header("UI References")]
    public Button btnCancelPick;

    private void Start()
    {
        btnCancelPick.onClick.AddListener(OnCancelButtonClicked);
        btnCancelPick.gameObject.SetActive(false);

        // Olayları Dinle
        EventDispatcher.RegisterFunction<Tiles>("OnSideTilePicked", ShowCancelButton);
        EventDispatcher.RegisterFunction<Tiles>("OnTileThrown", HideButtonOnAction);
    }

    private void Update()
    {
        // Yandan alınan taş elde değilse (geri bırakıldı, açmadan atma cezasıyla iade edildi, tur bitti)
        // ya da per açıldı/taş işlendiyse buton kaybolsun: iade edilecek taş yok.
        if (btnCancelPick.gameObject.activeSelf)
        {
            var tm = GameManager.Instance.turnManager;
            if (!tm.hasPickedFromSide || tm.hasOpenedThisTurn || tm.hasProcessedThisTurn)
                btnCancelPick.gameObject.SetActive(false);
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
            btnCancelPick.gameObject.SetActive(true);
    }

    private void HideButtonOnAction(Tiles tile)
    {
        btnCancelPick.gameObject.SetActive(false);
    }

    // Butona basılınca: taşı yana iade et.
    // Veriden ve ıstakadan silme RollbackSidePickProcess'te ID ile yapılır. Burada eskiden renk/sayıya göre
    // ilk eşleşen görsel de siliniyordu; elde ikizi varsa, alınan taş yerine oyuncunun kendi taşı ekrandan gidiyordu.
    public void OnCancelButtonClicked()
    {
        GameManager.Instance.CancelSidePickAction();
        btnCancelPick.gameObject.SetActive(false);
    }
}
