using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;


public class BackgroundRemovalManager : MonoBehaviour 
{
	
	public int playerIndex = -1;
	
	public Camera foregroundCamera;

	public bool colorCameraResolution = true;

	public bool computeBodyTexOnly = false;

	private Color32 defaultColor = new Color32(64, 64, 64, 255);
	
	public GUIText debugText;

	private byte[] foregroundImage;
	
	private Texture2D foregroundTex;
	
	private Rect foregroundRect;
	
	// primary sensor data structure
	private KinectInterop.SensorData sensorData = null;
	
	private bool isBrInited = false;
	
	private static BackgroundRemovalManager instance;
	
    public static BackgroundRemovalManager Instance
    {
        get
        {
            return instance;
        }
    }
	
	public bool IsBackgroundRemovalInitialized()
	{
		return isBrInited;
	}
	
//	// returns the raw foreground image
//	public byte[] GetForegroundImage()
//	{
//		return foregroundImage;
//	}
	
	public Texture GetForegroundTex()
	{ 
		bool bHiResSupported = sensorData != null && sensorData.sensorInterface != null ?
			sensorData.sensorInterface.IsBRHiResSupported() : false;
		bool bKinect1Int = sensorData != null && sensorData.sensorInterface != null ?
			(sensorData.sensorInterface.GetSensorPlatform() == KinectInterop.DepthSensorPlatform.KinectSDKv1) : false;

		if(computeBodyTexOnly && sensorData != null && sensorData.alphaBodyTexture)
		{
			return sensorData.alphaBodyTexture;
		}
		else if(sensorData != null && bHiResSupported && !bKinect1Int && sensorData.color2DepthTexture)
		{
			return sensorData.color2DepthTexture;
		}
		else if(sensorData != null && !bKinect1Int && sensorData.depth2ColorTexture)
		{
			return sensorData.depth2ColorTexture;
		}
		
		return foregroundTex;
	}

	public Texture GetAlphaBodyTex()
	{
		if(sensorData != null)
		{
			if(sensorData.alphaBodyTexture != null)
				return sensorData.alphaBodyTexture;
			else
				return sensorData.bodyIndexTexture;
		}

		return null;
	}
	
	void Start() 
	{
		try 
		{
			KinectManager kinectManager = KinectManager.Instance;
			if(kinectManager && kinectManager.IsInitialized())
			{
				sensorData = kinectManager.GetSensorData();
			}
			
			if(sensorData == null || sensorData.sensorInterface == null)
			{
				throw new Exception("Background removal cannot be started, because KinectManager is missing or not initialized.");
			}
			
			bool bNeedRestart = false;
			bool bSuccess = sensorData.sensorInterface.IsBackgroundRemovalAvailable(ref bNeedRestart);

			if(bSuccess)
			{
				if(bNeedRestart)
				{
					KinectInterop.RestartLevel(gameObject, "BR");
					return;
				}
			}
			else
			{
				string sInterfaceName = sensorData.sensorInterface.GetType().Name;
				throw new Exception(sInterfaceName + ": Background removal is not supported!");
			}
			
			bSuccess = sensorData.sensorInterface.InitBackgroundRemoval(sensorData, colorCameraResolution);

			if (!bSuccess)
	        {
				throw new Exception("Background removal could not be initialized.");
	        }

			int imageLength = sensorData.sensorInterface.GetForegroundFrameLength(sensorData, colorCameraResolution);
			foregroundImage = new byte[imageLength];

			Rect neededFgRect = sensorData.sensorInterface.GetForegroundFrameRect(sensorData, colorCameraResolution);

			foregroundTex = new Texture2D((int)neededFgRect.width, (int)neededFgRect.height, TextureFormat.RGBA32, false);

			if(foregroundCamera != null)
			{
				Rect cameraRect = foregroundCamera.pixelRect;
				float rectHeight = cameraRect.height;
				float rectWidth = cameraRect.width;
				
				if(rectWidth > rectHeight)
					rectWidth = Mathf.Round(rectHeight * neededFgRect.width / neededFgRect.height);
				else
					rectHeight = Mathf.Round(rectWidth * neededFgRect.height / neededFgRect.width);
				
				foregroundRect = new Rect((cameraRect.width - rectWidth) / 2, cameraRect.height - (cameraRect.height - rectHeight) / 2, rectWidth, -rectHeight);
			}

			instance = this;
			isBrInited = true;
			
		} 
		catch(DllNotFoundException ex)
		{
			Debug.LogError(ex.ToString());
			if(debugText != null)
				debugText.GetComponent<GUIText>().text = "Please check the Kinect and BR-Library installations.";
		}
		catch (Exception ex) 
		{
			Debug.LogError(ex.ToString());
			if(debugText != null)
				debugText.GetComponent<GUIText>().text = ex.Message;
		}
	}

