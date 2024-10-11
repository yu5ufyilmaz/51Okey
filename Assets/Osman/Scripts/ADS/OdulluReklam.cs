using System.Collections;
using System.Collections.Generic;
using GoogleMobileAds.Api;
using UnityEngine;

public class OdulluReklam : MonoBehaviour
{
#if UNITY_EDITOR
    string _adUnitId = "ca-app-pub-3940256099942544/5224354917";
#elif UNITY_IPHONE
            string _adUnitId = "ca-app-pub-3940256099942544/1712485313";
#else
            string _adUnitId = "unused";
#endif
    RewardedAd _OdulluReklam;
    // Start is called before the first frame update
    void Start()
    {
        MobileAds.Initialize((InitializationStatus initStatus)=>
        {});
        OdulluReklamOlustur();

    }

    public void OdulluReklamOlustur()
    {
        if(_OdulluReklam != null)
        {
            _OdulluReklam.Destroy();
            _OdulluReklam = null;
        }

        var _AdRequest= new AdRequest();///Her hangibir hatada burayı tekrar kontrol et kesin burda bir şey varrrrrrr
        RewardedAd.Load(_adUnitId, _AdRequest, 
        (RewardedAd Ad, LoadAdError error) =>
        {
            if(error != null|| Ad == null)
            {
                Debug.LogError("Ödüllü reklam yüklenirken hata oluştu HATA : " + error);
                return;
            }

            _OdulluReklam = Ad;
            
        });
        OdulluReklamOlaylariniDinle(_OdulluReklam);
    }

    void OdulluReklamOlaylariniDinle(RewardedAd ad)
    {
        ad.OnAdPaid += (AdValue adValue) =>
        {
            Debug.Log(string.Format("OnAdPaid: {0}",
            adValue.Value,
            adValue.CurrencyCode));
        };

        ad.OnAdImpressionRecorded += () =>
        {
            Debug.Log("Ödüllü reklam gösterildi.");
        };

        ad.OnAdClicked += () =>
        {
            Debug.Log("Ödüllü reklama tıklandı.");
        };

        ad.OnAdFullScreenContentOpened += () =>
        {
            Debug.Log("Ödüllü reklam tam ekran açıldı.");
        };
        //Bu ikisi şart.
        ad.OnAdFullScreenContentClosed += () =>
        {

           Debug.Log("Ödüllü ekran tam ekran kapanıldı.");
           OdulluReklamOlustur();
        };

        ad.OnAdFullScreenContentFailed += (AdError error) =>
        {
            Debug.Log(string.Format("OnAdFullScreenContentFailed: {0}", error));
            OdulluReklamOlustur();
        };
    }

    public void OdulluReklamGoster()
    {
        const string OdulMesaji="Ödül kazanıldı, Ürün {0},Değer {1}";
        if(_OdulluReklam != null&& _OdulluReklam.CanShowAd())
        {
            _OdulluReklam.Show((Reward reward)=>
            {
                Debug.Log(string.Format(OdulMesaji, reward.Type, reward.Amount));
            });
        } 
        else
        {
            Debug.Log("Ödullu reklamı hazır değil！");
        }
    }

}
