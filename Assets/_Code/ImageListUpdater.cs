using System.Collections;
using UnityEngine;
using UnityEngine.Networking;
using Firebase.Extensions;
using Firebase.Storage;
using System.Collections.Generic;
using System;
using UnityEngine.UI;
using TMPro;
using System.Linq;

public class ImageListUpdater : MonoSingleton<ImageListUpdater> {

    [Header("Firebase")]

    [SerializeField] private string _firebaseProjectUrl;
    [SerializeField] private string _folderName;
    [SerializeField] private string _imgInfoJsonName;

    // Could be discarded for less RAM usage
    private FirebaseStorage _firebaseStorage;
    private StorageReference _firebaseStorageImageFolderReference;

    [Header("Image Listing")]

    [SerializeField] private Transform _btnSectionsContainer;
    [SerializeField] private GameObject _btnSectionPickerPrefab;

    [SerializeField] private Transform _imgSectionsContainer;
    private RectTransform _imgSectionsContainerRectTransform;
    [SerializeField] private GameObject _imgSectionPrefab;
    [SerializeField] private GameObject _imgOptionPrefab;
    private List<ImageCaracteristics>[] _coloringImagesListAll = new List<ImageCaracteristics>[0];

    [SerializeField] private Sprite[] _lockStateSprites;

    [Header("Switching Mode")]

    [SerializeField] private CanvasGroup _selectionMenu;
    [SerializeField] private CanvasGroup _paintScreen;

    private ImageInfoJson _infoJson;
    private bool _infoJsonInitialized = false;
    private List<Transform> _sectionGridTransforms = new List<Transform>();
    private int canDynamicallyLoad = 1;

    [Header("Dynamic Loading")]

    [SerializeField] private int _imageRowsToLoadFromTop;
    private float _heightToLoadImage;
    private float _imageHeight;
    private int _currentSection = 0; //

    private bool _wasUpdatedJson = false;
    private int _currentlyInstantiatingImage = 0;

    [Header("Scrollbar")]

    [SerializeField] private RectTransform _scrollbar;
    private Vector2 _scrollbarPosCache;
    [SerializeField] private float _scrollbarHeightMax;
    [SerializeField] private float _scrollbarBottomMin;
    private float _scrollbarTotalDistance;

    [Space(24)]

    [HideInInspector] private RectTransform _verticalScrollCurrent;

    [SerializeField] private RectTransform _scrollrect;
    private RectTransform[] _scrollRectTransforms;
    private float _canvasHeight;

    [Header("Context Popup")]

    [SerializeField] private GameObject _contextPopup;
    [SerializeField] private RawImage _contextPopupTex;
    private ImageCaracteristics _currentImgCaracteristics;

    [Header("Ad Popup")]

    [SerializeField] private GameObject _adPopup;
    [SerializeField] private RawImage _adPopupTex;
    [SerializeField] private Button _adPopupUnlockBtn;
    [SerializeField] private GameObject _adLoadingScreen;

    [Header("Pre-Download Images")]

    [SerializeField] private ImageInfoJson _preDownloadedJson;
    [System.Serializable]
    class PreDownloadedCategory {
        public Texture2D[] images;
    }
    [SerializeField] private PreDownloadedCategory[] _preDownloadedCategory;
    private int[] _orderInCategoryOffset = new int[0];

    [Header("Action Popup")]

    [SerializeField] private CanvasGroup _actionPopup;
    private TextMeshProUGUI _actionPopupText;
    [SerializeField] private float _actionPopupDuration;

    protected override void Awake() {
        base.Awake();

        _imgSectionsContainerRectTransform = _imgSectionsContainer.GetComponent<RectTransform>();
        _canvasHeight = FindObjectOfType<Canvas>().GetComponent<RectTransform>().sizeDelta.y;
        _scrollbarPosCache = _scrollbar.anchoredPosition;
        _scrollbarTotalDistance = _scrollbarHeightMax - ((_scrollbarBottomMin - _canvasHeight) + _scrollbar.sizeDelta.y);
    }

