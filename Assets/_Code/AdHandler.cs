using System;
using System.Collections;
using UnityEngine;
using GoogleMobileAds.Api;
using GoogleMobileAds.Ump.Api;
using System.Collections.Generic;
using PimDeWitte.UnityMainThreadDispatcher;

public class AdHandler : MonoSingleton<AdHandler> {

    [Header("IDs")]

    //[SerializeField] private string _androidAppId;
    [SerializeField] private string _androidInterstitialId;
    [SerializeField] private string _androidRewardedId;

    [Space(24)]

    //[SerializeField] private string _iosAppId;
    [SerializeField] private string _iosInterstitialId;
    [SerializeField] private string _iosRewardedId;
    //private string _appId;
    private string _interstitialId;
    private string _rewardedId;

    private InterstitialAd _interstitialAdCache;
    private RewardedAd _rewardedAdCache;

    private bool _adShowingCurrently = false;
    private bool _waitForConsent = true;

    public bool RewardedAvailable { get { return _rewardedAdCache != null; } }

    [Header("Debug")]

    [SerializeField] private bool _debugLogs;

    protected override void Awake() {
        base.Awake();

#if UNITY_ANDROID
        //_appId = _androidAppId;
        _interstitialId = _androidInterstitialId;
        _rewardedId = _androidRewardedId;
#else
        //_appId = _iosAppId;
        _interstitialId = _iosInterstitialId;
        _rewardedId = _iosRewardedId;
#endif

        ConsentInformation.Reset();

        ConsentDebugSettings debugSettings = new ConsentDebugSettings {
            DebugGeography = DebugGeography.EEA,  // Geography appears as in EEA for debug devices.
            TestDeviceHashedIds = new List<string> {
                "b031baca-d0ad-413c-9e47-54408bc2665c"
            }
        };
        ConsentRequestParameters consentRequest = new ConsentRequestParameters {
            TagForUnderAgeOfConsent = false, //Here false means users are not under age.
            ConsentDebugSettings = debugSettings,
        };

        ConsentInformation.Update(consentRequest, consentError => {
            if (consentError == null) _waitForConsent = false;
            else DebugConsole.Instance.PrintToConsole($"OnConsentInfoUpdated() - Error n {consentError.ErrorCode} :\n{consentError.Message}");
        });

        StartCoroutine(OnConsentInfoUpdatedRoutine());
    }

    private IEnumerator OnConsentInfoUpdatedRoutine() {
        while (_waitForConsent) yield return null;

        ConsentForm.LoadAndShowConsentFormIfRequired(formError => {
            if (formError != null) DebugConsole.Instance.PrintToConsole($"LoadAndShowConsentFormIfRequired() - Error n {formError.ErrorCode} :\n{formError.Message}");
            else if (ConsentInformation.CanRequestAds()) {
                MobileAds.RaiseAdEventsOnUnityMainThread = true;
                MobileAds.Initialize(initStatus => {
                    if (_debugLogs) Debug.Log("AdMob Initialized");

                    LoadInterstitial();
                    LoadRewarded();
                });
            }
        });
    }

    private void LoadInterstitial() {
        if (_interstitialAdCache == null) {
            AdRequest adRequest = new AdRequest();
            adRequest.Keywords.Add("unity-admob-sample");

            InterstitialAd.Load(_interstitialId, adRequest, (InterstitialAd interstitialAd, LoadAdError loadError) => {
                if (loadError != null || interstitialAd == null) {
                    StartCoroutine(DelayRetryLoadAd(false));
                    DebugConsole.Instance.PrintToConsole($"Could not load Interstitial Ad properly. \nError: {loadError}");
                }
                else {
                    _interstitialAdCache = interstitialAd;
                    _interstitialAdCache.OnAdFullScreenContentClosed += RenewInterstitial;
                    _interstitialAdCache.OnAdFullScreenContentFailed += RenewInterstitial;

                    if (_debugLogs) Debug.Log("Interstitial Loaded");
                }
            });
        }
    }

    private void RenewInterstitial() {
        _adShowingCurrently = false;
        _interstitialAdCache.Destroy();
        _interstitialAdCache = null;
        LoadInterstitial();
    }

    private void RenewInterstitial(AdError error) {
        DebugConsole.Instance.PrintToConsole($"Error ocurred, loading new Interstitial Ad. \nError: {error}");
        RenewInterstitial();
    }

    public void ShowInterstitial() {
        if (!_adShowingCurrently) {
            if (_interstitialAdCache != null) {
                _adShowingCurrently = true;
                _interstitialAdCache.Show();
            }
            else if (_debugLogs) Debug.LogWarning("Interstitial Ad was not Ready to Show");
        }
    }

    private void LoadRewarded() {
        if (_rewardedAdCache == null) {

            AdRequest adRequest = new AdRequest();
            adRequest.Keywords.Add("unity-admob-sample");

            RewardedAd.Load(_rewardedId, adRequest, (RewardedAd rewardedAd, LoadAdError loadError) => {

                if (loadError != null || rewardedAd == null) {
                    StartCoroutine(DelayRetryLoadAd(true));
                    DebugConsole.Instance.PrintToConsole($"Could not load Rewarded Ad properly. \nError: {loadError}");
                }
                else {
                    _rewardedAdCache = rewardedAd;
                    _rewardedAdCache.OnAdFullScreenContentClosed += RenewRewarded;
                    _rewardedAdCache.OnAdFullScreenContentFailed += RenewRewarded;

                    if (_debugLogs) Debug.Log("Rewarded Loaded");
                }
            });
        }
    }

    private void RenewRewarded() {
        _adShowingCurrently = false;
        _rewardedAdCache.Destroy();
        _rewardedAdCache = null;
        LoadRewarded();
    }

    private void RenewRewarded(AdError error) {
        DebugConsole.Instance.PrintToConsole($"Error ocurred, loading new Rewarded Ad. \nError: {error}");
        RenewRewarded();
    }

    public bool ShowRewarded(Action rewardAction) {
        if (!_adShowingCurrently) {
            if (_rewardedAdCache != null && _rewardedAdCache.CanShowAd()) {
                _adShowingCurrently = true;
                _rewardedAdCache.Show(reward => UnityMainThreadDispatcher.Instance.Enqueue(rewardAction));
                return true;
            }
            if (_debugLogs) Debug.LogWarning("Rewarded Ad was not Ready to Show");
        }
        return false;
    }

    private IEnumerator DelayRetryLoadAd(bool isRewarded) {
        yield return new WaitForSeconds(5f);

        if (isRewarded) LoadRewarded();
        else LoadInterstitial();
    }

    // Debug
    public void ShowPrivacyForm() {
        ConsentForm.ShowPrivacyOptionsForm(formError => {
            Debug.Log("ConsentForm.ShowPrivacyOptionsForm()'s onDismissed callback");
            if (formError != null) {
                Debug.Log($"Privacy Options Requirement Status: {ConsentInformation.PrivacyOptionsRequirementStatus}");
                Debug.Log($"Consent Status: {ConsentInformation.ConsentStatus}");
                Debug.Log($"Is Consent Form Available: {ConsentInformation.IsConsentFormAvailable()}");
                Debug.Log($"Can Request Ads: {ConsentInformation.CanRequestAds()}");
            }
            else Debug.Log($"FormError [{formError.ErrorCode}]:\n{formError.Message}");
        });
    }

    public void ResetConsent() {
        ConsentInformation.Reset();
        Application.Quit();
    }

}
