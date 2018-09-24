using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using Microsoft.Kinect.VisualGestureBuilder;
using Windows.Kinect;
using System.IO;

public interface VisualGestureListenerInterface
{
	void GestureInProgress(long userId, int userIndex, string gesture, float progress);
	
	bool GestureCompleted(long userId, int userIndex, string gesture, float confidence);
}

public struct VisualGestureData
{
	public long userId;
	public float timestamp;
	public string gestureName;
	public bool isDiscrete;
	public bool isContinuous;
	public bool isComplete;
	public bool isResetting;
	public float confidence;
	public float progress;
}

public class VisualGestureManager : MonoBehaviour 
{
	public int playerIndex = 0;

	public string gestureDatabase = string.Empty;

	public List<string> gestureNames = new List<string>();

	public float minConfidence = 0.1f;

	public List<MonoBehaviour> visualGestureListeners;
	
	public GUIText debugText;

	private long primaryUserID = 0;

	private Dictionary<string, VisualGestureData> gestureData = new Dictionary<string, VisualGestureData>();

	private VisualGestureBuilderFrameSource vgbFrameSource = null;
	
	private VisualGestureBuilderFrameReader vgbFrameReader = null;
	
	private bool isVisualGestureInitialized = false;
	
	private static VisualGestureManager instance;
	
	public static VisualGestureManager Instance
    {
        get
        {
            return instance;
        }
    }
	
	public bool IsVisualGestureInitialized()
	{
		return isVisualGestureInitialized;
	}
	
	public long GetTrackedUserID()
	{
		return primaryUserID;
	}
	
	public List<string> GetGesturesList()
	{
		return gestureNames;
	}
	
	public int GetGesturesCount()
	{
		return gestureNames.Count;
	}

	/// <param name="i">The index</param>
	public string GetGestureAtIndex(int i)
	{
		if(i >= 0 && i < gestureNames.Count)
		{
			return gestureNames[i];
		}

		return string.Empty;
	}
	
	public bool IsTrackingGesture(string gestureName)
	{
		return gestureNames.Contains(gestureName);
	}
	
	public bool IsGestureCompleted(string gestureName, bool bResetOnComplete)
	{
		if(gestureNames.Contains(gestureName))
		{
			VisualGestureData data = gestureData[gestureName];
			
			if(data.isDiscrete && data.isComplete && !data.isResetting && data.confidence >= minConfidence)
			{
				if(bResetOnComplete)
				{
					data.isResetting = true;
					gestureData[gestureName] = data;
				}

				return true;
			}
		}

		return false;
	}

	public float GetGestureConfidence(string gestureName)
	{
		if(gestureNames.Contains(gestureName))
		{
			VisualGestureData data = gestureData[gestureName];
			
			if(data.isDiscrete)
			{
				return data.confidence;
			}
		}
		
		return 0f;
	}

	public float GetGestureProgress(string gestureName)
	{
		if(gestureNames.Contains(gestureName))
		{
			VisualGestureData data = gestureData[gestureName];
			
			if(data.isContinuous)
			{
				return data.progress;
			}
		}
		
		return 0f;
	}