    private void Start() {
        _infoJson = _preDownloadedJson;
        UpdateListArrayCategoryAmount(_infoJson.categories.Length);
        UpdateImageSectionsAmount();
        canDynamicallyLoad += _infoJson.files.Length;
        foreach (ImageInfo imageInfo in _infoJson.files) StartCoroutine(LoadTexture("", imageInfo, true));
        CategorySwitching.Instance.RescaleVerticalScroll(0);

        _firebaseStorage = FirebaseStorage.DefaultInstance;
        _firebaseStorageImageFolderReference = _firebaseStorage.GetReferenceFromUrl(_firebaseProjectUrl).Child(_folderName);

        // Get JSON File
        _firebaseStorageImageFolderReference.Child(_imgInfoJsonName).GetDownloadUrlAsync().ContinueWithOnMainThread(task => {
            if (task.IsFaulted || task.IsCanceled) Debug.LogError("Error getting JSON reference");
            else StartCoroutine(LoadJsonFromUrl(task.Result.ToString()));
        });

        StartCoroutine(TimeoutLoadLocalJson());
    }

    private void Update() {
        DinamicallyLoadImages();
        UpdateScrollbarPos();
        //CheckForSwipeChangeCategory();
    }

    #region UI_Setup
    private void DinamicallyLoadImages() {
        if (canDynamicallyLoad == 0) {
            if (GetSectionGridLoadPosition() > _sectionGridTransforms[_currentSection].childCount && _currentlyInstantiatingImage <= 0) {
                ImageInfo[] imagesToLoad = _infoJson.files.Where(imgInfo => imgInfo.category == _currentSection - 1 && (
                                                                            imgInfo.order == _sectionGridTransforms[_currentSection].childCount - _orderInCategoryOffset[_currentSection - 1] ||
                                                                            imgInfo.order == _sectionGridTransforms[_currentSection].childCount - _orderInCategoryOffset[_currentSection - 1] + 1)).ToArray();
                foreach (ImageInfo imageInfo in imagesToLoad) {
                    _currentlyInstantiatingImage++;
                    if (Application.internetReachability == NetworkReachability.NotReachable) {
                        StartCoroutine(LoadTexture("", imageInfo));

                        continue;
                    }
                    _firebaseStorageImageFolderReference.Child(imageInfo.fileName).GetDownloadUrlAsync().ContinueWithOnMainThread(task => {
                        if (task.IsFaulted || task.IsCanceled) Debug.Log($"Error getting image {imageInfo.fileName} reference");
                        else StartCoroutine(LoadTexture(task.Result.ToString(), imageInfo));
                    });
                }
            }
        }
    }

    private void UpdateScrollbarPos() {
        _scrollbarPosCache[1] = _scrollbarHeightMax + (_scrollbarTotalDistance * (-_verticalScrollCurrent.anchoredPosition.y / (_verticalScrollCurrent.sizeDelta.y - (_canvasHeight + _scrollbarHeightMax - _scrollbarBottomMin))));
        _scrollbar.anchoredPosition = Vector2.Lerp(_scrollbar.anchoredPosition, _scrollbarPosCache, 0.5f);
    }

    private void ChangeCategory(int newCurrentSection, RectTransform scrollBarTarget) {
        if (newCurrentSection != _currentSection) {
            UpdateCategory((byte)newCurrentSection, scrollBarTarget);
            CategorySwitching.Instance.SwitchToCategory((byte)newCurrentSection);
        }
    }

    public void UpdateCategory(byte btnId, RectTransform scrollBarTarget) {
        _currentSection = btnId;
        _verticalScrollCurrent = scrollBarTarget;
        SelectionMenu.Instance.UpdateTabPickerImage(_btnSectionsContainer.GetChild(btnId + 1).GetComponent<RectTransform>());
    }

