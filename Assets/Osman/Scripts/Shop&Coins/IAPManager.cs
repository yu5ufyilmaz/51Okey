using System.Collections;
using System.Collections.Generic;

using UnityEngine;
using UnityEngine.Purchasing;
using UnityEngine.Purchasing.Extension;

public class IAPManager : MonoBehaviour
{
    private string coins100 = "coin100";
    private string coins200 = "coin200";
    private string coins500 = "coin500";
    private string coins1000 = "coin1000";
    private string removeAds = "removeads";
    public ShopController _shopController;

    public void OnPurchaseComplete(Product product)
    {
        if (product.definition.id == coins100)
        {
            _shopController.CoinPurchaseButton(100);
        }
        else if (product.definition.id == coins200)
        {
            _shopController.CoinPurchaseButton(200);
        }
        else if (product.definition.id == coins500)
        {
            _shopController.CoinPurchaseButton(500);
        }
        else if (product.definition.id == coins1000)
        {
            _shopController.CoinPurchaseButton(1000);
        }
        else if (product.definition.id == removeAds)
        {
            _shopController.RemoveAdsButton();
        }
    }

    public void OnPurchaseFailed(Product product, PurchaseFailureDescription failureDescription)
    {
        Debug.Log(product.definition.id + " purchase failed" + failureDescription);
    }
}