	void OnDestroy()
	{
		if(isBrInited && sensorData != null && sensorData.sensorInterface != null)
		{
			sensorData.sensorInterface.FinishBackgroundRemoval(sensorData);
		}
		
		isBrInited = false;
		instance = null;
	}
	
	void Update () 
	{
		if(isBrInited)
		{
			if(playerIndex != -1)
			{
				KinectManager kinectManager = KinectManager.Instance;
				long userID = 0;

				if(kinectManager && kinectManager.IsInitialized())
				{
					userID = kinectManager.GetUserIdByIndex(playerIndex);

					if(userID != 0)
					{
						sensorData.selectedBodyIndex = (byte)kinectManager.GetBodyIndexByUserId(userID);
					}
				}

				if(userID == 0)
				{
					sensorData.selectedBodyIndex = 222;
				}
			}
			else
			{
				sensorData.selectedBodyIndex = 255;
			}

			bool bSuccess = sensorData.sensorInterface.UpdateBackgroundRemoval(sensorData, colorCameraResolution, defaultColor, computeBodyTexOnly);
			
			if(bSuccess)
			{
				KinectManager kinectManager = KinectManager.Instance;
				if(kinectManager && kinectManager.IsInitialized())
				{
					bool bLimitedUsers = kinectManager.IsTrackedUsersLimited();
					List<int> alTrackedIndexes = kinectManager.GetTrackedBodyIndices();
					bSuccess = sensorData.sensorInterface.PollForegroundFrame(sensorData, colorCameraResolution, defaultColor, bLimitedUsers, alTrackedIndexes, ref foregroundImage);

					if(bSuccess)
					{
						foregroundTex.LoadRawTextureData(foregroundImage);
						foregroundTex.Apply();
					}
				}
			}
		}
	}
	
	void OnGUI()
	{
		if(isBrInited && foregroundCamera)
		{
			PortraitBackground portraitBack = PortraitBackground.Instance;
			if(portraitBack && portraitBack.enabled)
			{
				foregroundRect = portraitBack.GetBackgroundRect();

				foregroundRect.y += foregroundRect.height;  // invert y
				foregroundRect.height = -foregroundRect.height;
			}

			bool bHiResSupported = sensorData != null && sensorData.sensorInterface != null ?
				sensorData.sensorInterface.IsBRHiResSupported() : false;
			bool bKinect1Int = sensorData != null && sensorData.sensorInterface != null ?
				(sensorData.sensorInterface.GetSensorPlatform() == KinectInterop.DepthSensorPlatform.KinectSDKv1) : false;

			if(computeBodyTexOnly && sensorData != null && sensorData.alphaBodyTexture)
			{
				GUI.DrawTexture(foregroundRect, sensorData.alphaBodyTexture);
			}
			else if(sensorData != null && bHiResSupported && !bKinect1Int && sensorData.color2DepthTexture)
			{
				//GUI.DrawTexture(foregroundRect, sensorData.alphaBodyTexture);
				GUI.DrawTexture(foregroundRect, sensorData.color2DepthTexture);
			}
			else if(sensorData != null && !bKinect1Int && sensorData.depth2ColorTexture)
			{
				//GUI.DrawTexture(foregroundRect, sensorData.alphaBodyTexture);
				GUI.DrawTexture(foregroundRect, sensorData.depth2ColorTexture);
			}
			else if(foregroundTex)
			{
				//GUI.DrawTexture(foregroundRect, sensorData.alphaBodyTexture);
				GUI.DrawTexture(foregroundRect, foregroundTex);
			}
		}
	}


}