    private IEnumerator LoadJsonFromUrl(string url) {
        //_debugText.text = "Started Loading JSON from url";
        UnityWebRequest jsonRequest = UnityWebRequest.Get(url);

        yield return jsonRequest.SendWebRequest();

        if (!_infoJsonInitialized) {
            if (jsonRequest.result == UnityWebRequest.Result.ConnectionError || jsonRequest.result == UnityWebRequest.Result.ProtocolError) {
                Debug.Log(jsonRequest.error);
            }
            else {
                _infoJsonInitialized = true;
                _infoJson = JsonUtility.FromJson<ImageInfoJson>(jsonRequest.downloadHandler.text);
                if (_infoJson.version > SaveSystem.Instance.ReadLocalJsonVersion().version) {
                    //_debugText.text = "JSON loaded successfuly from url";
                    Debug.Log("Version Updated");
                    _wasUpdatedJson = true;
                    SaveSystem.Instance.WriteJsonVersion(_infoJson);
                }
                //else _debugText.text = "JSON loaded from local Instead";
                // Get each image
                UpdateListArrayCategoryAmount(_infoJson.categories.Length);
                UpdateImageSectionsAmount();
                canDynamicallyLoad--;
            }
        }
    }

    private void UpdateListArrayCategoryAmount(int totalCategoryAmount) {
        List<ImageCaracteristics>[] listArray = new List<ImageCaracteristics>[totalCategoryAmount];
        int[] intArr = new int[totalCategoryAmount];
        for (int i = 0; i < listArray.Length; i++) {
            listArray[i] = i < _coloringImagesListAll.Length ? _coloringImagesListAll[i] : new List<ImageCaracteristics>();
            intArr[i] = i < _orderInCategoryOffset.Length ? _orderInCategoryOffset[i] : 0;
        }
        _coloringImagesListAll = listArray;
        _orderInCategoryOffset = intArr;
    }

    private void UpdateImageSectionsAmount() {
        CanvasScaler cScaler = GetComponent<CanvasScaler>();
        RectTransform parentScale = GetComponent<RectTransform>();
        Button sectionPicker;
        RectTransform sectionPickerRectTransform;
        TextMeshProUGUI sectionBtnText;
        float buttonDistance = 62.5f;
        _scrollRectTransforms = new RectTransform[_coloringImagesListAll.Length + 1];
        for (int i = 0; i < _coloringImagesListAll.Length + 1; i++) if (i >= _sectionGridTransforms.Count) {
                int AAA = i - 1;
                // Section
                _scrollRectTransforms[i] = Instantiate(_imgSectionPrefab, _imgSectionsContainer).transform.GetChild(0).GetComponent<RectTransform>();
                if (i == 0) _verticalScrollCurrent = _scrollRectTransforms[AAA + 1].GetComponent<RectTransform>();
                GridLayoutGroup gridLGroup = _scrollRectTransforms[i].GetComponentInChildren<GridLayoutGroup>();
                _sectionGridTransforms.Add(gridLGroup.transform);
                gridLGroup.gameObject.SetActive(true/*AAA == -1*/); // -1 or 0
                float spacingProportion = gridLGroup.spacing.x * (parentScale.sizeDelta.x / cScaler.referenceResolution.x);
                gridLGroup.padding.top = (int)spacingProportion;
                gridLGroup.padding.right = (int)spacingProportion;
                gridLGroup.padding.left = (int)spacingProportion;
                gridLGroup.padding.bottom = (int)spacingProportion;
                gridLGroup.cellSize *= (parentScale.sizeDelta.x / cScaler.referenceResolution.x);
                gridLGroup.spacing = Vector2.one * spacingProportion;

                // ScrollRect
                //_scrollrectGridLayout.cellSize = (gridLGroup.padding.left + (2f * (gridLGroup.cellSize.x + gridLGroup.spacing.x)) + gridLGroup.padding.right) * Vector2.one;
                //_categoryRectTransform = gridLGroup.GetComponent<RectTransform>();

                // Dynamic image load
                _imageHeight = gridLGroup.cellSize.y + gridLGroup.spacing.y;
                _heightToLoadImage = _imageHeight * _imageRowsToLoadFromTop + gridLGroup.padding.top;

                // Buttons
                sectionPicker = Instantiate(_btnSectionPickerPrefab, _btnSectionsContainer).GetComponent<Button>();
                sectionPickerRectTransform = sectionPicker.GetComponent<RectTransform>(); // //
                sectionPicker.onClick.AddListener(() => ChangeCategory(AAA + 1, _scrollRectTransforms[AAA + 1]));
                sectionBtnText = sectionPicker.GetComponentInChildren<TextMeshProUGUI>();
                sectionBtnText.text = AAA == -1 ? "New" : _infoJson.categories[AAA];
                sectionPickerRectTransform.sizeDelta = new(sectionBtnText.preferredWidth, sectionPickerRectTransform.sizeDelta.y);
                sectionPickerRectTransform.anchoredPosition += Vector2.right * buttonDistance;
                buttonDistance += sectionPickerRectTransform.sizeDelta.x + 62.5f;

                if (AAA == -1) { // -1 or 0
                    SelectionMenu.Instance.ChangeCurrentObject(gridLGroup.gameObject);
                    SelectionMenu.Instance.UpdateTabPickerImage(sectionPickerRectTransform);
                }
            }
        _btnSectionsContainer.GetComponent<RectTransform>().sizeDelta += Vector2.right * buttonDistance;
        _imgSectionsContainer.GetComponent<RectTransform>().sizeDelta = new Vector2(parentScale.sizeDelta.x, _imgSectionsContainer.GetComponent<RectTransform>().sizeDelta.y);

        ScrollRect[] scrollRects = new ScrollRect[_scrollRectTransforms.Length];
        for (int i = 0; i < scrollRects.Length; i++) scrollRects[i] = _scrollRectTransforms[i]?.parent.GetComponent<ScrollRect>();
        CategorySwitching.Instance.SetVerticalScrolls(scrollRects);
    }

