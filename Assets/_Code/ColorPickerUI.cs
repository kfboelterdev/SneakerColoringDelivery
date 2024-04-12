using UnityEngine;
using UnityEngine.UI;

public class ColorPickerUI : MonoSingleton<ColorPickerUI> {

    [Header("UI Elements")]

    [SerializeField] private RectTransform _macroColorPicker;
    private Image _macroColorPickerColor;
    [SerializeField] private RectTransform[] _macroColorPositions = new RectTransform[14];

    [SerializeField] private Image[] _microColorImages = new Image[5];
    [SerializeField] private Sprite[] _microColorSprites = new Sprite[2];

    [Header("Color Picking")]

    [SerializeField] private MacroColor[] _selectableColors = new MacroColor[14];

    [Header("Cache")]

    private byte[] _selectedColor = new byte[2];

    // Access

    public Color32 SelectedColor { get { return _selectableColors[_selectedColor[0]].microColors[_selectedColor[1]]; } }

    private void Start() {
        _macroColorPickerColor = _macroColorPicker.GetComponent<Image>();

        PickMacroColor(10);
        PickMicroColor(2);
    }

    public void PickMacroColor(int hue) {
        _selectedColor[0] = (byte)hue;

        // Update Macro Color Picker
        _macroColorPicker.position = _macroColorPositions[_selectedColor[0]].position;
        _macroColorPicker.rotation = _macroColorPositions[_selectedColor[0]].rotation;
        Color.RGBToHSV(_selectableColors[_selectedColor[0]].microColors[2], out float selectionHue, out _, out float _);
        _macroColorPickerColor.color = selectionHue != 0 ? _selectableColors[_selectedColor[0]].microColors[2] // Shows Middle Color
                                                         : _selectableColors[_selectedColor[0]].microColors[0];

        // Update Micro Colors displayed
        for (int i = 0; i < _microColorImages.Length; i++) _microColorImages[i].color = _selectableColors[_selectedColor[0]].microColors[i];
    }

    public void PickMicroColor(int brightness) {
        _selectedColor[1] = (byte)brightness;

        // Update Micro Color Images
        for (int i = 0; i < _microColorImages.Length; i++) _microColorImages[i].sprite = _selectedColor[1] == i ? _microColorSprites[1] : _microColorSprites[0];
    }

}

[System.Serializable]
public struct MacroColor {
    public Color32[] microColors;

    public MacroColor(int i) {
        microColors = new Color32[5];
    }
}