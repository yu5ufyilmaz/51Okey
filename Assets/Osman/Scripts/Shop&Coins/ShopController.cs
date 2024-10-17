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
        int currentValue;
        if (int.TryParse(coinText.text, out currentValue))
        {
            currentValue += coinAmount;
            coinText.text = currentValue.ToString();
        }
    }

}
