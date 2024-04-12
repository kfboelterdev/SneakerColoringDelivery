using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class CategorySwitching : MonoSingleton<CategorySwitching> {

    [Header("UI Obj")]

    [SerializeField] private ScrollRect _horizontalScroll;
    private List<ScrollRect> _verticalScrolls = new List<ScrollRect>();
    private List<RectTransform> _verticalRectTransforms = new List<RectTransform>();

    [Header("Switching")]

    [SerializeField] private float _inputDirectionTreshold;
    [Range(0f, 1f)]
    [SerializeField] private float _switchTresholdPercent;
    private float _switchTreshold;

    [Header("Cache")]

    private byte _currentCategory;
    private bool _currentlySwitching;
    private bool _allowScroll = true;

    private Action _actionCache;
    private Vector2 _touchDeltaCache;

    protected override void Awake() {
        base.Awake();

        _switchTreshold = Screen.width * _switchTresholdPercent;
    }

    private void Update() {
        if (_allowScroll && Input.touchCount > 0) {
            _actionCache = Input.GetTouch(0).phase switch {
                TouchPhase.Began => () => _currentlySwitching = false,
                TouchPhase.Moved => DecideDirection,
                TouchPhase.Ended => TrySwitching,
                _ => () => { }

            };
            _actionCache();
        }
#if UNITY_EDITOR
        if (_allowScroll) {
            if (Input.GetMouseButtonDown(0)) {
                _currentlySwitching = false;
                _touchDeltaCache = Vector2.zero;
            }
            else if (Input.GetMouseButton(0)) {
                // Decide Direction
                if (!_currentlySwitching) {
                    if (_touchDeltaCache == (Vector2)Input.mousePosition) return;
                    if (_touchDeltaCache == Vector2.zero) {
                        _touchDeltaCache = Input.mousePosition;
                        return;
                    }
                    Vector2 mouseDelta = (Vector2)Input.mousePosition - _touchDeltaCache;
                    if (mouseDelta.magnitude >= _inputDirectionTreshold) {
                        _horizontalScroll.horizontal = Mathf.Abs(mouseDelta.x) > Mathf.Abs(mouseDelta.y);
                        if (_horizontalScroll.horizontal) _horizontalScroll.movementType = ScrollRect.MovementType.Unrestricted;
                        foreach (ScrollRect vScroll in _verticalScrolls) vScroll.vertical = !_horizontalScroll.horizontal;

                        _currentlySwitching = true;
                    }
                }
                // Apply horizontal Movement
                else if (_horizontalScroll.horizontal) {
                    Vector2 mouseDelta = (Vector2)Input.mousePosition - _touchDeltaCache;
                    _horizontalScroll.content.anchoredPosition = Vector2.Scale(mouseDelta, Vector2.right);
                }
            }
            else if (Input.GetMouseButtonUp(0)) {
                if (_currentlySwitching) {
                    _horizontalScroll.movementType = ScrollRect.MovementType.Elastic;
                    _horizontalScroll.horizontal = true;
                    if (_horizontalScroll.horizontal && Mathf.Abs(_horizontalScroll.content.anchoredPosition.x) > _switchTreshold) {
                        if (Mathf.Sign(_horizontalScroll.content.anchoredPosition.x) < 0) {
                            if (_currentCategory < _verticalScrolls.Count - 1) SwitchCategory(1);
                        }
                        else if (_currentCategory > 0) SwitchCategory(-1);
                    }
                }
            }
        }
#endif
    }

    private void DecideDirection() {
        _touchDeltaCache = Input.GetTouch(0).deltaPosition;
        if (!_currentlySwitching) {
            if (_touchDeltaCache.magnitude >= _inputDirectionTreshold) {
                _horizontalScroll.horizontal = Mathf.Abs(_touchDeltaCache.x) > Mathf.Abs(_touchDeltaCache.y);
                if (_horizontalScroll.horizontal) _horizontalScroll.movementType = ScrollRect.MovementType.Unrestricted;
                foreach (ScrollRect vScroll in _verticalScrolls) vScroll.vertical = !_horizontalScroll.horizontal;

                _currentlySwitching = true;
            }
        }
        else if (_horizontalScroll.horizontal) {
            _touchDeltaCache[1] = 0f;
            _horizontalScroll.content.anchoredPosition += _touchDeltaCache;
        }
    }

    private void TrySwitching() {
        if (_currentlySwitching) {
            _horizontalScroll.movementType = ScrollRect.MovementType.Elastic;
            _horizontalScroll.horizontal = true;
            if (_horizontalScroll.horizontal && Mathf.Abs(_horizontalScroll.content.anchoredPosition.x) > _switchTreshold) {
                if (Mathf.Sign(_horizontalScroll.content.anchoredPosition.x) < 0) {
                    if (_currentCategory < _verticalScrolls.Count - 1) SwitchCategory(1);
                }
                else if (_currentCategory > 0) SwitchCategory(-1);
            }
        }
    }

    private void SwitchCategory(int direction) {
        _currentCategory += (byte)direction;
        ImageListUpdater.Instance.UpdateCategory(_currentCategory, _verticalScrolls[_currentCategory].content);

        direction *= (int)_horizontalScroll.content.sizeDelta.x;
        foreach (RectTransform vRectTransform in _verticalRectTransforms) vRectTransform.anchoredPosition -= direction * Vector2.right;
        _horizontalScroll.content.anchoredPosition += direction * Vector2.right;

        LayoutRebuilder.ForceRebuildLayoutImmediate(_horizontalScroll.content); //
    }

    public void SwitchToCategory(byte category) {
        if (category != _currentCategory) SwitchCategory(category - _currentCategory);
    }

    public void SetVerticalScrolls(ScrollRect[] scrollRects) {
        if (scrollRects.Length > _verticalScrolls.Count) {
            for (int i = _verticalScrolls.Count; i < scrollRects.Length; i++) {
                //if (scrollRects[i] != null) {
                _verticalScrolls.Add(scrollRects[i]);
                _verticalRectTransforms.Add(_verticalScrolls[i].transform.GetComponent<RectTransform>());
                _verticalRectTransforms[i].anchoredPosition = new Vector2(i * _horizontalScroll.content.sizeDelta.x, _verticalRectTransforms[i].anchoredPosition.y);
                _verticalRectTransforms[i].sizeDelta = new Vector2(_horizontalScroll.content.sizeDelta.x, _verticalRectTransforms[i].sizeDelta.y);
                RescaleVerticalScroll((byte)i);
            }
        }

    }

    public void RescaleVerticalScroll(byte scrollId) {
        GridLayoutGroup vContentGridLGroup = _verticalScrolls[scrollId].content.GetComponent<GridLayoutGroup>();
        float vContentRows = Mathf.Ceil(_verticalScrolls[scrollId].content.childCount / 2f);
        _verticalScrolls[scrollId].content.sizeDelta = new Vector2(_verticalScrolls[scrollId].content.sizeDelta.x, ((vContentRows - 1) * vContentGridLGroup.spacing.y)
                                                                                                                  + (vContentRows * vContentGridLGroup.cellSize.y)
                                                                                                                  + vContentGridLGroup.padding.top
                                                                                                                  + vContentGridLGroup.padding.bottom);
        if (scrollId != 0) RescaleVerticalScroll(0);
    }

    public void AllowScroll(bool canScroll) {
        _allowScroll = canScroll;
    }

}
