using System;
using System.Collections;
using UnityEngine;
using com.unity3d.mediation;
//using IronSourceJSON;
//using System.Collections.Generic;
//using PimDeWitte.UnityMainThreadDispatcher;

public class AdHandler : MonoSingleton<AdHandler> {

    [Header("IDs")]

    [Space(6)]

    [SerializeField] private string _androidAppId;
    [SerializeField] private string _androidInterstitialId;
    [SerializeField] private string _androidRewardedId;

    [Space(24)]

    [SerializeField] private string _iosAppId;
    [SerializeField] private string _iosInterstitialId;
    [SerializeField] private string _iosRewardedId;

    private string _interstitialId;
    private string _rewardedId;
    private string _appId;

    private LevelPlayInterstitialAd _interstitialAdCache;
    private LevelPlayRewardedAd _rewardedAdCache;

    [SerializeField] private GameObject _adLoadingScreen;

    private bool _adShowingCurrently = false;
    private Action _currentRewardAction;

    public bool RewardedAvailable { get { return _rewardedAdCache.IsAdReady(); } }

    [Header("Debug")]

    [SerializeField] private bool _debugLogs;

    protected override void Awake() {
        base.Awake();

#if UNITY_ANDROID
        _appId = _androidAppId;
        _interstitialId = _androidInterstitialId;
        _rewardedId = _androidRewardedId;
#else
        _appId = _iosAppId;
        _interstitialId = _iosInterstitialId;
        _rewardedId = _iosRewardedId;
#endif

        IronSource.Agent.init(_appId);
        IronSource.Agent.setConsent(true);
        IronSource.Agent.setMetaData("do_not_sell", "false");
        IronSource.Agent.setMetaData("is_deviceid_optout", "true");
        IronSource.Agent.setMetaData("is_child_directed", "true");
        IronSource.Agent.setMetaData("Google_Family_Self_Certified_SDKS", "true");

        IronSourceEvents.onConsentViewDidLoadSuccessEvent += onConsentViewDidLoadSuccessEvent;
        IronSourceEvents.onSdkInitializationCompletedEvent += () => { IronSource.Agent.loadConsentViewWithType("pre"); Debug.Log("loadConsentViewWithType"); };

        LevelPlay.OnInitSuccess += Initializer;
        LevelPlay.OnInitFailed += (levelPlayConfiguration) => Debug.Log($"LevelPlay Init Fail: (code - {levelPlayConfiguration.ErrorCode}) {levelPlayConfiguration.ErrorMessage}");

        LevelPlayAdFormat[] legacyAdFormats = new[] { LevelPlayAdFormat.REWARDED, LevelPlayAdFormat.INTERSTITIAL };
        LevelPlay.Init(_appId, null, legacyAdFormats);

        Debug.Log($"Initializing IronSource with App ID: {_appId}");
    }

    private void onConsentViewDidLoadSuccessEvent(string obj) {
        IronSource.Agent.showConsentViewWithType("pre");

        Debug.Log("Consent View Loaded Successfully");
    }

    private void Initializer(LevelPlayConfiguration configuration) {
        _rewardedAdCache = new LevelPlayRewardedAd(_rewardedId);
        _interstitialAdCache = new LevelPlayInterstitialAd(_interstitialId);

        _rewardedAdCache.OnAdLoaded += (adUnitId) => Debug.Log($"Rewarded Ad Loaded: {adUnitId}");
        _rewardedAdCache.OnAdLoadFailed += (adUnitId) => StartCoroutine(DelayRetryLoadAd(true, adUnitId.AdUnitId));
        _rewardedAdCache.OnAdClosed += RenewRewarded;
        _rewardedAdCache.OnAdRewarded += OnReward;

        _interstitialAdCache.OnAdLoaded += (adUnitId) => Debug.Log($"Interstitial Ad Loaded: {adUnitId}");
        _interstitialAdCache.OnAdLoadFailed += (adUnitId) => StartCoroutine(DelayRetryLoadAd(false, adUnitId.AdUnitId));
        _interstitialAdCache.OnAdClosed += RenewInterstitial;

        LoadInterstitial();
        LoadRewarded();

        Debug.Log("Initializer()");
    }

