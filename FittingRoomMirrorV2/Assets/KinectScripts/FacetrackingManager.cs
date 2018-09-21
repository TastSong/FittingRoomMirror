using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
//using System.Runtime.InteropServices;

public class FacetrackingManager : MonoBehaviour 
{
	public int playerIndex = 0;
	
	public bool getFaceModelData = false;

	public bool displayFaceRect = false;
	
	public float faceTrackingTolerance = 0.25f;
	
	public GameObject faceModelMesh = null;
	
	public bool mirroredModelMesh = true;

	public enum TextureType : int { None, ColorMap, FaceRectangle }

	public TextureType texturedModelMesh = TextureType.ColorMap;

	public Camera foregroundCamera;
	
	[Range(0.1f, 2.0f)]
	public float modelMeshScale = 1f;
	
	public GUIText debugText;

	private bool isTrackingFace = false;
	private float lastFaceTrackedTime = 0f;
	
	// Skeleton ID of the tracked face
	//private long faceTrackingID = 0;
	
	private Dictionary<KinectInterop.FaceShapeAnimations, float> dictAU = new Dictionary<KinectInterop.FaceShapeAnimations, float>();
	private bool bGotAU = false;

	private Dictionary<KinectInterop.FaceShapeDeformations, float> dictSU = new Dictionary<KinectInterop.FaceShapeDeformations, float>();
	private bool bGotSU = false;

	private bool bFaceModelMeshInited = false;

	private Vector3[] avModelVertices = null;
	private Vector2[] avModelUV = null;
	private bool bGotModelVertices = false;

	private Vector3 headPos = Vector3.zero;
	private bool bGotHeadPos = false;

	private Quaternion headRot = Quaternion.identity;
	private bool bGotHeadRot = false;
	
	// Tracked face rectangle
	private Rect faceRect = new Rect();
	//private bool bGotFaceRect;

	// primary user ID, as reported by KinectManager
	private long primaryUserID = 0;

	// primary sensor data structure
	private KinectInterop.SensorData sensorData = null;
	
	// Bool to keep track of whether face-tracking system has been initialized
	private bool isFacetrackingInitialized = false;
	
	// The single instance of FacetrackingManager
	private static FacetrackingManager instance;
	
    public static FacetrackingManager Instance
    {
        get
        {
            return instance;
        }
    }
	
	public bool IsFaceTrackingInitialized()
	{
		return isFacetrackingInitialized;
	}
	
	public bool IsTrackingFace()
	{
		return isTrackingFace;
	}

	public long GetFaceTrackingID()
	{
		return isTrackingFace ? primaryUserID : 0;
	}
	
	public bool IsTrackingFace(long userId)
	{
		if(sensorData != null && sensorData.sensorInterface != null)
		{
			return sensorData.sensorInterface.IsFaceTracked(userId);
		}

		return false;
	}
	
	public Vector3 GetHeadPosition(bool bMirroredMovement)
	{
		Vector3 vHeadPos = bGotHeadPos ? headPos : Vector3.zero;

		if(!bMirroredMovement)
		{
			vHeadPos.z = -vHeadPos.z;
		}
		
		return vHeadPos;
	}
	
	
	public Vector3 GetHeadPosition(long userId, bool bMirroredMovement)
	{
		Vector3 vHeadPos = Vector3.zero;
		bool bGotPosition = sensorData.sensorInterface.GetHeadPosition(userId, ref vHeadPos);

		if(bGotPosition)
		{
			if(!bMirroredMovement)
			{
				vHeadPos.z = -vHeadPos.z;
			}
			
			return vHeadPos;
		}

		return Vector3.zero;
	}
	
	public Quaternion GetHeadRotation(bool bMirroredMovement)
	{
		Vector3 rotAngles = bGotHeadRot ? headRot.eulerAngles : Vector3.zero;

		if(bMirroredMovement)
		{
			rotAngles.x = -rotAngles.x;
			rotAngles.z = -rotAngles.z;
		}
		else
		{
			rotAngles.x = -rotAngles.x;
			rotAngles.y = -rotAngles.y;
		}
		
		return Quaternion.Euler(rotAngles);
	}
	
	public Quaternion GetHeadRotation(long userId, bool bMirroredMovement)
	{
		Quaternion vHeadRot = Quaternion.identity;
		bool bGotRotation = sensorData.sensorInterface.GetHeadRotation(userId, ref vHeadRot);

		if(bGotRotation)
		{
			Vector3 rotAngles = vHeadRot.eulerAngles;
			
			if(bMirroredMovement)
			{
				rotAngles.x = -rotAngles.x;
				rotAngles.z = -rotAngles.z;
			}
			else
			{
				rotAngles.x = -rotAngles.x;
				rotAngles.y = -rotAngles.y;
			}
			
			return Quaternion.Euler(rotAngles);
		}

		return Quaternion.identity;
	}

