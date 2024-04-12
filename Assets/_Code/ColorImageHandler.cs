using pl.ayground;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class ColorImageHandler : MonoSingleton<ColorImageHandler> {

    public bool canInput = false;
    [HideInInspector] public UnityEvent onLoadImage = new UnityEvent();

    [Header("Colored Image")]

    //public Texture2D imageBase;
    [SerializeField] private RawImage _uiImage;
    private string _uiImageName;
    private RectTransform _uiImageRectTransform;
    private DrawableTextureContainer _drawableTextureContainer;
    [Tooltip("Area to be ignored from bottom to top")]
    [Range(0f, 0.5f)]
    [SerializeField] private float _paintInputIgnoreAreaBellow; // Could be instead calculated privately based on UI Scale
    [Tooltip("Area to be ignored from top to bottom")]
    [Range(0f, 0.5f)]
    [SerializeField] private float _paintInputIgnoreAreaAbove; // Ditto

    [Space]

    [SerializeField] private Camera _camera;

    [Header("Pinch Zoom")]

    [SerializeField] private float _zoomMin;
    [SerializeField] private float _zoomMax;
    [SerializeField] private RectTransform _uiImageContainer;

    private float _pinchDistance;
    private float _pinchInitialDistance;
    private float _pinchImageInitialScale;

    [Range(0f, 0.99f)]
    [SerializeField] private float _smoothFollowSpeed;
    [SerializeField] //
    private RectTransform _mimicImageRectTransform;
    private Vector2 _inputDisplacementInitialPosition;
    private Vector2 _imageDisplacementInitialPosition;

    private float _proportionToBase; // How many times relative to 2048

    [Header("Bucket Animation")]

    [SerializeField] private RawImage _newStateImage;
    [SerializeField] private RectTransform _bucketAnimMask;
    [SerializeField] private float _bucketAnimDuration;
    private Coroutine _bucketAnimationRoutine;

    [Header("Cache")]

    private Vector2 _localizedInputCache;
    private Vector2Int _localizedInputIntCache;

    private Vector2 _pinchScaleCache;
    private Vector2 _pinchLocalizedNormalizedCache;

    private Vector2 _imagePositionBeforeClampCache;
    private Vector2 _reverseClampCache;

    [Header("Debug")]

    [SerializeField] private TextMeshProUGUI _debugTMP;
    [SerializeField] private TextMeshProUGUI _debugTMP2;


    // Access

    public string ImageName { get { return _uiImageName; } }
    public Texture2D CurrentTex { get { return _drawableTextureContainer.getTexture(); } }

#if UNITY_EDITOR
    [Header("Debug")]

    [SerializeField] private bool _debugLogs;
#endif

    private void Start() {
        _uiImageRectTransform = _uiImage.rectTransform;
        InitiateMimicRect();
#if UNITY_EDITOR
        _pinchDistance = _uiImageRectTransform.localScale.x;
#endif
    }

    private void Update() {
        if (canInput) {
            LocalizeInput(Input.mousePosition, out Vector2Int inputInRImage);
#if UNITY_EDITOR
            PCInput();
#endif
            MobileInput();
            ReverseClampImageBorders();
            SmoothPinchFollow();
        }
    }

    private void InitiateMimicRect() {
        GameObject mimicRect = new GameObject("MimicRectTransform");
        mimicRect.transform.parent = _uiImageRectTransform.parent;
        mimicRect.AddComponent<RectTransform>();
        _mimicImageRectTransform = mimicRect.GetComponent<RectTransform>();
        _mimicImageRectTransform.anchoredPosition = _uiImageRectTransform.anchoredPosition;
        _mimicImageRectTransform.pivot = _uiImageRectTransform.pivot;
        _mimicImageRectTransform.sizeDelta = _uiImageRectTransform.sizeDelta;
        _mimicImageRectTransform.localScale = _uiImageRectTransform.localScale;
    }

#if UNITY_EDITOR
    private void PCInput() {
        if (Input.GetMouseButtonUp(0) && Input.mousePosition.y > _paintInputIgnoreAreaBellow * Screen.height && Input.mousePosition.y < (1 - _paintInputIgnoreAreaAbove) * Screen.height) {
            LocalizeInput(Input.mousePosition, out _localizedInputIntCache);
            if (_drawableTextureContainer.IsInImageBounds(_localizedInputIntCache.x, _localizedInputIntCache.y)) {
                Color32 previousColor = _drawableTextureContainer.getPixelColor(_localizedInputIntCache.x, _localizedInputIntCache.y);
                if (TryPaintAtPosition(_localizedInputIntCache, ColorPickerUI.Instance.SelectedColor)) {
                    ColorMemory.Instance.SaveColorAction(_localizedInputIntCache, ColorPickerUI.Instance.SelectedColor, previousColor);
                    if (_debugLogs) Debug.Log($"Painted using {ColorPickerUI.Instance.SelectedColor} in ({(int)Input.mousePosition.x},{(int)Input.mousePosition.y})");
                }
                else if (_debugLogs) Debug.LogWarning($"Couldn't paint in ({(int)Input.mousePosition.x},{(int)Input.mousePosition.y})");
            }
            else if (_debugLogs) Debug.Log("Input not inside Image");
        }
        if (Input.GetMouseButtonDown(1)) {
            CalculateZoomPivot();
            CalculateInitialDisplacement();
        }
        else if (Input.GetMouseButton(1)) {
            // Zoom
            if (Input.mouseScrollDelta.y != 0) {

                if (_debugLogs) Debug.Log($"Size went from {_mimicImageRectTransform.localScale} to {_pinchImageInitialScale * Mathf.Clamp(_pinchDistance + Mathf.Sign(Input.mouseScrollDelta.y) * 0.1f, _zoomMin, _zoomMax)}");
                _pinchDistance = Mathf.Clamp(_pinchDistance + Mathf.Sign(Input.mouseScrollDelta.y) * 0.025f, _zoomMin, _zoomMax);
                //Debug.Log($"{_pinchImageInitialScale * _pinchDistance} = {_pinchImageInitialScale} * {_pinchDistance}");
                _pinchScaleCache[0] = _pinchDistance;
                _pinchScaleCache[1] = _pinchScaleCache[0];
                _mimicImageRectTransform.localScale = _pinchScaleCache;
                if (_debugLogs) Debug.Log($"Pivot set to {_mimicImageRectTransform.pivot}");
            }

            // Displacement
            _mimicImageRectTransform.anchoredPosition = _imageDisplacementInitialPosition + ((Vector2)Input.mousePosition - _inputDisplacementInitialPosition);
        }
        else {
            SetRectPivot(_mimicImageRectTransform, Vector2.one / 2); // Needed so Bucket works properly
        }
    }
#endif

    private void MobileInput() {
        if (Input.touchCount > 1) {
            _pinchDistance = Vector2.Distance(Input.touches[0].position, Input.touches[1].position);
            if (Input.touches[0].phase == TouchPhase.Began || Input.touches[1].phase == TouchPhase.Began) {
                // Zoom
                _pinchInitialDistance = _pinchDistance;
                _pinchImageInitialScale = _mimicImageRectTransform.localScale.x;

                CalculateZoomPivot();

                CalculateInitialDisplacement();
            }
            else {
                // Zoom
                _pinchScaleCache[0] = Mathf.Clamp(_pinchImageInitialScale * _pinchDistance / _pinchInitialDistance, _zoomMin, _zoomMax);
                _pinchScaleCache[1] = _pinchScaleCache[0];
                _mimicImageRectTransform.localScale = _pinchScaleCache;

                // Displacement
                _mimicImageRectTransform.anchoredPosition = _imageDisplacementInitialPosition + (Vector2.Lerp(Input.touches[0].position, Input.touches[1].position, 0.5f) - _inputDisplacementInitialPosition);
            }
        }
        // Coloring
        else if (Input.touchCount > 0 && Input.touches[0].phase == TouchPhase.Ended) {
            if (_pinchInitialDistance == 0) {
                if (Input.touches[0].position.y > _paintInputIgnoreAreaBellow * Screen.height && Input.touches[0].position.y < (1 - _paintInputIgnoreAreaAbove) * Screen.height) {
                    LocalizeInput(Input.touches[0].position, out _localizedInputIntCache);
                    if (_drawableTextureContainer.IsInImageBounds(_localizedInputIntCache.x, _localizedInputIntCache.y)) {
                        Color32 previousColor = _drawableTextureContainer.getPixelColor(_localizedInputIntCache.x, _localizedInputIntCache.y);
                        if (TryPaintAtPosition(_localizedInputIntCache, ColorPickerUI.Instance.SelectedColor))
                            ColorMemory.Instance.SaveColorAction(_localizedInputIntCache, ColorPickerUI.Instance.SelectedColor, previousColor);
                    }
                }
            }
            else {
                _pinchInitialDistance = 0;
                SetRectPivot(_mimicImageRectTransform, Vector2.one / 2); // Needed so Bucket works properly
            }
        }
    }

    private void LocalizeInput(Vector2 rawInput, out Vector2Int localizedInput) {
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(_uiImageRectTransform, rawInput, _camera, out _localizedInputCache)) {
            //localizedInput /= 1.0f; // Needed in some distortion scenarios
            _localizedInputCache *= _proportionToBase;
            _localizedInputCache.x += _drawableTextureContainer.getWidth() / 2;
            _localizedInputCache.y += _drawableTextureContainer.getHeight() / 2;

            localizedInput = new Vector2Int((int)_localizedInputCache.x, (int)_localizedInputCache.y);
        }
        else localizedInput = -Vector2Int.one;
    }

    public bool TryPaintAtPosition(Vector2Int position, Color32 color) {
        if (!_drawableTextureContainer.PaintBucketTool(position.x, position.y, color)) return false;
        _uiImage.texture = _drawableTextureContainer.getTexture();

        //byte[] drawablePrevBytes = _drawableTextureContainer.getTexture().EncodeToPNG();
        //if (_bucketAnimationRoutine != null) StopCoroutine(_bucketAnimationRoutine);
        //_bucketAnimationRoutine = StartCoroutine(PaintBucketAnimation(drawablePrevBytes, position));

        return true;
    }

    //private IEnumerator PaintBucketAnimation(byte[] drawablePrevBytes, Vector2 localizedInput) {
    //    Texture2D texPrev = new Texture2D(0, 0);
    //    texPrev.LoadImage(drawablePrevBytes);
    //    _uiImage.texture = texPrev;
    //    _newStateImage.texture = _drawableTextureContainer.getTexture();

    //    Debug.Log(localizedInput / _proportionToBase);
    //    _bucketAnimMask.anchoredPosition = (localizedInput / _proportionToBase) - (Vector2.one * 1024);

    //    float duration = 0;
    //    while (duration < _bucketAnimDuration) {
    //        duration += Time.deltaTime;
    //        if (duration > _bucketAnimDuration) duration = _bucketAnimDuration;

    //        _bucketAnimMask.localScale = Vector2.one * (2 * Mathf.Sqrt(2) * duration / _bucketAnimDuration);
    //        _newStateImage.rectTransform.localScale = Vector2.one * (1f / _bucketAnimMask.localScale.x);
    //        _newStateImage.rectTransform.anchoredPosition = -(_bucketAnimMask.anchoredPosition / _bucketAnimMask.localScale);

    //        yield return null;
    //    }
    //    _bucketAnimationRoutine = null;
    //}

    private void CalculateZoomPivot() {
#if UNITY_EDITOR
        RectTransformUtility.ScreenPointToLocalPointInRectangle(_mimicImageRectTransform, Input.mousePosition, _camera, out _pinchLocalizedNormalizedCache);
        _pinchLocalizedNormalizedCache /= _mimicImageRectTransform.rect.size;
        _pinchLocalizedNormalizedCache += _mimicImageRectTransform.pivot;
        SetRectPivot(_mimicImageRectTransform, _pinchLocalizedNormalizedCache);
        if (_debugLogs) Debug.Log($"Pivot set to {_mimicImageRectTransform.pivot}");
#else
        RectTransformUtility.ScreenPointToLocalPointInRectangle(_mimicImageRectTransform, Vector2.Lerp(Input.touches[0].position, Input.touches[1].position, 0.5f), _camera, out _pinchLocalizedNormalizedCache);
        _pinchLocalizedNormalizedCache /= _mimicImageRectTransform.rect.size;
        _pinchLocalizedNormalizedCache += _mimicImageRectTransform.pivot;
        SetRectPivot(_mimicImageRectTransform, _pinchLocalizedNormalizedCache);
#endif
    }

    private void CalculateInitialDisplacement() {
#if UNITY_EDITOR
        _inputDisplacementInitialPosition = Input.mousePosition;
        _imageDisplacementInitialPosition = _mimicImageRectTransform.anchoredPosition;
# else
        _inputDisplacementInitialPosition = Vector2.Lerp(Input.touches[0].position, Input.touches[1].position, 0.5f);
        _imageDisplacementInitialPosition = _mimicImageRectTransform.anchoredPosition;
#endif
    }

    private void ReverseClampImageBorders() {
        _imagePositionBeforeClampCache = _mimicImageRectTransform.anchoredPosition;
        _reverseClampCache.x = Mathf.Clamp(_mimicImageRectTransform.anchoredPosition.x,
                                           (_uiImageContainer.rect.size.x * _uiImageContainer.localScale.x * (1 - _uiImageContainer.pivot.x)) - (_mimicImageRectTransform.rect.size.x * _mimicImageRectTransform.localScale.x * (1 - _mimicImageRectTransform.pivot.x)),
                                           -(_uiImageContainer.rect.size.x * _uiImageContainer.localScale.x * _uiImageContainer.pivot.x) + (_mimicImageRectTransform.rect.size.x * _mimicImageRectTransform.localScale.x * _mimicImageRectTransform.pivot.x));
        _reverseClampCache.y = Mathf.Clamp(_mimicImageRectTransform.anchoredPosition.y,
                                           (_uiImageContainer.rect.size.y * _uiImageContainer.localScale.y * (1 - _uiImageContainer.pivot.y)) - (_mimicImageRectTransform.rect.size.y * _mimicImageRectTransform.localScale.y * (1 - _mimicImageRectTransform.pivot.y)),
                                           -(_uiImageContainer.rect.size.y * _uiImageContainer.localScale.y * _uiImageContainer.pivot.y) + (_mimicImageRectTransform.rect.size.y * _mimicImageRectTransform.localScale.y * _mimicImageRectTransform.pivot.y));

        if (_imagePositionBeforeClampCache != _reverseClampCache) {
            _mimicImageRectTransform.anchoredPosition = _reverseClampCache;
            CalculateZoomPivot();
            CalculateInitialDisplacement();
        }
    }

    private void SmoothPinchFollow() {
        _uiImageRectTransform.anchoredPosition = Vector2.Lerp(_uiImageRectTransform.anchoredPosition, _mimicImageRectTransform.anchoredPosition, _smoothFollowSpeed);
        _uiImageRectTransform.pivot = Vector2.Lerp(_uiImageRectTransform.pivot, _mimicImageRectTransform.pivot, _smoothFollowSpeed);
        _uiImageRectTransform.localScale = Vector2.Lerp(_uiImageRectTransform.localScale, _mimicImageRectTransform.localScale, _smoothFollowSpeed);
    }

    // Maybe move to other script
    private void SetRectPivot(RectTransform rectTransform, Vector2 newPivot) {
        Vector2 correctionDisplacement = rectTransform.rect.size * (rectTransform.pivot - newPivot) * rectTransform.localScale.x;
        rectTransform.pivot = newPivot;
        rectTransform.localPosition -= (Vector3)correctionDisplacement;
    }

    public void SetNewImage(ImageCaracteristics caracteristics) {
        _uiImageName = caracteristics.name;
        SelectionMenu.Instance.ChangeBackgroundColor(false);
        _drawableTextureContainer = ColorMemory.Instance.GetSave(caracteristics, caracteristics.name.Substring(0, 3) == "PD_" ?
                                                                         Resources.Load<Texture2D>(caracteristics.name.Split('-')[0]) :
                                                                         SaveSystem.Instance.ReadImageBase(caracteristics.name.Split('-')[0]));
        _uiImage.texture = _drawableTextureContainer.getTexture();
        _proportionToBase = _drawableTextureContainer.getTexture().width / 2048f; // Considers only square images

        //Texture2D texCurrent = _uiImage.texture as Texture2D;
        //Texture2D texPrev = new Texture2D(_uiImage.texture.width, _uiImage.texture.height, texCurrent.format, 0, true);
        _newStateImage.texture = _drawableTextureContainer.getTexture();

        StartCoroutine(AllowInput());
    }

    private IEnumerator AllowInput() {
        yield return null;

        canInput = true;
    }
}
