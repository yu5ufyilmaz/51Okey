using System.Collections;
using System.Collections.Generic;
using GoogleMobileAds.Api;
using UnityEngine;

public class BannerReklami : MonoBehaviour
{
#if UNITY_EDITOR
    string _adUnitId = "ca-app-pub-3940256099942544/6300978111";
#elif UNITY_IPHONE
            string _adUnitId = "ca-app-pub-3940256099942544/2934735716";
#else
            string _adUnitId = "unused";
#endif
    BannerView _bannerView;

    void Start()
    {
        MobileAds.Initialize((InitializationStatus initStatus) =>
        {

        });
        BannerYukle();
    }

    void BannerOlustur()
    {
        if (_bannerView != null)
        {
            _bannerView.Destroy();
            _bannerView = null;
        }
        AdSize adSize = new AdSize(728, 90);
        _bannerView = new BannerView(_adUnitId, adSize, AdPosition.Bottom);
    }

    void BannerYukle()
    {
        if (_bannerView == null)
        {
            BannerOlustur();
            var _AdRequest = new AdRequest();

            _bannerView.LoadAd(_AdRequest);
            BannerReklamOlaylariniDinle();
        }

    }
    void BannerReklamOlaylariniDinle()
    {
        _bannerView.OnBannerAdLoaded += () =>
        {
            Debug.Log("Banner reklamı yüklandı");
        };

        _bannerView.OnBannerAdLoadFailed += (LoadAdError error) =>
        {
            Debug.Log("Banner Yüklenmedi. HATA : " + error);
            //BannerYukle();
        };
    }
}
