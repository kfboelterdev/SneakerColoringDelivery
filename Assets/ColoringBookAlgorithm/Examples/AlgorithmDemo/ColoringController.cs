using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace pl.ayground
{
	public class ColoringController : MonoBehaviour
	{
		/// <summary>
		/// This is the place where you present the image. DrawableTextureContainer uses Texture2D and RawImage likes this format :)
		/// </summary>
		public RawImage image;
		/// <summary>
		/// This is to ease the scaling of various images (check in LoadNextImage how I use it)
		/// </summary>
		public RectTransform ImageUIContainer;
		/// <summary>
		/// A list of sample coloring pages, first 4 are PNGs prepared for use in coloring app, 5th is a photo of a coloring page to show how BradleyLocalTreshold works 
		/// </summary>
		public List<Texture2D> Samples;

		/// <summary>
		/// Camera for ScreenPointToLocalPointInRectangle use - see line 118 where its being used. 
		/// Also need to use "ScreenSpace - Camera" render mode for Canvas to properly get image position from click using this method.
		/// </summary>
		public Camera MyCamera;

		private DrawableTextureContainer imageContainer;
		private int imageNumber = 0;
		private float initialImageScale = 1; 

		/// <summary>
		/// Loads the next image.
		/// </summary>
		public void LoadNextImage(){
			imageNumber++;
			if (imageNumber >= 6) {
				imageNumber = 0;
			}

			// THAT IS HOW YOU CAN LOAD IMAGES FROM RESOURCES (*.bytes files) instead of direct Texture2d access.
			//			string ImageName = "sample" + imageNumber;
			//			TextAsset bindata = Resources.Load (ImageName) as TextAsset;
			//			Texture2D tex = new Texture2D (1, 1);
			//			tex.LoadImage (bindata.bytes); 


			// OR JUST USE TEXTURES DIRECTLY - 
			Texture2D tex = Samples [imageNumber];



			// THIS IS HOW YOU USE DrawableTextureContainer (two options, B&W prepared files [0..4], or photo [5])
			if (imageNumber != 5) {
				// load a prepared file
				imageContainer = new DrawableTextureContainer (tex, false, false);	
			} else {
				// Load a photo [True is for BradleyLocalTreshold]
				imageContainer = new DrawableTextureContainer (tex, true, false);	
			}
			// That is it, now load our RawImage texture: 
			image.texture = imageContainer.getTexture ();
			// All done




			// All below is my naive way to get things scalled well - you will probably get this done way better :)
			float imageWidth = tex.width;
			float imageHeight = tex.height;
			AspectRatioFitter fitter = image.GetComponent<AspectRatioFitter> ();
			fitter.aspectRatio = imageWidth / imageHeight;
			float initialImageScaleX = ImageUIContainer.GetComponent<RectTransform>().rect.size.x / imageWidth; 
			float initialImageScaleY = ImageUIContainer.GetComponent<RectTransform>().rect.size.y / imageHeight; 


			if (initialImageScaleX < 1 && initialImageScaleY < 1) {
				initialImageScale = Mathf.Min (initialImageScaleY, initialImageScaleX);
			} else if (initialImageScaleX < 1) {
				initialImageScale = initialImageScaleX;
			} else if (initialImageScaleY < 1) {
				initialImageScale = initialImageScaleY;
			}
			else {
				initialImageScale = Mathf.Min(initialImageScaleY, initialImageScaleX);
			}
		}

		/// <summary>
		/// Random coloring(random color and location). Do check the second line of this method - you always need to access the new texture.
		/// </summary>
		public void RandomColoring(){
			imageContainer.RandomPaintBucketTool ();
			image.texture = imageContainer.getTexture ();
		}

		void Start(){
			LoadNextImage ();
		}

		void Update() {
			if (Input.GetMouseButtonDown (0)) {
				Click (Input.mousePosition);
			} else if (Input.touches.Length > 0) {
				if (Input.touches [0].phase == TouchPhase.Began) {
					Click (Input.touches [0].position);
				}
			}
		}

		/// <summary>
		/// This is the Click handler - random color is used in the clicked position.
		/// </summary>
		/// <param name="position">Position - in source image resolution!</param>
		void Click(Vector2 position){
			Vector2 localCursor;
			if (!RectTransformUtility.ScreenPointToLocalPointInRectangle (image.GetComponent<RectTransform> (), position, MyCamera, out localCursor)) {
				return;
			}
			//Debug.Log ("LocalCursor (screen):" + localCursor);
			localCursor /= initialImageScale;
			//Debug.Log ("ScaleCorrected (screen):" + localCursor);
			localCursor.x += imageContainer.getWidth() / 2;
			localCursor.y += imageContainer.getHeight() / 2;
			//Debug.Log ("Paint Bucket Click Location (image coordinates): " + localCursor);
			imageContainer.RandomPaintBucketToolInPosition ((int)localCursor.x, (int)localCursor.y);
			image.texture = imageContainer.getTexture ();
		}



		//		/// <summary>
		//		/// This is how you color with Coloring Book Algorithm package
		//		/// </summary>
		//		/// <param name="inPosition">X/Y location on the source image (in source image resolution)</param>
		//		/// <param name="withColor">A color to use</param>
		public void ThisIsHowYouColor(Vector2 inPosition, Color32 withColor) {
			//1. Call the algorithm - imageContainer is an instance of my "DrawableTextureContainer" class
			//   It uses integer X and Y params for the location on source image
			imageContainer.PaintBucketTool((int)inPosition.x, (int)inPosition.y, withColor);
			//2. Update the texture - image is a Unity UI RawImage class. 
			//   But actually you can use the getTexture() result wherever you like (its a Texture2D instance).
			image.texture = imageContainer.getTexture();
		}


	}
}