	public Rect GetFaceColorRect(long userId)
	{
		Rect faceColorRect = new Rect();
		sensorData.sensorInterface.GetFaceRect(userId, ref faceColorRect);

		return faceColorRect;
	}
	
	public bool IsGotAU()
	{
		return bGotAU;
	}
	
	public float GetAnimUnit(KinectInterop.FaceShapeAnimations faceAnimKey)
	{
		if(dictAU.ContainsKey(faceAnimKey))
		{
			return dictAU[faceAnimKey];
		}
		
		return 0.0f;
	}
	
	public bool GetUserAnimUnits(long userId, ref Dictionary<KinectInterop.FaceShapeAnimations, float> dictAnimUnits)
	{
		if(sensorData != null && sensorData.sensorInterface != null)
		{
			bool bGotIt = sensorData.sensorInterface.GetAnimUnits(userId, ref dictAnimUnits);
			return bGotIt;
		}

		return false;
	}
	
	public bool IsGotSU()
	{
		return bGotSU;
	}
	
	public float GetShapeUnit(KinectInterop.FaceShapeDeformations faceShapeKey)
	{
		if(dictSU.ContainsKey(faceShapeKey))
		{
			return dictSU[faceShapeKey];
		}
		
		return 0.0f;
	}

	public bool GetUserShapeUnits(long userId, ref Dictionary<KinectInterop.FaceShapeDeformations, float> dictShapeUnits)
	{
		if(sensorData != null && sensorData.sensorInterface != null)
		{
			bool bGotIt = sensorData.sensorInterface.GetShapeUnits(userId, ref dictShapeUnits);
			return bGotIt;
		}
		
		return false;
	}
	
	public int GetFaceModelVertexCount()
	{
		if (bGotModelVertices) 
		{
			return avModelVertices.Length;
		} 

		return 0;
	}

	public Vector3 GetFaceModelVertex(int index)
	{
		if (bGotModelVertices) 
		{
			if(index >= 0 && index < avModelVertices.Length)
			{
				return avModelVertices[index];
			}
		}
		
		return Vector3.zero;
	}
	
	public Vector3[] GetFaceModelVertices()
	{
		if (bGotModelVertices) 
		{
			return avModelVertices;
		}

		return null;
	}

	public bool GetUserFaceVertices(long userId, ref Vector3[] avVertices)
	{
		if(sensorData != null && sensorData.sensorInterface != null)
		{
			bool bGotIt = sensorData.sensorInterface.GetFaceModelVertices(userId, ref avVertices);
			return bGotIt;
		}
		
		return false;
	}
	
	public int[] GetFaceModelTriangleIndices(bool bMirroredModel)
	{
		if(sensorData != null && sensorData.sensorInterface != null)
		{
			int iNumTriangles = sensorData.sensorInterface.GetFaceModelTrianglesCount();

			if(iNumTriangles > 0)
			{
				int[] avModelTriangles = new int[iNumTriangles];
				bool bGotModelTriangles = sensorData.sensorInterface.GetFaceModelTriangles(bMirroredModel, ref avModelTriangles);

				if(bGotModelTriangles)
				{
					return avModelTriangles;
				}
			}
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
				throw new Exception("Face tracking cannot be started, because KinectManager is missing or not initialized.");
			}

			if(debugText != null)
			{
				debugText.GetComponent<GUIText>().text = "Please, wait...";
			}
			
			bool bNeedRestart = false;
			if(sensorData.sensorInterface.IsFaceTrackingAvailable(ref bNeedRestart))
			{
				if(bNeedRestart)
				{
					KinectInterop.RestartLevel(gameObject, "FM");
					return;
				}
			}
			else
			{
				string sInterfaceName = sensorData.sensorInterface.GetType().Name;
				throw new Exception(sInterfaceName + ": Face tracking is not supported!");
			}

			if (!sensorData.sensorInterface.InitFaceTracking(getFaceModelData, displayFaceRect))
	        {
	            throw new Exception("Face tracking could not be initialized.");
	        }
			
			instance = this;
			isFacetrackingInitialized = true;

			//DontDestroyOnLoad(gameObject);

			if(debugText != null)
			{
				debugText.GetComponent<GUIText>().text = "Ready.";
			}
		} 
		catch(DllNotFoundException ex)
		{
			Debug.LogError(ex.ToString());
			if(debugText != null)
				debugText.GetComponent<GUIText>().text = "Please check the Kinect and FT-Library installations.";
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
		if(isFacetrackingInitialized && sensorData != null && sensorData.sensorInterface != null)
		{
			// finish face tracking
			sensorData.sensorInterface.FinishFaceTracking();
		}

//		// clean up
//		Resources.UnloadUnusedAssets();
//		GC.Collect();
		
		isFacetrackingInitialized = false;
		instance = null;
	}
	
