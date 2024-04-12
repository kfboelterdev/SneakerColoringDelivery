using System.Collections;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SharePrompt : MonoSingleton<SharePrompt> {

    [Header("Menus")]

    [SerializeField] private CanvasGroup _homeCGroup;
    [SerializeField] private CanvasGroup _homeUpperCGroup;
    [SerializeField] private CanvasGroup _myworkCGroup;
    [SerializeField] private CanvasGroup _myworkUpperCGroup;
    [SerializeField] private GameObject _sharePrompt;
    [SerializeField] private RawImage _sharePromptImage;

    [Header("Watermark")]

    [SerializeField] private GameObject _waterMarkRenderer;
    [SerializeField] private RenderTexture _watermarkedTexture;
    [SerializeField] private RawImage _waterMarkedImage;

    [Header("Action Popup")]

    [SerializeField] private CanvasGroup _actionPopup;
    private TextMeshProUGUI _actionPopupText;
    [SerializeField] private float _actionPopupDuration;
    private Coroutine _currentPopupCoroutine;

    [Header("Clipboard")]

    [SerializeField] private string _hashtagToCopy;

    protected override void Awake() {
        base.Awake();

        _actionPopupText = _actionPopup.GetComponentInChildren<TextMeshProUGUI>();
        //NativeGallery.Permission permission = await
    }

    public void Share(string subject, string text, Texture2D imageToShare) {
        NativeShare nativeShare = new NativeShare()
                                     .SetTitle("Sneaker Coloring") // Android
                                     .SetSubject(subject) // Mainly e-mail
                                                          //.SetText(text) // Message ?
                                     .AddFile(imageToShare); // Image

        nativeShare.Share();
    }

    public void CopyHastag() {
        GUIUtility.systemCopyBuffer = _hashtagToCopy; // Gotta test
        StartCoroutine(ActionPopup("Copied"));
    }

    public void CheckToOpenSharePrompt() {
        if (ColorMemory.Instance.ImageHasActions) {
            _sharePromptImage.texture = ColorImageHandler.Instance.CurrentTex;
            _sharePrompt.SetActive(true);
            SelectionMenu.Instance.DeactivateObject(_homeCGroup);
            SelectionMenu.Instance.DeactivateObject(_homeUpperCGroup);
            SelectionMenu.Instance.DeactivateObject(_myworkCGroup);
            SelectionMenu.Instance.DeactivateObject(_myworkUpperCGroup);
        }
    }

    public void ShareBtn() {
        MyWorkUpdater.Instance.ShareBtn(_sharePromptImage.texture);
    }

    public void SaveToGallery() {
        StartCoroutine(SaveRoutine(_sharePromptImage.texture));
    }

    public IEnumerator SaveRoutine(Texture tex) {
        if (_currentPopupCoroutine != null) StopCoroutine(_currentPopupCoroutine);
        _currentPopupCoroutine = StartCoroutine(ActionPopup("Saving..."));

        _waterMarkRenderer.SetActive(true);
        _waterMarkedImage.texture = tex;

        yield return null;

        Texture2D texRender = new Texture2D(_watermarkedTexture.width, _watermarkedTexture.height);
        RenderTexture.active = _watermarkedTexture;

        texRender.ReadPixels(new Rect(0, 0, _watermarkedTexture.width, _watermarkedTexture.height), 0, 0);
        texRender.Apply();

        yield return null;

        File.WriteAllBytes($"{Application.persistentDataPath}/temporary.png", texRender.EncodeToPNG());
        NativeGallery.Permission permission = NativeGallery.SaveImageToGallery($"{Application.persistentDataPath}/temporary.png", "SneakerColoring", $"{ColorImageHandler.Instance.ImageName}.png", (success, path) => Debug.Log("Media save result: " + success + " " + path));
        //NativeGallery.Permission permission = NativeGallery.SaveImageToGallery(texRender.EncodeToPNG(), "SneakerColoring", $"{ColorImageHandler.Instance.ImageName}.png", (success, path) => Debug.Log("Media save result: " + success + " " + path));
        if(_currentPopupCoroutine != null) StopCoroutine(_currentPopupCoroutine);
        _currentPopupCoroutine = StartCoroutine(ActionPopup(permission == NativeGallery.Permission.Granted ? "Saved" : $"Error: {permission}"));

        _waterMarkRenderer.SetActive(false);
    }

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
