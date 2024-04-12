using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SelectionMenu : MonoSingleton<SelectionMenu> {

    [SerializeField] private GameObject _currentObject;

    [SerializeField] private RectTransform _sectionPicker;
    [SerializeField] private Vector2Int _tabPickerPositionOffset;
    [SerializeField] private Vector2Int _tabPickerScaleOffset;

    [SerializeField] private Image _homeImage;
    [SerializeField] private Sprite[] _homeTabSprites;
    [SerializeField] private TextMeshProUGUI _homeText;
    [SerializeField] private Image _workImage;
    [SerializeField] private Sprite[] _workTabSprites;
    [SerializeField] private TextMeshProUGUI _workText;
    [SerializeField] private Color[] _textColorSelected;

    private Camera _cam;
    private Color _camColorSelectionMenu;

    protected override void Awake() {
        base.Awake();

        _cam = Camera.main;
        _camColorSelectionMenu = _cam.backgroundColor;
    }

    public void ActivateObject(CanvasGroup cGroup) {
        cGroup.alpha = 1.0f;
        cGroup.interactable = true;
        cGroup.blocksRaycasts = true;
    }

    public void DeactivateObject(CanvasGroup cGroup) {
        cGroup.alpha = 0.0f;
        cGroup.interactable = false;
        cGroup.blocksRaycasts = false;
    }

    public void ChangeCurrentObject(GameObject newCurrent) {
        _currentObject = newCurrent;
    }

    public void ChangeFocusObject(GameObject newFocus) {
        if (newFocus != _currentObject) {
            newFocus.SetActive(true);
            _currentObject.SetActive(false);
            _currentObject = newFocus;
        }
    }

    public void HomeBtnFocus() {
        _homeImage.sprite = _homeTabSprites[0];
        _homeText.color = _textColorSelected[0];
        _workImage.sprite = _workTabSprites[1];
        _workText.color = _textColorSelected[1];
    }

    public void WorkBtnFocus() {
        _homeImage.sprite = _homeTabSprites[1];
        _homeText.color = _textColorSelected[1];
        _workImage.sprite = _workTabSprites[0];
        _workText.color = _textColorSelected[0];
    }

    public void UpdateTabPickerImage(RectTransform rectTransformToOverlay) {
        _sectionPicker.anchoredPosition = rectTransformToOverlay.anchoredPosition + _tabPickerPositionOffset;
        _sectionPicker.sizeDelta = rectTransformToOverlay.sizeDelta + _tabPickerScaleOffset;
    }

    public void ChangeBackgroundColor(bool isMenu) {
        _cam.backgroundColor = isMenu ? _camColorSelectionMenu : Color.white;
    }

}
