using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class MyWorkUpdater : MonoSingleton<MyWorkUpdater> {

    [Header("Objects")]

    [SerializeField] private RectTransform _scrollRectTransform;
    private GridLayoutGroup _scrollGrid;
    [SerializeField] private GameObject _workedImageBtnPrefab;

    [Header("Tex Loading")]

    [SerializeField] private int _loadImageLines;
    private float _loadImageHeight;

    [Header("PopUp")]

    [SerializeField] private GameObject _popup;
    [SerializeField] private RawImage _popupImage;
    private WorkedImage _currentPopupImage;

    [Header("WaterMarking")]

    [SerializeField] private GameObject _waterMarkRenderer;
    [SerializeField] private RenderTexture _watermarkedTexture;
    [SerializeField] private RawImage _waterMarkedImage;

    [Header("Scrollbar")]

    [SerializeField] private Image _scrollbar;
    [SerializeField] private int _minBtnToShowBar;

    [Header("Cache")]

    private List<WorkedImage> _workedImages;
    private List<RawImage> _workedImagesComponent = new List<RawImage>();

    private void Start() {
        _scrollGrid = _scrollRectTransform.GetComponent<GridLayoutGroup>();

        _loadImageHeight = _scrollGrid.padding.top + _loadImageLines * (_scrollGrid.cellSize.y + _scrollGrid.spacing.y);

        _workedImages = SaveSystem.Instance.GetAllWorkedImages();
        _workedImages.Sort((WorkedImage x, WorkedImage y) => x.GetDateTime().CompareTo(y.GetDateTime()));

        RescaleScrollGrid();
    }

    private void Update() {
        if (_scrollRectTransform.childCount < _workedImages.Count && (_scrollRectTransform.anchoredPosition.y + _loadImageHeight) / (_scrollGrid.cellSize.y / 2) > _scrollRectTransform.childCount) {
            int currentChild = _scrollRectTransform.childCount;
            for (int i = 0; i < (int)((_scrollRectTransform.anchoredPosition.y + _loadImageHeight) / (_scrollGrid.cellSize.y / 2)) - _scrollRectTransform.childCount; i++)
                if (i + _scrollRectTransform.childCount < _workedImages.Count) InstantiateWorkedImage(i + currentChild);

        }
    }

    private void RescaleScrollGrid() {
        RectTransform canvasRectTransform = GetComponent<RectTransform>();
        CanvasScaler canvasScaler = GetComponent<CanvasScaler>();
        float spacingProportion = _scrollGrid.spacing.x * (canvasRectTransform.sizeDelta.x / canvasScaler.referenceResolution.x);
        _scrollGrid.padding.top = (int)spacingProportion;
        _scrollGrid.padding.right = (int)spacingProportion;
        _scrollGrid.padding.left = (int)spacingProportion;
        _scrollGrid.padding.bottom = (int)spacingProportion;
        _scrollGrid.cellSize *= (canvasRectTransform.sizeDelta.x / canvasScaler.referenceResolution.x);
        _scrollGrid.spacing = Vector2.one * spacingProportion;
    }

    private void RescaleScrollRect() {
        _scrollRectTransform.sizeDelta = new Vector2(_scrollRectTransform.sizeDelta.x, _scrollGrid.padding.top + _scrollGrid.padding.bottom + Mathf.Ceil(_scrollRectTransform.childCount / 2f) * (_scrollGrid.cellSize.y + _scrollGrid.spacing.y));
    }


    private void InstantiateWorkedImage(int id) {
        Button workedImageBtn = InstantiateWorkedImageButton();

        _workedImagesComponent.Add(workedImageBtn.GetComponentInChildren<RawImage>());
        _workedImages[id] = new WorkedImage(_workedImages[id], id);
        _workedImagesComponent[id].texture = SaveSystem.Instance.ReadImageColored(_workedImages[id].name);

        string wImg = _workedImages[id].name;
        workedImageBtn.onClick.AddListener(() => FocusWorkedImagePopUp(wImg));
    }

    private Button InstantiateWorkedImageButton() {
        Button workedImageBtn = Instantiate(_workedImageBtnPrefab, _scrollGrid.transform).GetComponentInChildren<Button>();
        workedImageBtn.transform.parent.SetSiblingIndex(0);

        RescaleScrollRect();
        _scrollbar.enabled = _scrollGrid.transform.childCount >= _minBtnToShowBar;
        return workedImageBtn;
    }

    public void UpdateWorkedImageList(WorkedImage workedImage, Texture2D texToUse) { // Review here
        if (workedImage.rawImageIdInList >= 0) {
            _workedImages[workedImage.rawImageIdInList] = workedImage;
            UpdateRawImageTex(workedImage, out Button buttonVar, texToUse);
        }
        else {
            int id = UpdateRawImageTex(workedImage, out Button buttonVar, texToUse);
            _workedImages[id] = new WorkedImage(workedImage, id);
            string focusPopupTargetName = _workedImages[id].name;
            buttonVar.onClick.AddListener(() => FocusWorkedImagePopUp(focusPopupTargetName));
        }

    }

    private int UpdateRawImageTex(WorkedImage workedImage, out Button buttonVar, Texture2D texToUse) {
        int id = workedImage.rawImageIdInList;
        if (workedImage.rawImageIdInList < 0) {
            buttonVar = InstantiateWorkedImageButton();

            _workedImages.Add(new WorkedImage(workedImage, _workedImages.Count));
            _workedImagesComponent.Add(buttonVar.GetComponentInChildren<RawImage>());
            id = _workedImages.Count - 1;
        }
        else buttonVar = null;
        _workedImagesComponent[id].texture = texToUse;
        return id;
    }

    public WorkedImage GetWorkedImageByName(string name) {
        foreach (WorkedImage workedImage in _workedImages) if (workedImage.name == name) return workedImage;

        return new WorkedImage(true);
    }

    private void FocusWorkedImagePopUp(string name) {
        WorkedImage wImg = GetWorkedImageByName(name);
        _currentPopupImage = wImg;
        _popupImage.texture = _workedImagesComponent[wImg.rawImageIdInList].texture;
        _popup.SetActive(true);
    }

    public void DeleteImageBtn(string workedImageName = null) {
        WorkedImage wImg = workedImageName.Length <= 0 ? _currentPopupImage : GetWorkedImageByName(workedImageName);
        if (wImg.name != null) {
            SaveSystem.Instance.DeleteSave(wImg.name);
            Destroy(_workedImagesComponent[wImg.rawImageIdInList].transform.parent.gameObject);
            _workedImagesComponent.RemoveAt(wImg.rawImageIdInList);
            ImageListUpdater.Instance.ResetDrawableTex(wImg.name);

            _workedImages.Remove(wImg);
            for (int i = 0; i < _workedImages.Count; i++) {
                _workedImages[i] = new WorkedImage(_workedImages[i], i);
            }
            RescaleScrollRect();
            _scrollbar.enabled = _scrollGrid.transform.childCount >= _minBtnToShowBar;
        }
        else Debug.Log($"There is no image with name '{workedImageName}' to delete");
    }

    public void CopyBtn() {

    }

    public void ShareBtn(Texture tex = null) {
        if (tex == null) tex = _popupImage.texture;
        StartCoroutine(ShareRoutine(tex));
    }

    public void SaveToGallery() {
        StartCoroutine(SharePrompt.Instance.SaveRoutine(_popupImage.texture));
    }

    private IEnumerator ShareRoutine(Texture tex) {
        _waterMarkRenderer.SetActive(true);
        _waterMarkedImage.texture = tex;

        yield return null;

        Texture2D texRender = new Texture2D(_watermarkedTexture.width, _watermarkedTexture.height);
        RenderTexture.active = _watermarkedTexture;

        texRender.ReadPixels(new Rect(0, 0, _watermarkedTexture.width, _watermarkedTexture.height), 0, 0);
        texRender.Apply();

        _waterMarkRenderer.SetActive(false);
        SharePrompt.Instance.Share("CB", "Come color", texRender);
    }

    public void ContinueColoringBtn() {
        ImageListUpdater.Instance.ContinueBtn(_currentPopupImage.name);
    }

}

