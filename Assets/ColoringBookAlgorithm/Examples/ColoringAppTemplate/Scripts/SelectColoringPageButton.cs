using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class SelectColoringPageButton : MonoBehaviour {

	public Image image;
	private ColoringController parent;
	private string textureName;

	public void Init(ColoringController parent, Texture2D texture){
		this.parent = parent;
		this.textureName = texture.name;
		Rect rec = new Rect(0, 0, texture.width, texture.height);
		image.sprite=Sprite.Create(texture,rec,new Vector2(0.5f,0.5f),100);
		image.preserveAspect = true;
	}

	public void Click(){
		parent.InitWithNamedColorPage (textureName);
		parent.ClosePagesBrowser ();
	}
}