	void Start() 
	{
		try 
		{
			
			KinectManager kinectManager = KinectManager.Instance;
			KinectInterop.SensorData sensorData = kinectManager != null ? kinectManager.GetSensorData() : null;

			if(sensorData == null || sensorData.sensorInterface == null)
			{
				throw new Exception("Visual gesture tracking cannot be started, because the KinectManager is missing or not initialized.");
			}

			if(sensorData.sensorInterface.GetSensorPlatform() != KinectInterop.DepthSensorPlatform.KinectSDKv2)
			{
				throw new Exception("Visual gesture tracking is only supported by Kinect SDK v2");
			}
			
			bool bNeedRestart = false;
			if(IsVisualGesturesAvailable(ref bNeedRestart))
			{
				if(bNeedRestart)
				{
					KinectInterop.RestartLevel(gameObject, "VG");
					return;
				}
			}
			else
			{
				throw new Exception("Visual gesture tracking is not supported!");
			}

			if (!InitVisualGestures())
	        {
				throw new Exception("Visual gesture tracking could not be initialized.");
	        }
			
			if(visualGestureListeners.Count == 0)
			{
				MonoBehaviour[] monoScripts = FindObjectsOfType(typeof(MonoBehaviour)) as MonoBehaviour[];
				
				foreach(MonoBehaviour monoScript in monoScripts)
				{
					if(typeof(VisualGestureListenerInterface).IsAssignableFrom(monoScript.GetType()) &&
					   monoScript.enabled)
					{
						visualGestureListeners.Add(monoScript);
					}
				}
			}

			instance = this;
			isVisualGestureInitialized = true;
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
		if(isVisualGestureInitialized)
		{
			// finish visual gesture tracking
			FinishVisualGestures();
		}

		isVisualGestureInitialized = false;
		instance = null;
	}
	
	void Update() 
	{
		if(isVisualGestureInitialized)
		{
			KinectManager kinectManager = KinectManager.Instance;
			if(kinectManager && kinectManager.IsInitialized())
			{
				primaryUserID = kinectManager.GetUserIdByIndex(playerIndex);
			}

			// update visual gesture tracking
			if(UpdateVisualGestures(primaryUserID))
			{
				// process the gestures
				foreach(string gestureName in gestureNames)
				{
					if(gestureData.ContainsKey(gestureName))
					{
						VisualGestureData data = gestureData[gestureName];

						if(data.isComplete && !data.isResetting && data.confidence >= minConfidence)
						{
							Debug.Log(gestureName + " detected.");
							int userIndex = kinectManager ? kinectManager.GetUserIndexById(data.userId) : 0;

							foreach(VisualGestureListenerInterface listener in visualGestureListeners)
							{
								if(listener.GestureCompleted(data.userId, userIndex, data.gestureName, data.confidence))
								{
									data.isResetting = true;
									gestureData[gestureName] = data;
								}
							}
						}
						else if(data.progress >= 0.1f)
						{
							int userIndex = kinectManager ? kinectManager.GetUserIndexById(data.userId) : 0;

							foreach(VisualGestureListenerInterface listener in visualGestureListeners)
							{
								listener.GestureInProgress(data.userId, userIndex, data.gestureName, data.progress);
							}
						}
					}
				}
			}
		}
	}
	
	private bool IsVisualGesturesAvailable(ref bool bNeedRestart)
	{
		bool bOneCopied = false, bAllCopied = true;
		string sTargetPath = ".";
		
		if(!KinectInterop.Is64bitArchitecture())
		{
			// 32 bit
			sTargetPath = KinectInterop.GetTargetDllPath(".", false) + "/";
			
			Dictionary<string, string> dictFilesToUnzip = new Dictionary<string, string>();
			dictFilesToUnzip["Kinect20.VisualGestureBuilder.dll"] = sTargetPath + "Kinect20.VisualGestureBuilder.dll";
			dictFilesToUnzip["KinectVisualGestureBuilderUnityAddin.dll"] = sTargetPath + "KinectVisualGestureBuilderUnityAddin.dll";
			dictFilesToUnzip["vgbtechs/AdaBoostTech.dll"] = sTargetPath + "vgbtechs/AdaBoostTech.dll";
			dictFilesToUnzip["vgbtechs/RFRProgressTech.dll"] = sTargetPath + "vgbtechs/RFRProgressTech.dll";
			dictFilesToUnzip["msvcp110.dll"] = sTargetPath + "msvcp110.dll";
			dictFilesToUnzip["msvcr110.dll"] = sTargetPath + "msvcr110.dll";
			
			KinectInterop.UnzipResourceFiles(dictFilesToUnzip, "KinectV2UnityAddin.x86.zip", ref bOneCopied, ref bAllCopied);
		}
		else
		{
			//Debug.Log("Face - x64-architecture.");
			sTargetPath = KinectInterop.GetTargetDllPath(".", true) + "/";
			
			Dictionary<string, string> dictFilesToUnzip = new Dictionary<string, string>();
			dictFilesToUnzip["Kinect20.VisualGestureBuilder.dll"] = sTargetPath + "Kinect20.VisualGestureBuilder.dll";
			dictFilesToUnzip["KinectVisualGestureBuilderUnityAddin.dll"] = sTargetPath + "KinectVisualGestureBuilderUnityAddin.dll";
			dictFilesToUnzip["vgbtechs/AdaBoostTech.dll"] = sTargetPath + "vgbtechs/AdaBoostTech.dll";
			dictFilesToUnzip["vgbtechs/RFRProgressTech.dll"] = sTargetPath + "vgbtechs/RFRProgressTech.dll";
			dictFilesToUnzip["msvcp110.dll"] = sTargetPath + "msvcp110.dll";
			dictFilesToUnzip["msvcr110.dll"] = sTargetPath + "msvcr110.dll";
			
			KinectInterop.UnzipResourceFiles(dictFilesToUnzip, "KinectV2UnityAddin.x64.zip", ref bOneCopied, ref bAllCopied);
		}

		bNeedRestart = (bOneCopied && bAllCopied);
		
		return true;
	}
	
	private bool InitVisualGestures()
	{
		KinectManager kinectManager = KinectManager.Instance;
		KinectInterop.SensorData sensorData = kinectManager != null ? kinectManager.GetSensorData() : null;

		Kinect2Interface kinectInterface = sensorData.sensorInterface as Kinect2Interface;
		KinectSensor kinectSensor = kinectInterface != null ? kinectInterface.kinectSensor : null;

		if(kinectSensor == null)
			return false;

		if(gestureDatabase == string.Empty)
		{
			Debug.LogError("Please specify gesture database file!");
			return false;
		}

		if(!File.Exists(gestureDatabase))
		{
			TextAsset textRes = Resources.Load(gestureDatabase, typeof(TextAsset)) as TextAsset;
			
			if(textRes != null && textRes.bytes.Length != 0)
			{
				File.WriteAllBytes(gestureDatabase, textRes.bytes);
			}
		}
		
		vgbFrameSource = VisualGestureBuilderFrameSource.Create(kinectSensor, 0);

		vgbFrameReader = vgbFrameSource != null ? vgbFrameSource.OpenReader() : null;
		if(vgbFrameReader != null)
		{
			vgbFrameReader.IsPaused = true;
		}
		
		using (VisualGestureBuilderDatabase database = VisualGestureBuilderDatabase.Create(gestureDatabase))
		{
			if(database == null)
			{
				Debug.LogError("Gesture database not found: " + gestureDatabase);
				return false;
			}

			// check if we need to load all gestures
			bool bAllGestures = (gestureNames.Count == 0);

			foreach (Gesture gesture in database.AvailableGestures)
			{
				bool bAddGesture = bAllGestures || gestureNames.Contains(gesture.Name);

				if(bAddGesture)
				{
					string sGestureName = gesture.Name;
					vgbFrameSource.AddGesture(gesture);

					if(!gestureNames.Contains(sGestureName))
					{
						gestureNames.Add(sGestureName);
					}

					if(!gestureData.ContainsKey(sGestureName))
					{
						VisualGestureData data = new VisualGestureData();
						data.gestureName = sGestureName;
						data.isDiscrete = (gesture.GestureType == GestureType.Discrete);
						data.isContinuous = (gesture.GestureType == GestureType.Continuous);
						data.timestamp = Time.realtimeSinceStartup;
						
						gestureData.Add(sGestureName, data);
					}
				}
			}
		}

		return true;
	}
	
	private void FinishVisualGestures()
	{
		if (vgbFrameReader != null)
		{
			vgbFrameReader.Dispose();
			vgbFrameReader = null;
		}
		
		if (vgbFrameSource != null)
		{
			vgbFrameSource.Dispose();
			vgbFrameSource = null;
		}

		if(gestureData != null)
		{
			gestureData.Clear();
		}
	}
	
	private bool UpdateVisualGestures(long userId)
	{
		if(vgbFrameSource == null || vgbFrameReader == null)
			return false;

		bool wasPaused = vgbFrameReader.IsPaused;
		vgbFrameSource.TrackingId = (ulong)userId;
		vgbFrameReader.IsPaused = (userId == 0);

		if(vgbFrameReader.IsPaused)
		{
			if(!wasPaused)
			{
				foreach (Gesture gesture in vgbFrameSource.Gestures)
				{
					if(gestureData.ContainsKey(gesture.Name))
					{
						VisualGestureData data = gestureData[gesture.Name];

						data.userId = 0;
						data.isComplete = false;
						data.isResetting = false;
						data.confidence = 0f;
						data.progress = 0f;
						data.timestamp = Time.realtimeSinceStartup;
						
						gestureData[gesture.Name] = data;
					}
				}
			}

			return false;
		}

		VisualGestureBuilderFrame frame = vgbFrameReader.CalculateAndAcquireLatestFrame();

		if(frame != null)
		{
			Dictionary<Gesture, DiscreteGestureResult> discreteResults = frame.DiscreteGestureResults;
			Dictionary<Gesture, ContinuousGestureResult> continuousResults = frame.ContinuousGestureResults;

			if (discreteResults != null)
			{
				foreach (Gesture gesture in discreteResults.Keys)
				{
					if(gesture.GestureType == GestureType.Discrete && gestureData.ContainsKey(gesture.Name))
					{
						DiscreteGestureResult result = discreteResults[gesture];
						VisualGestureData data = gestureData[gesture.Name];

						data.userId = vgbFrameSource.IsTrackingIdValid ? (long)vgbFrameSource.TrackingId : 0;
						data.isComplete = result.Detected;
						data.confidence = result.Confidence;
						data.timestamp = Time.realtimeSinceStartup;

						//Debug.Log(string.Format ("{0} - {1}, confidence: {2:F0}%", data.gestureName, data.isComplete ? "Yes" : "No", data.confidence * 100f));

						if(data.isResetting && !data.isComplete)
						{
							data.isResetting = false;
						}

						gestureData[gesture.Name] = data;
					}
				}
			}

			if (continuousResults != null)
			{
				foreach (Gesture gesture in continuousResults.Keys)
				{
					if(gesture.GestureType == GestureType.Continuous && gestureData.ContainsKey(gesture.Name))
					{
						ContinuousGestureResult result = continuousResults[gesture];
						VisualGestureData data = gestureData[gesture.Name];

						data.userId = vgbFrameSource.IsTrackingIdValid ? (long)vgbFrameSource.TrackingId : 0;
						data.progress = result.Progress;
						data.timestamp = Time.realtimeSinceStartup;

						gestureData[gesture.Name] = data;
					}
				}
			}
			
			frame.Dispose();
			frame = null;
		}

		return true;
	}
	
}
