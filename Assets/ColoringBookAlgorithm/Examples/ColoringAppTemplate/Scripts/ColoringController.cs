using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using pl.ayground;

public class ColoringController : MonoBehaviour {

	public enum AppState {
		COLORING,
		PAGE_PICKER_ENABLED
	}


	public List<Texture2D> ColoringPages;
	public List<Color32> ColorPalette;

	public RawImage Image;
	public Image ColoringPageContainer;
	public Transform CrayonsContainerContent;
	public Transform ColoringPagesPickerContainerContent;
	public ColorSelectionButton ColorButtonPrefab;
	public SelectColoringPageButton ColoringPageButonPrefab;
	public Camera MyCamera;
	public Image ColoringPagesPicker;

	private DrawableTextureContainer imageContainer;
	private float imageScale = 1f;
	private Color32 selectedColor;

	// Adding Coloring Pages on the fly 
	private bool AllPagesAdded = false;
	private int currentPageId = 0;

	private AppState state = AppState.COLORING;
	// Use this for initialization
	void Start () {
		InitColorPicker ();
		InitWithRandomColoringPage ();
		Input.simulateMouseWithTouches = true;
	}

	public void InitColorPicker(){
		selectedColor = ColorPalette [0];
		foreach (Color32 c in ColorPalette) {
			ColorSelectionButton obj = Instantiate (ColorButtonPrefab, CrayonsContainerContent);
			obj.Init (this, c);
		}
		CrayonsContainerContent.GetComponent<RectTransform> ().sizeDelta = new Vector2 (100, ColorPalette.Count * 110);
	}

	public void SetColor(Color32 _color){
		//Debug.Log ("Color changed to: " + _color);
		selectedColor = _color;
	}

	public void InitWithRandomColoringPage(){
		InitWithColoringPage(ColoringPages[Random.Range(0,ColoringPages.Count)]);
	}

	public void InitWithColoringPage(Texture2D texture){
		imageContainer = new DrawableTextureContainer (texture, true, false);
		Image.texture = imageContainer.getTexture ();
		// Lets fit the image into the frame
		float imageWidth = Image.texture.width;
		float imageHeight = Image.texture.height;
		AspectRatioFitter fitter = Image.GetComponent<AspectRatioFitter> ();
		fitter.aspectRatio = imageWidth / imageHeight;
		// And calculate what is the final scale used to fit it there
		float initialImageScaleX = ColoringPageContainer.GetComponent<RectTransform>().rect.size.x / imageWidth; 
		float initialImageScaleY = ColoringPageContainer.GetComponent<RectTransform>().rect.size.y / imageHeight; 
		if (initialImageScaleX < 1 && initialImageScaleY < 1) {
			imageScale = Mathf.Min (initialImageScaleY, initialImageScaleX);
		} else if (initialImageScaleX < 1) {
			imageScale = initialImageScaleX;
		} else if (initialImageScaleY < 1) {
			imageScale = initialImageScaleY;
		}
		else {
			imageScale = Mathf.Min(initialImageScaleY, initialImageScaleX);
		}
		
	}
	// Update is called once per frame
	void Update () {
		if (state == AppState.COLORING) {
			if (Input.GetMouseButtonDown (0)) {
				Click (Input.mousePosition);
			}
		}
	}

	void Click (Vector3 position){
		//Debug.Log ("On Screen Click: " + position);
		Vector2 localCursor;
		if (!RectTransformUtility.ScreenPointToLocalPointInRectangle (Image.GetComponent<RectTransform> (), position, MyCamera, out localCursor))
			return;
		else {
			//Debug.Log ("Click:" + localCursor);
			localCursor /= imageScale;
			//Debug.Log ("    with Scale Adjusted: " + localCursor);
			localCursor.x += imageContainer.getWidth () / 2;
			localCursor.y += imageContainer.getHeight () / 2;
			//Debug.Log ("    with Dimensions adjusted:" + localCursor);
			imageContainer.PaintBucketTool ((int)localCursor.x, (int)localCursor.y, selectedColor);
			Image.texture = imageContainer.getTexture ();
		}
	}

	public void OpenPagesBrowser(){
		if (state == AppState.COLORING) {
			state = AppState.PAGE_PICKER_ENABLED;
			ColoringPagesPicker.gameObject.SetActive (true);
			StartCoroutine (AddColoringPages ());
		}
	}

	private IEnumerator AddColoringPages(){
		while (true) {
			if (AllPagesAdded) {
				break;
			} else {
				yield return new WaitForEndOfFrame ();
				AddNextPage ();
			}
		}
	}

	void AddNextPage(){
		SelectColoringPageButton obj = Instantiate (ColoringPageButonPrefab, ColoringPagesPickerContainerContent);
		obj.Init (this,ColoringPages [currentPageId]);
		currentPageId++;
		if (currentPageId >= ColoringPages.Count) {
			AllPagesAdded = true;
		}
		ColoringPagesPickerContainerContent.GetComponent<RectTransform> ().sizeDelta = new Vector2 (currentPageId * 330, 400);
	}


	public void ClosePagesBrowser(){
		if (state == AppState.PAGE_PICKER_ENABLED) {
			state = AppState.COLORING;
			ColoringPagesPicker.gameObject.SetActive (false);
		}
	}

	public void InitWithNamedColorPage(string name){
		foreach (Texture2D t in ColoringPages) {
			if (t.name.Equals(name)) {
				InitWithColoringPage(t);
				return;
			}
		}
	}
}