    //public void RepositionImageSliderInGridLayout(int id) {
    //    int prevPad = _scrollrectGridLayout.padding.left;
    //    _scrollrectGridLayout.padding.left = (int)(id * -_categoryRectTransform.sizeDelta.x);
    //    _scrollrect.anchoredPosition += (prevPad - _scrollrectGridLayout.padding.left) * Vector2.right;

    //    LayoutRebuilder.ForceRebuildLayoutImmediate(_scrollrect);
    //}
    #endregion

    #region Loading
    private IEnumerator LoadTexture(string url, ImageInfo info, bool isPreDownloaded = false) {
        int x = info.category;
        int y = info.order + (isPreDownloaded ? 0 : _orderInCategoryOffset[x]);

        // Wait to spawn
        while (_sectionGridTransforms[x + 1].childCount < y) yield return null;

        // Instantiate Image Button
        Button imgOption = Instantiate(_imgOptionPrefab, _sectionGridTransforms[x + 1]).transform.GetChild(0).GetComponent<Button>();
        Button imgOptionInNew = Instantiate(_imgOptionPrefab, _sectionGridTransforms[0]).transform.GetChild(0).GetComponent<Button>();
        if (!isPreDownloaded) _currentlyInstantiatingImage--;
        CategorySwitching.Instance.RescaleVerticalScroll((byte)(info.category + 1));

        // Update LockState
        Image imgOptionLock = imgOption.transform.GetChild(0).GetComponent<Image>();
        imgOptionLock.enabled = info.purcheaseStatus != 0;
        imgOptionLock.sprite = _lockStateSprites[info.purcheaseStatus];
        imgOptionLock = imgOptionInNew.transform.GetChild(0).GetComponent<Image>();
        imgOptionLock.enabled = info.purcheaseStatus != 0;
        imgOptionLock.sprite = _lockStateSprites[info.purcheaseStatus];

        // Get the texture to be used on the buton
        Texture2D textureToUse = new Texture2D(0, 0);
        bool couldReadImage = false;
        if (isPreDownloaded) {
            textureToUse = SaveSystem.Instance.ReadImageColored(info.fileName, true);
            if (textureToUse == null) textureToUse = _preDownloadedCategory[x].images[y];
            couldReadImage = true;

            _orderInCategoryOffset[info.category]++;
            canDynamicallyLoad--;
        }
        else if (!_wasUpdatedJson) {
            textureToUse = SaveSystem.Instance.CheckSaveStateReturnTexToUse(info.fileName);
            if (textureToUse) couldReadImage = true;
        }
        if (_wasUpdatedJson || !couldReadImage) {
            UnityWebRequest imgRequest = UnityWebRequestTexture.GetTexture(url);

            yield return imgRequest.SendWebRequest();

            if (imgRequest.result != UnityWebRequest.Result.ConnectionError && imgRequest.result != UnityWebRequest.Result.ProtocolError) {
                textureToUse = ((DownloadHandlerTexture)imgRequest.downloadHandler).texture;
                SaveSystem.Instance.WriteImageBase(textureToUse, info.fileName);
            }
            else Debug.Log(imgRequest.error);
        }

        // Wait to add image to list
        while (_coloringImagesListAll[x].Count < y) yield return null;

        _coloringImagesListAll[x].Add(new ImageCaracteristics(info.fileName, info.purcheaseStatus));
        _coloringImagesListAll[x][y] = new ImageCaracteristics(_coloringImagesListAll[x][y], new RawImage[2] { imgOption.GetComponent<RawImage>(), imgOptionInNew.GetComponent<RawImage>() }, new Vector2Int(x, y)); // Could shrink 1 line?
        foreach (RawImage image in _coloringImagesListAll[x][y].selectionImages) {
            image.texture = textureToUse;

            // Stop "loading" Animation
            Animator animLoading = image.transform.GetChild(1).GetComponent<Animator>();
            //animLoading.SetTrigger("StopLoad"); // Not Needed
            animLoading.gameObject.SetActive(false);

            // Setup Button Functionality
            image.GetComponent<Button>().onClick.AddListener(() => EnterImageEdition(_coloringImagesListAll[x][y]));
        }
    }

