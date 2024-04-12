using pl.ayground;
using UnityEngine;
using UnityEngine.UI;

public class DTT_Debugging : MonoBehaviour {

    private DrawableTextureContainer _dtt;
    [SerializeField] private Texture2D _tex;

    private RawImage _rawImg;

    [SerializeField] private Color32 _color;

    private Camera _cam;

    private void Awake() {
        _dtt = new DrawableTextureContainer(_tex, false, false);
        _rawImg = GetComponentInChildren<RawImage>();
        _rawImg.texture = _dtt.getTexture();
    }

    private void Update() {
        if (Input.GetMouseButton(0)) {
            LocalizeInput(Input.mousePosition, out Vector2Int localizedInput);
            TryPaintAtPosition(localizedInput, _color);
        }
    }

    private void LocalizeInput(Vector2 rawInput, out Vector2Int localizedInput) {
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(_rawImg.rectTransform, rawInput, _cam, out Vector2 _localizedInputCache)) {
            //_localizedInputCache *= 1;
            _localizedInputCache.x += _dtt.getWidth() / 2;
            _localizedInputCache.y += _dtt.getHeight() / 2;

            localizedInput = new Vector2Int((int)_localizedInputCache.x, (int)_localizedInputCache.y);
        }
        else localizedInput = -Vector2Int.one;
    }

    public bool TryPaintAtPosition(Vector2Int position, Color32 color) {
        if (!_dtt.PaintBucketTool(position.x, position.y, color)) return false;
        _rawImg.texture = _dtt.getTexture();
        return true;
    }

}