	void Update() 
	{
		if(isFacetrackingInitialized)
		{
			KinectManager kinectManager = KinectManager.Instance;
			if(kinectManager && kinectManager.IsInitialized())
			{
				primaryUserID = kinectManager.GetUserIdByIndex(playerIndex);
			}

			isTrackingFace = false;

			if(sensorData.sensorInterface.UpdateFaceTracking())
			{
				isTrackingFace = sensorData.sensorInterface.IsFaceTracked(primaryUserID);

				if(isTrackingFace)
				{
					lastFaceTrackedTime = Time.realtimeSinceStartup;
					
					/**bGotFaceRect =*/ sensorData.sensorInterface.GetFaceRect(primaryUserID, ref faceRect);
					
					bGotHeadPos = sensorData.sensorInterface.GetHeadPosition(primaryUserID, ref headPos);

					// get head rotation
					bGotHeadRot = sensorData.sensorInterface.GetHeadRotation(primaryUserID, ref headRot);

					bGotAU = sensorData.sensorInterface.GetAnimUnits(primaryUserID, ref dictAU);

					bGotSU = sensorData.sensorInterface.GetShapeUnits(primaryUserID, ref dictSU);

					if(faceModelMesh != null && faceModelMesh.activeInHierarchy)
					{
						if(!bFaceModelMeshInited)
						{
							bFaceModelMeshInited = CreateFaceModelMesh(faceModelMesh, headPos, ref avModelVertices, ref avModelUV, ref bGotModelVertices);
						}
					}
					
					if(getFaceModelData)
					{
						UpdateFaceModelMesh(primaryUserID, faceModelMesh, headPos, faceRect, ref avModelVertices, ref avModelUV, ref bGotModelVertices);
					}
				}
				else if((Time.realtimeSinceStartup - lastFaceTrackedTime) <= faceTrackingTolerance)
				{
					isTrackingFace = true;
				}
			}
			
			if(faceModelMesh != null && bFaceModelMeshInited)
			{
				faceModelMesh.SetActive(isTrackingFace);
			}
		}
	}
	
	void OnGUI()
	{
		if(isFacetrackingInitialized)
		{
			if(debugText != null)
			{
				if(isTrackingFace)
				{
					debugText.GetComponent<GUIText>().text = "Tracking - BodyID: " + primaryUserID;
				}
				else
				{
					debugText.GetComponent<GUIText>().text = "Not tracking...";
				}
			}
		}
	}


	public bool CreateFaceModelMesh(GameObject faceModelMesh, Vector3 headPos, ref Vector3[] avModelVertices, ref Vector2[] avModelUV, ref bool bGotModelVertices)
	{
		if(faceModelMesh == null)
			return false;

		int iNumTriangles = sensorData.sensorInterface.GetFaceModelTrianglesCount();
		if(iNumTriangles <= 0)
			return false;

		int[] avModelTriangles = new int[iNumTriangles];
		bool bGotModelTriangles = sensorData.sensorInterface.GetFaceModelTriangles(mirroredModelMesh, ref avModelTriangles);

		if(!bGotModelTriangles)
			return false;
		
		int iNumVertices = sensorData.sensorInterface.GetFaceModelVerticesCount(0);
		if(iNumVertices < 0)
			return false;

		avModelVertices = new Vector3[iNumVertices];
		bGotModelVertices = sensorData.sensorInterface.GetFaceModelVertices(0, ref avModelVertices);

		avModelUV = new Vector2[iNumVertices];

		if(!bGotModelVertices)
			return false;

		Matrix4x4 kinectToWorld = KinectManager.Instance ? KinectManager.Instance.GetKinectToWorldMatrix() : Matrix4x4.identity;
		Vector3 headPosWorld = kinectToWorld.MultiplyPoint3x4(headPos);
		
		for(int i = 0; i < avModelVertices.Length; i++)
		{
			avModelVertices[i] = kinectToWorld.MultiplyPoint3x4(avModelVertices[i]) - headPosWorld;
		}
		
		//Quaternion faceModelRot = faceModelMesh.transform.rotation;
		//faceModelMesh.transform.rotation = Quaternion.identity;

		Mesh mesh = new Mesh();
		mesh.name = "FaceMesh";
		faceModelMesh.GetComponent<MeshFilter>().mesh = mesh;
		
		mesh.vertices = avModelVertices;
		//mesh.uv = avModelUV;
		mesh.triangles = avModelTriangles;
		mesh.RecalculateNormals();

		faceModelMesh.transform.position = headPos;
		//faceModelMesh.transform.rotation = faceModelRot;

		//bFaceModelMeshInited = true;
		return true;
	}