    public IEnumerator TimeoutLoadLocalJson() {
        yield return new WaitForSeconds(5f);

        if (!_infoJsonInitialized) {
            _infoJsonInitialized = true;
            _infoJson = SaveSystem.Instance.ReadLocalJsonVersion();

            UpdateListArrayCategoryAmount(_infoJson.categories.Length);
            UpdateImageSectionsAmount();
            canDynamicallyLoad--;
        }
    }
    #endregion

    #region UI_Operations
    public void EnterImageEdition(ImageCaracteristics caracteristics) {
        CategorySwitching.Instance.AllowScroll(false);
        switch (caracteristics.purcheaseStatus) {
            case 0:
                if (SaveSystem.Instance.ReadImageColored(caracteristics.name) == null) ContinueBtn(caracteristics.name);
                else OpenContextPopup(caracteristics);
                break;
            case 1:
                ShowAdPopUp(caracteristics);
                break;
            case 2:
                ShowAdPopUp(caracteristics); // Change Later
                break;
        }
    }

    public void ResetDrawableTex(string texName) {
        texName = texName.Split('-')[0];
        Texture2D tex = SaveSystem.Instance.CheckSaveStateReturnTexToUse(texName);
        if (tex == null && texName.Contains("PD_")) tex = GetPreDownloadedTex(texName);
        Vector2Int arrPos = GetImageCaracteristics(texName).arrayPos;

        foreach (RawImage imageOption in _coloringImagesListAll[arrPos.x][arrPos.y].selectionImages) imageOption.texture = tex;
    }

    private void OpenContextPopup(ImageCaracteristics caracteristics) {
        _currentImgCaracteristics = caracteristics;
        _currentImgCaracteristics.name += ":D";
        _contextPopup.SetActive(true);
        _contextPopupTex.texture = caracteristics.selectionImages[0].texture;
    }

    public void ShareBtn() {
        MyWorkUpdater.Instance.ShareBtn(_contextPopupTex.texture);
    }

    public void ContinueBtn(string caracteristicsName = "") {
        SelectionMenu.Instance.ActivateObject(_paintScreen);
        SelectionMenu.Instance.DeactivateObject(_selectionMenu);
        _contextPopup.SetActive(false);
        if (caracteristicsName == "-") {
            _currentImgCaracteristics.name = $"{_currentImgCaracteristics.name.Split(":D")[0]}-{SaveSystem.Instance.CheckWorkedImageAmountWithName(_currentImgCaracteristics.name.Split(":D")[0])}";
            caracteristicsName = "";
        }
        else if (_currentImgCaracteristics.name != null && _currentImgCaracteristics.name.Contains(":D")) {
            _currentImgCaracteristics.name = _currentImgCaracteristics.name.Split(":D")[0];
            int lastCopyAccessed = GetImageCaracteristics(_currentImgCaracteristics.name).lastCopyAccessed;
            string caracteristicsNameLastAcessed = _currentImgCaracteristics.name;
            if (lastCopyAccessed > 0) caracteristicsNameLastAcessed += $"-{lastCopyAccessed}";
            if (SaveSystem.Instance.ReadImageColored(caracteristicsNameLastAcessed) != null) _currentImgCaracteristics.name = caracteristicsNameLastAcessed;
        }
        ColorImageHandler.Instance.SetNewImage(caracteristicsName.Length <= 0 ? _currentImgCaracteristics : GetImageCaracteristics(caracteristicsName));
    }

