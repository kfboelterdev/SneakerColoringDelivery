using pl.ayground;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ColorMemory : MonoSingleton<ColorMemory> {

    private Stack<ColorAction> _undoStack = new(16);
    private Stack<ColorAction> _redoStack = new(8);

    [Header("UI Elements")]

    [SerializeField] private Button _undoBtn;
    [SerializeField] private Button _redoBtn;
    [SerializeField] private Button _saveBtn;

    [Header("Cache")]

    private int _lastSaveUndoAmount;

    private bool _gettingSave = false;
    private DrawableTextureContainer _drawableCache;
    public DrawableTextureContainer DrawableCache {
        get {
            if (!_gettingSave) {
                DrawableTextureContainer drawable = _drawableCache;
                _drawableCache = null;
                return drawable;
            }
            else return null;
        }
    }

    public bool ImageHasActions { get { return _lastSaveUndoAmount > 0; } }

#if UNITY_EDITOR
    [Header("Debug")]

    [SerializeField] private bool _debugLogs;
#endif


    public void SaveColorAction(Vector2Int actionPosition, Color32 actionColor, Color32 previousColor) {
        _undoStack.Push(new ColorAction(actionPosition, actionColor, previousColor));
        _undoBtn.interactable = true;
        _saveBtn.interactable = true;

        _redoStack.Clear();
        _redoBtn.interactable = false;

#if UNITY_EDITOR
        if (_debugLogs) Debug.Log("Color Action Saved");
#endif
    }

    // Button
    public void UndoAction() {
        ColorAction undoAction = _undoStack.Pop();
#if UNITY_EDITOR
        bool b = ColorImageHandler.Instance.TryPaintAtPosition(undoAction.ActionPosition, undoAction.PreviousColor);
        if (_debugLogs) {
            if (b) Debug.Log("Action Undone Succesfully");
            else Debug.LogError("Error Occurred When Trying to Undo Action");
        }
#else
        ColorImageHandler.Instance.TryPaintAtPosition(undoAction.ActionPosition, undoAction.PreviousColor);
#endif
        if (_undoStack.Count <= 0) _undoBtn.interactable = false;

        _redoStack.Push(undoAction);
        _redoBtn.interactable = true;
        _saveBtn.interactable = _undoStack.Count != _lastSaveUndoAmount;
    }

    // Button
    public void RedoAction() {
        ColorAction redoAction = _redoStack.Pop();
#if UNITY_EDITOR
        bool b = ColorImageHandler.Instance.TryPaintAtPosition(redoAction.ActionPosition, redoAction.ActionColor);
        if (_debugLogs) {
            if (b) Debug.Log("Action Redone Succesfully");
            else Debug.LogError("Error Occurred When Trying to Redo Action");
        }
#else
        ColorImageHandler.Instance.TryPaintAtPosition(redoAction.ActionPosition, redoAction.ActionColor);
#endif
        if (_redoStack.Count <= 0) _redoBtn.interactable = false;

        _undoStack.Push(redoAction);
        _undoBtn.interactable = true;
        _saveBtn.interactable = _undoStack.Count != _lastSaveUndoAmount;
    }

    public void SaveActions() {
        if (_undoStack.Count > 0) {
            SaveSystem.Instance.Save(ColorImageHandler.Instance.ImageName, _undoStack, ColorImageHandler.Instance.CurrentTex);
            _lastSaveUndoAmount = _undoStack.Count;
            _saveBtn.interactable = false;
        }
    }

    public DrawableTextureContainer GetSave(ImageCaracteristics caracteristics, Texture2D preDownloadedTexture = null) {
        _undoBtn.interactable = false;
        _redoBtn.interactable = false;
        _saveBtn.interactable = false;
        _undoStack = new(16);
        _redoStack = new(4);
        _lastSaveUndoAmount = 0;

        Texture2D tex = SaveSystem.Instance.ReadImageColored(caracteristics.name);
        if (tex != null) return new DrawableTextureContainer(tex, false, false);
        else {
            tex = caracteristics.name.Contains("PD_") ? Resources.Load<Texture2D>(caracteristics.name.Split('-')[0])
                                                      : SaveSystem.Instance.ReadImageBase(caracteristics.name.Split("-")[0]);
            return new DrawableTextureContainer(tex, true, false);
        }
    }

    public void ResetImageToSave() {
        ColorImageHandler.Instance.canInput = false;
    }

    private void OnApplicationQuit() {
        SaveActions();
    }

}

[System.Serializable]
public struct ColorAction {
    public int[] actionPosition;
    public byte[] actionColor;
    public byte[] previousColor;

    public ColorAction(Vector2Int actionPosition, Color32 actionColor, Color32 previousColor) {
        this.actionPosition = new int[2] { actionPosition.x, actionPosition.y };
        this.actionColor = new byte[4] { actionColor.r, actionColor.g, actionColor.b, actionColor.a };
        this.previousColor = new byte[4] { previousColor.r, previousColor.g, previousColor.b, previousColor.a };
    }

    public Vector2Int ActionPosition { get { return new Vector2Int(actionPosition[0], actionPosition[1]); } }
    public Color32 ActionColor { get { return new Color32(actionColor[0], actionColor[1], actionColor[2], actionColor[3]); } }
    public Color32 PreviousColor { get { return new Color32(previousColor[0], previousColor[1], previousColor[2], previousColor[3]); } }

}