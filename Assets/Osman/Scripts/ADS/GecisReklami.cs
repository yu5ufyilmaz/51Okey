using System.Collections;
using System.Collections.Generic;
using GoogleMobileAds.Api;
using GoogleMobileAds.Api.AdManager;
using UnityEngine;

public class GecisReklami : MonoBehaviour
{
            // ca-app-pub-3940256099942544/1033173712 /// Bu Geçiş reklamları için Deneme için verilen key i Oyunu çıkarma aşamasında kendi keylerimizi kullnacağız.
        #if UNITY_EDITOR
            string _adUnitId = "ca-app-pub-3940256099942544/1033173712";
        #elif UNITY_IPHONE
            string _adUnitId = "ca-app-pub-3940256099942544/4411468910";
        #else
            string _adUnitId = "unused";
        #endif

    InterstitialAd _GecisReklami;
    void Start()
    {
        MobileAds.Initialize((InitializationStatus initStatus) =>
        {
            Debug.Log("Mobile Ads SDK başlatıldı");
        });
        GecisReklamiOlustur();
    }


    public void GecisReklamiOlustur()
    {
        if(_GecisReklami != null)
        {
            _GecisReklami.Destroy();
            _GecisReklami = null;
        }

        var _AdRequest= new AdRequest();///Her hangibir hatada burayı tekrar kontrol et kesin burda bir şey varrrrrrr
        InterstitialAd.Load(_adUnitId, _AdRequest, 
        (InterstitialAd Ad, LoadAdError error) =>
        {
            if(error != null|| Ad == null)
            {
                Debug.LogError("Reklam yüklenirken hata oluştu HATA : " + error);
                return;
            }

            _GecisReklami = Ad;
            
        });
    }

    public void GecisReklamiGoster()
    {
        if(_GecisReklami != null&& _GecisReklami.CanShowAd())
        {
            _GecisReklami.Show();
            Debug.Log("Reklamı görüntüle");
        }
        else
        {
            Debug.Log("Geçiş reklamı hazır değil");
        }
    }

    public void GecisReklamiYoket()
    {
        _GecisReklami.Destroy();
    }

}