    public void StartNew() {
        ContinueBtn("-");
    }
    #endregion

    #region Ads
    private void ShowAdPopUp(ImageCaracteristics caracteristics) {
        _adPopup.SetActive(true);
        _adPopupTex.texture = caracteristics.name.Substring(0, 3) == "PD_" ? Resources.Load<Texture2D>(caracteristics.name) : SaveSystem.Instance.ReadImageBase(caracteristics.name);
        _adPopupUnlockBtn.onClick.RemoveAllListeners();
        _adPopupUnlockBtn.onClick.AddListener(() => {
            if (!AdHandler.Instance.ShowRewarded(() => CloseAdToUnlockSuccess(caracteristics.arrayPos))) StartCoroutine(WaitForRewardedAdLoad(caracteristics));
        });
    }

    private IEnumerator WaitForRewardedAdLoad(ImageCaracteristics caracteristics) {
        float timer = 6f;
        _adLoadingScreen.SetActive(true);
        while (AdHandler.Instance.RewardedAvailable) {
            timer -= Time.unscaledDeltaTime;

            if (timer <= 0) {
                StartCoroutine(ActionPopup("No Ad Available"));
                _adLoadingScreen.SetActive(false);

                yield break;
            }
        }
        _adLoadingScreen.SetActive(true);
        AdHandler.Instance.ShowRewarded(() => CloseAdToUnlockSuccess(caracteristics.arrayPos));
    }

    private void CloseAdToUnlockSuccess(Vector2Int arrayPos) {
        _adPopup.SetActive(false);
        _coloringImagesListAll[arrayPos.x][arrayPos.y] = new ImageCaracteristics(_coloringImagesListAll[arrayPos.x][arrayPos.y], 0);
        foreach (RawImage imageOption in _coloringImagesListAll[arrayPos.x][arrayPos.y].selectionImages) imageOption.GetComponentInChildren<Image>().enabled = false;
        ContinueBtn(_coloringImagesListAll[arrayPos.x][arrayPos.y].name);
    }
    #endregion

    public void RescaleImageSliderHeight(int listId) {
        Debug.Log(_scrollRectTransforms[listId] == null);
        GridLayoutGroup sectionGrid = _scrollRectTransforms[listId].GetComponent<GridLayoutGroup>();
        _scrollRectTransforms[listId].sizeDelta = new Vector2(_scrollRectTransforms[listId].sizeDelta.x, sectionGrid.preferredHeight);
    }

    #region Generic
    public ImageCaracteristics GetImageCaracteristics(string name) {
        foreach (List<ImageCaracteristics> caracteristicsList in _coloringImagesListAll) foreach (ImageCaracteristics caracteristics in caracteristicsList) if (caracteristics.name == name.Split('-')[0]) {
                    ImageCaracteristics caracteristicsCopy = caracteristics;
                    caracteristicsCopy.name = name;
                    return caracteristicsCopy;
                }
        Debug.LogError($"Image with name '{name}' not found in _coloringImagesListAll");
        return new ImageCaracteristics();
    }

    private Texture2D GetPreDownloadedTex(string texName) {
        foreach (PreDownloadedCategory category in _preDownloadedCategory) foreach (Texture2D tex in category.images) if (tex.name == texName) return tex;
        return null;
    }
    #endregion

    public void UpdateSelectionTexture(string imageName, Texture2D texToUse, int lastUsedImage) {
        Vector2Int imageGridId = GetImageInfoGridId(imageName.Split("-")[0]);
        _coloringImagesListAll[imageGridId.x][imageGridId.y] = new ImageCaracteristics(_coloringImagesListAll[imageGridId.x][imageGridId.y], lastUsedImage, true);
        foreach (RawImage imageOption in _coloringImagesListAll[imageGridId.x][imageGridId.y].selectionImages) imageOption.texture = texToUse;
    }