	public void UpdateFaceModelMesh(long userId, GameObject faceModelMesh, Vector3 headPos, Rect faceRect, ref Vector3[] avModelVertices, ref Vector2[] avModelUV, ref bool bGotModelVertices)
	{
		// init the vertices array if needed
		if(avModelVertices == null)
		{
			int iNumVertices = sensorData.sensorInterface.GetFaceModelVerticesCount(userId);
			avModelVertices = new Vector3[iNumVertices];
		}

		// get face model vertices
		bGotModelVertices = sensorData.sensorInterface.GetFaceModelVertices(userId, ref avModelVertices);
		
		if(bGotModelVertices && faceModelMesh != null)
		{
			//Quaternion faceModelRot = faceModelMesh.transform.rotation;
			//faceModelMesh.transform.rotation = Quaternion.identity;

			KinectManager kinectManager = KinectManager.Instance;
			
			if(texturedModelMesh != TextureType.None)
			{
				float colorWidth = (float)kinectManager.GetColorImageWidth();
				float colorHeight = (float)kinectManager.GetColorImageHeight();

				//bool bGotFaceRect = sensorData.sensorInterface.GetFaceRect(userId, ref faceRect);
				bool faceRectValid = /**bGotFaceRect &&*/ faceRect.width > 0 && faceRect.height > 0;

				if(texturedModelMesh == TextureType.ColorMap &&
				   faceModelMesh.GetComponent<MeshRenderer>().material.mainTexture == null)
				{
					faceModelMesh.GetComponent<MeshRenderer>().material.mainTexture = kinectManager.GetUsersClrTex();
				}

				for(int i = 0; i < avModelVertices.Length; i++)
				{
					Vector2 posDepth = kinectManager.MapSpacePointToDepthCoords(avModelVertices[i]);

					bool bUvSet = false;
					if(posDepth != Vector2.zero)
					{
						ushort depth = kinectManager.GetDepthForPixel((int)posDepth.x, (int)posDepth.y);
						Vector2 posColor = kinectManager.MapDepthPointToColorCoords(posDepth, depth);

						if(posColor != Vector2.zero && !float.IsInfinity(posColor.x) && !float.IsInfinity(posColor.y))
						{
							if(texturedModelMesh == TextureType.ColorMap)
							{
								avModelUV[i] = new Vector2(posColor.x / colorWidth, posColor.y / colorHeight);
								bUvSet = true;
							}
							else if(texturedModelMesh == TextureType.FaceRectangle && faceRectValid)
							{
								avModelUV[i] = new Vector2((posColor.x - faceRect.x) / faceRect.width, 
								                           -(posColor.y - faceRect.y) / faceRect.height);
								bUvSet = true;
							}
						}
					}

					if(!bUvSet)
					{
						avModelUV[i] = Vector2.zero;
					}
				}
			}
			else
			{
				if(faceModelMesh.GetComponent<MeshRenderer>().material.mainTexture != null)
				{
					faceModelMesh.GetComponent<MeshRenderer>().material.mainTexture = null;
				}
			}

			Matrix4x4 kinectToWorld = kinectManager ? kinectManager.GetKinectToWorldMatrix() : Matrix4x4.identity;
			Vector3 headPosWorld = kinectToWorld.MultiplyPoint3x4(headPos);

			for(int i = 0; i < avModelVertices.Length; i++)
			{
				avModelVertices[i] = kinectToWorld.MultiplyPoint3x4(avModelVertices[i]) - headPosWorld;
			}
			
			Mesh mesh = faceModelMesh.GetComponent<MeshFilter>().mesh;
			mesh.vertices = avModelVertices;
			if(texturedModelMesh != TextureType.None)
			{
				mesh.uv = avModelUV;
			}

			mesh.RecalculateNormals();
			mesh.RecalculateBounds();

			// check for head pos overlay
			Vector3 newHeadPos = headPos;

			if(foregroundCamera)
			{
				// get the background rectangle (use the portrait background, if available)
				Rect backgroundRect = foregroundCamera.pixelRect;
				PortraitBackground portraitBack = PortraitBackground.Instance;
				
				if(portraitBack && portraitBack.enabled)
				{
					backgroundRect = portraitBack.GetBackgroundRect();
				}
				
				if(kinectManager)
				{
					Vector3 posColorOverlay = kinectManager.GetJointPosColorOverlay(primaryUserID, (int)KinectInterop.JointType.Head, foregroundCamera, backgroundRect);
					
					if(posColorOverlay != Vector3.zero)
					{
						newHeadPos = posColorOverlay;
					}
				}
			}
			
			faceModelMesh.transform.position = newHeadPos;
			//faceModelMesh.transform.rotation = faceModelRot;

			if(faceModelMesh.transform.localScale.x != modelMeshScale)
			{
				faceModelMesh.transform.localScale = new Vector3(modelMeshScale, modelMeshScale, modelMeshScale);
			}
		}
	}
	
}
