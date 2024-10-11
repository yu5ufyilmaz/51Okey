using System.Collections;
using System.Collections.Generic;
using GoogleMobileAds.Api;
using UnityEngine;

public class OdulluGecisReklam : MonoBehaviour
{
#if UNITY_EDITOR
    string _adUnitId = "ca-app-pub-3940256099942544/5354046379";
#elif UNITY_IPHONE
            string _adUnitId = "ca-app-pub-3940256099942544/6978759866";
#else
            string _adUnitId = "unused";
#endif
    RewardedInterstitialAd _OdulluGecisReklami;
    // Start is called before the first frame update
    void Start()
    {
        MobileAds.Initialize((InitializationStatus initStatus)=>
        {});
        OdulluGecisReklamOlustur();

    }

    void OdulluGecisReklamOlustur()
    {
        if(_OdulluGecisReklami != null)
        {
            _OdulluGecisReklami.Destroy();
            _OdulluGecisReklami = null;
        }

        var _AdRequest= new AdRequest();///Her hangibir hatada burayı tekrar kontrol et kesin burda bir şey varrrrrrr
        RewardedInterstitialAd.Load(_adUnitId, _AdRequest, 
        (RewardedInterstitialAd Ad, LoadAdError error) =>
        {
            if(error != null|| Ad == null)
            {
                Debug.LogError("Ödüllü reklam yüklenirken hata oluştu HATA : " + error);
                return;
            }

            _OdulluGecisReklami = Ad;
            
        });
        OdulluGecisReklamOlaylariniDinle(_OdulluGecisReklami);
    }

    void OdulluGecisReklamOlaylariniDinle(RewardedInterstitialAd ad)
    {
        ad.OnAdPaid += (AdValue adValue) =>
        {
            Debug.Log(string.Format("OnAdPaid: {0}",
            adValue.Value,
            adValue.CurrencyCode));
        };

        ad.OnAdImpressionRecorded += () =>
        {
            Debug.Log("Ödüllü geçiş reklam gösterildi.");
        };

        ad.OnAdClicked += () =>
        {
            Debug.Log("Ödüllü geçiş reklama tıklandı.");
        };

        ad.OnAdFullScreenContentOpened += () =>
        {
            Debug.Log("Ödüllü geçiş reklam tam ekran açıldı.");
        };
        //Bu ikisi şart.
        ad.OnAdFullScreenContentClosed += () =>
        {

           Debug.Log("Ödüllü geçiş ekran tam ekran kapanıldı.");
           OdulluGecisReklamOlustur();
        };

        ad.OnAdFullScreenContentFailed += (AdError error) =>
        {
            Debug.Log(string.Format("OnAdFullScreenContentFailed: {0}", error));
            OdulluGecisReklamOlustur();
        };
    }

    public void OdulluGecisReklamGoster()
    {
        const string OdulMesaji="Ödüllü Geçiş kazanıldı, Ürün {0},Değer {1}";
        if(_OdulluGecisReklami != null&& _OdulluGecisReklami.CanShowAd())
        {
            _OdulluGecisReklami.Show((Reward reward)=>
            {
                Debug.Log(string.Format(OdulMesaji, reward.Type, reward.Amount));
            });
        } 
        else
        {
            Debug.Log("Ödullu geçiş reklamı hazır değil！");
        }
    }

}