    private Vector2Int GetImageInfoGridId(string imageName) {
        for (int i = 0; i < _coloringImagesListAll.Length; i++) {
            for (int j = 0; j < _coloringImagesListAll[i].Count; j++) {
                if (_coloringImagesListAll[i][j].name == imageName) return new Vector2Int(i, j);
            }
        }
        return Vector2Int.zero;
    }

    private float GetSectionGridLoadPosition() { return MathF.Floor(_imgSectionsContainerRectTransform.anchoredPosition.y + _heightToLoadImage) / (_imageHeight / 2); }

    private IEnumerator ActionPopup(string text) {
        _actionPopupText.text = text;

        float i = 1;
        _actionPopup.alpha = 1;
        while (_actionPopup.alpha > 0) {
            if (i > 0) i -= 2 * Time.deltaTime / _actionPopupDuration;
            else _actionPopup.alpha -= 2 * Time.deltaTime / _actionPopupDuration;

            yield return null;
        }
    }
}

[Serializable]
public struct ImageInfoJson {
    public int version;
    public string[] categories;
    public ImageInfo[] files;
    public ImageInfoJson(bool uh) {
        version = -1;
        categories = null;
        files = null;
    }
}

[Serializable]
public struct ImageInfo {
    public string fileName;
    public int category;
    public int order;
    public int purcheaseStatus; // 0 - Unlocked; 1 - Ad Gated; 2 - Pay Gated

    //public ImageInfo(bool uh) {
    //    fileName = "Test";
    //    category = 0;
    //    order = 1;
    //    adGated = false;
    //}
}

public struct ImageCaracteristics {
    public string name;
    public int purcheaseStatus;
    public RawImage[] selectionImages;
    public Vector2Int arrayPos;
    public int lastCopyAccessed;

    public ImageCaracteristics(string name, int purcheaseStatus) {
        this.name = name;
        this.purcheaseStatus = purcheaseStatus;
        selectionImages = null;
        arrayPos = Vector2Int.zero;
        lastCopyAccessed = 0;
    }
    public ImageCaracteristics(ImageCaracteristics previousCaracteristics, RawImage[] selectionImages, Vector2Int arrayPos) {
        name = previousCaracteristics.name;
        purcheaseStatus = previousCaracteristics.purcheaseStatus;
        this.selectionImages = selectionImages;
        this.arrayPos = arrayPos;
        lastCopyAccessed = 0;
    }
    public ImageCaracteristics(ImageCaracteristics previousCaracteristics, int newPurcheaseStatus) {
        name = previousCaracteristics.name;
        purcheaseStatus = newPurcheaseStatus; //
        selectionImages = previousCaracteristics.selectionImages;
        arrayPos = previousCaracteristics.arrayPos;
        lastCopyAccessed = 0;
    }
    public ImageCaracteristics(ImageCaracteristics previousCaracteristics, int lastCopyAccessed, bool nothingLol) {
        name = previousCaracteristics.name;
        purcheaseStatus = previousCaracteristics.purcheaseStatus; //
        selectionImages = previousCaracteristics.selectionImages;
        arrayPos = previousCaracteristics.arrayPos;
        this.lastCopyAccessed = lastCopyAccessed;
    }


}

// UNUSED
public class ImageInformation {
    public string name;
    public int purcheaseStatus;
    private RawImage selectionImage;
    private RawImage myWorkImage;
    private Vector2Int imageListPos;

    public ImageInformation(string name, int purcheaseStatus, RawImage selectionImage, Vector2Int imageListPos) {
        this.name = name;
        this.purcheaseStatus = purcheaseStatus;
        this.selectionImage = selectionImage;
        this.imageListPos = imageListPos;
    }

    public void LoadSaveToUIImage() {

    }

    public string GetBaseImageSavePath() {
        return "";
    }

    public string GetColoredImageSavePath() {
        return "";
    }

    public string GetBucketSavePath() {
        return "";
    }

}