using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;

public class ShopController : MonoBehaviour
{
    [SerializeField]
    private TMP_Text coinText;
    [SerializeField]
    private GameObject adsBuyButton;
    [SerializeField]
    private GameObject shopPanel;
    
    void Start()
    {
        shopPanel.SetActive(false);
        GameServices.Profile.Changed += ShowProfile;
        ShowProfile(GameServices.Profile.Current);
    }

    void OnDestroy()
    {
        // Store sahneden uzun yaşar; yok olmuş coinText'e dokunulmasın.
        GameServices.Profile.Changed -= ShowProfile;
    }

    void ShowProfile(PlayerProfile profile)
    {
        if (profile != null && coinText != null)
        {
            coinText.text = profile.Coins.ToString();
        }
    }

    public void OpenShop()
    {
        shopPanel.SetActive(true);
    }
    public void CloseShop()
    {
        shopPanel.SetActive(false);
    }

    public void CoinPurchaseButton(int price)
    {
        AddCoins(price);
    }

    public void RemoveAdsButton()
    {
        adsBuyButton.GetComponent<Button>().interactable = false;
        adsBuyButton.GetComponentInChildren<TextMeshProUGUI>().text = "Active";
    }

    public void AddCoins(int coinAmount)
    {
        Apply(new CoinTx(System.Guid.NewGuid().ToString("N"), coinAmount, CoinReason.Purchase), coinAmount);
    }

    // Mağaza işlem kimliği tekrarlanan callback'in iki kez kredilenmesini engeller.
    public void GrantPurchase(int coinAmount, string transactionId)
    {
        var id = string.IsNullOrEmpty(transactionId) ? System.Guid.NewGuid().ToString("N") : transactionId;
        Apply(new CoinTx(id, coinAmount, CoinReason.Purchase), coinAmount);
    }

    // async void: sonuç gerçek async çağrıya dönünce de bloklamadan okunur; hata yutulmaz, uyarıya yazılır.
    static async void Apply(CoinTx tx, int coinAmount)
    {
        try
        {
            if (!await GameServices.Wallet.ApplyAsync(tx))
                Debug.LogWarning("[SHOP] coin credit not applied, amount=" + coinAmount);
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("[SHOP] coin credit failed (" + e.GetType().Name + "), amount=" + coinAmount);
        }
    }

}
