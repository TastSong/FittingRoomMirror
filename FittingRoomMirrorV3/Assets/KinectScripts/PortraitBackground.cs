using UnityEngine;
using System.Collections;

public class PortraitBackground : MonoBehaviour 
{
	public bool useDepthImageResolution = false;

	private Rect backgroundRect;
	private static PortraitBackground instance = null;


	public static PortraitBackground Instance
	{
		get
		{
			return instance;
		}
	}
	

	public Rect GetBackgroundRect()
	{
		return backgroundRect;
	}


	void Start () 
	{
		KinectManager kinectManager = KinectManager.Instance;

		if(kinectManager && kinectManager.IsInitialized())
		{
			float fFactorDW = 0f;
			if(!useDepthImageResolution)
			{
				fFactorDW = (float)kinectManager.GetColorImageWidth() / (float)kinectManager.GetColorImageHeight() -
					(float)kinectManager.GetColorImageHeight() / (float)kinectManager.GetColorImageWidth();
			}
			else
			{
				fFactorDW = (float)kinectManager.GetDepthImageWidth() / (float)kinectManager.GetDepthImageHeight() -
					(float)kinectManager.GetDepthImageHeight() / (float)kinectManager.GetDepthImageWidth();
			}

			float fDeltaWidth = (float)Screen.height * fFactorDW;
			float dOffsetX = -fDeltaWidth / 2f;

			float fFactorSW = 0f;
			if(!useDepthImageResolution)
			{
				fFactorSW = (float)kinectManager.GetColorImageWidth() / (float)kinectManager.GetColorImageHeight();
			}
			else
			{
				fFactorSW = (float)kinectManager.GetDepthImageWidth() / (float)kinectManager.GetDepthImageHeight();
			}

			float fScreenWidth = (float)Screen.height * fFactorSW;

			GUITexture guiTexture = GetComponent<GUITexture>();
			if(guiTexture)
			{
				guiTexture.pixelInset = new Rect(dOffsetX, 0, fDeltaWidth, 0);
			}

			backgroundRect = new Rect(dOffsetX, 0, fScreenWidth, Screen.height);
			instance = this;
		}
	}
}