    private void LoadInterstitial() {
        Debug.Log($"{_interstitialAdCache != null} && !{(_interstitialAdCache != null ? _interstitialAdCache.IsAdReady() : "Can't Compare")}");
        if (_interstitialAdCache != null && !_interstitialAdCache.IsAdReady()) {
            _interstitialAdCache.LoadAd();

            Debug.Log("LoadInterstitial");
        }
    }

    private void RenewInterstitial(LevelPlayAdInfo adInfo) {
        _adShowingCurrently = false;
        LoadInterstitial();
    }

    public void ShowInterstitial() {
        if (!_adShowingCurrently) {
            if (_interstitialAdCache != null && _interstitialAdCache.IsAdReady()) {
                _adShowingCurrently = true;
                _interstitialAdCache.ShowAd();

                Debug.Log("ShowInterstital");
            }
            else StartCoroutine(ShowInterstitialRoutine());
        }
        else Debug.LogWarning("Trying to show interstitial but Ad is already showing!");
    }

    private IEnumerator ShowInterstitialRoutine() {
        Debug.LogWarning("Trying to show interstitial but it isn't loaded! Will play as soon as loaded");

        while (_interstitialAdCache == null) yield return null;
        while (!_interstitialAdCache.IsAdReady()) yield return null;

        _adShowingCurrently = true;
        _interstitialAdCache.ShowAd();

        Debug.Log("ShowInterstital");

    }

    private void LoadRewarded() {
        Debug.Log($"{_rewardedAdCache != null} && !{(_rewardedAdCache != null ? _rewardedAdCache.IsAdReady() : "Can't Compare")}");
        if (_rewardedAdCache != null && !_rewardedAdCache.IsAdReady()) {
            _rewardedAdCache.LoadAd();

            Debug.Log("RewardedAdCache");
        }
    }

    private void RenewRewarded(LevelPlayAdInfo adInfo) {
        _adShowingCurrently = false;
        LoadRewarded();
    }

    public bool ShowRewarded(Action rewardAction) {
        if (!_adShowingCurrently) {
            _currentRewardAction = rewardAction;
            if (_rewardedAdCache != null && _rewardedAdCache.IsAdReady()) {
                _adShowingCurrently = true;
                _rewardedAdCache.ShowAd();

                Debug.Log("ShowRewarded");
                return true;
            }
            StartCoroutine(ShowRewardedRoutine());
        }
        return false;
    }

    private IEnumerator ShowRewardedRoutine() {
        Debug.LogWarning("Trying to show rewarded but it isn't loaded! Will play as soon as loaded");

        _adLoadingScreen.SetActive(true);
        while (_rewardedAdCache == null) yield return null;
        while (!_rewardedAdCache.IsAdReady()) yield return null;
        _adLoadingScreen.SetActive(false);

        _adShowingCurrently = true;
        _rewardedAdCache.ShowAd();

        Debug.Log("ShowRewarded");
    }

    private void OnReward(LevelPlayAdInfo placement, LevelPlayReward adInfo) {
        _currentRewardAction.Invoke();
        _currentRewardAction = null;
    }

    private IEnumerator DelayRetryLoadAd(bool isRewarded, string adUnitId) {
        Debug.LogWarning($"{(isRewarded ? "Rewarded" : "Interstitial")} Ad Failed to Load: {adUnitId}, trying again in 3 seconds");

        yield return new WaitForSeconds(3f);

        if (isRewarded) LoadRewarded();
        else LoadInterstitial();
    }

    void OnApplicationPause(bool isPaused) {
        IronSource.Agent.onApplicationPause(isPaused);
    }

}