[System.Serializable]
public struct WorkedImage {
    public string name;
    public int rawImageIdInList;
    public byte day;
    public byte month;
    public int year;
    public byte hour;
    public byte minute;
    public byte second;

    public WorkedImage(bool isNull) {
        name = null;
        rawImageIdInList = -1;
        day = (byte)DateTime.Now.Day;
        month = (byte)DateTime.Now.Month;
        year = DateTime.Now.Year;
        hour = (byte)DateTime.Now.Hour;
        minute = (byte)DateTime.Now.Minute;
        second = (byte)DateTime.Now.Second;
    }
    public WorkedImage(string name) { // Save System Instances New
        this.name = name;
        rawImageIdInList = -1;
        day = (byte) DateTime.Now.Day;
        month = (byte)DateTime.Now.Month;
        year = DateTime.Now.Year;
        hour = (byte)DateTime.Now.Hour;
        minute = (byte)DateTime.Now.Minute;
        second = (byte)DateTime.Now.Second;
    }
    public WorkedImage(WorkedImage previousWorkedImg, int rawImageIdInList) { // My Work Updater Instances
        name = previousWorkedImg.name;
        this.rawImageIdInList = rawImageIdInList;
        day = (byte)DateTime.Now.Day;
        month = (byte)DateTime.Now.Month;
        year = DateTime.Now.Year;
        hour = (byte)DateTime.Now.Hour;
        minute = (byte)DateTime.Now.Minute;
        second = (byte)DateTime.Now.Second;
    }

    public DateTime GetDateTime() {
        return new DateTime(year, month, day, hour, minute, second);
    }

}