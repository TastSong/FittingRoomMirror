#define USE_SINGLE_KM_IN_MULTIPLE_SCENES


using UnityEngine;

using System;
using System.Collections;
using System.Collections.Generic;

public class KinectManager : MonoBehaviour 
{
	public float sensorHeight = 1.0f;

	public float sensorAngle = 0f;
	
	public enum AutoHeightAngle : int { DontUse, ShowInfoOnly, AutoUpdate, AutoUpdateAndShowInfo }
	
	public AutoHeightAngle autoHeightAngle = AutoHeightAngle.DontUse;

	public enum UserMapType : int { None, RawUserDepth, BodyTexture, UserTexture, CutOutTexture }

	public UserMapType computeUserMap = UserMapType.RawUserDepth;

	public bool computeColorMap = false;
	
	public bool computeInfraredMap = false;
	
	public bool displayUserMap = false;
	
	public bool displayColorMap = false;
	
	public bool displaySkeletonLines = false;
	
	public float DisplayMapsWidthPercent = 20f;
	
	public bool useMultiSourceReader = false;
	
	// Public Bool to determine whether to use sensor's audio source, if available
	//public bool useAudioSource = false;
	
	public float minUserDistance = 0.5f;
	
	public float maxUserDistance = 0f;
	
	public float maxLeftRightDistance = 0f;
	
	public int maxTrackedUsers = 6;

	public bool showTrackedUsersOnly = true;
	
	public bool detectClosestUser = true;

	public bool ignoreInferredJoints = false;
	
	public bool ignoreZCoordinates = false;
	
	public bool lateUpdateAvatars = false;
	
	public enum Smoothing : int { None, Default, Medium, Aggressive }

	public Smoothing smoothing = Smoothing.Default;
	public bool useBoneOrientationConstraints = false;
	//public bool useBoneOrientationsFilter = false;
	public bool allowTurnArounds = false;
	
	public enum AllowedRotations : int { None = 0, Default = 1, All = 2 }
	
	public AllowedRotations allowedHandRotations = AllowedRotations.Default;

	public float waitTimeBeforeRemove = 1f;

	public List<AvatarController> avatarControllers = new List<AvatarController>();
	
	public KinectGestures.Gestures playerCalibrationPose;
	
	public List<KinectGestures.Gestures> playerCommonGestures = new List<KinectGestures.Gestures>();

	public float minTimeBetweenGestures = 0.7f;
	
	public KinectGestures gestureManager;
	
	public List<MonoBehaviour> gestureListeners = new List<MonoBehaviour>();

	public GUIText calibrationText;
	
	public GUIText gesturesDebugText;

	private bool kinectInitialized = false; 
	
	private static KinectManager instance = null;

	private List<DepthSensorInterface> sensorInterfaces = null;

	private KinectInterop.SensorData sensorData = null;

	// Depth and user maps
//	private KinectInterop.DepthBuffer depthImage;
//	private KinectInterop.BodyIndexBuffer bodyIndexImage;
//	private KinectInterop.UserHistogramBuffer userHistogramImage;
	private Color32[] usersHistogramImage;
	private ushort[] usersPrevState;
	private float[] usersHistogramMap;

	private Texture2D usersLblTex;
	private Rect usersMapRect;
	private int usersMapSize;
//	private int minDepth;
//	private int maxDepth;
	
	// Color map
	//private KinectInterop.ColorBuffer colorImage;
	//private Texture2D usersClrTex;
	private Rect usersClrRect;
	private int usersClrSize;
	
	private KinectInterop.BodyFrameData bodyFrame;
	//private Int64 lastBodyFrameTime = 0;
	
	private List<Int64> alUserIds = new List<Int64>();
	private Dictionary<Int64, int> dictUserIdToIndex = new Dictionary<Int64, int>();
	private Int64[] aUserIndexIds = new Int64[KinectInterop.Constants.MaxBodyCount];
	private Dictionary<Int64, float> dictUserIdToTime = new Dictionary<Int64, float>();

	private bool bLimitedUsers = false;
	
	private Int64 liPrimaryUserId = 0;
	
	private Matrix4x4 kinectToWorld = Matrix4x4.zero;
	//private Matrix4x4 mOrient = Matrix4x4.zero;

	private Dictionary<Int64, KinectGestures.GestureData> playerCalibrationData = new Dictionary<Int64, KinectGestures.GestureData>();
	
	private Dictionary<Int64, List<KinectGestures.GestureData>> playerGesturesData = new Dictionary<Int64, List<KinectGestures.GestureData>>();
	private Dictionary<Int64, float> gesturesTrackingAtTime = new Dictionary<Int64, float>();
	
	
	private JointPositionsFilter jointPositionFilter = null;
	private BoneOrientationsConstraint boneConstraintsFilter = null;
	//private BoneOrientationsFilter boneOrientationFilter = null;

	private System.Threading.Thread kinectReaderThread = null;
	private bool kinectReaderRunning = false;

    public static KinectManager Instance
    {
        get
        {
            return instance;
        }
    }

	public static bool IsKinectInitialized()
	{
		return instance != null ? instance.kinectInitialized : false;
	}
	
	public bool IsInitialized()
	{
		return kinectInitialized;
	}

	internal KinectInterop.SensorData GetSensorData()
	{
		return sensorData;
	}

	public KinectInterop.DepthSensorPlatform GetSensorPlatform()
	{
		if(sensorData != null && sensorData.sensorInterface != null)
		{
			return sensorData.sensorInterface.GetSensorPlatform();
		}
		
		return KinectInterop.DepthSensorPlatform.None;
	}
	
	public int GetBodyCount()
	{
		return sensorData != null ? sensorData.bodyCount : 0;
	}
	
	public int GetJointCount()
	{
		return sensorData != null ? sensorData.jointCount : 0;
	}

	public int GetJointIndex(KinectInterop.JointType joint)
	{
		if(sensorData != null && sensorData.sensorInterface != null)
		{
			return sensorData.sensorInterface.GetJointIndex(joint);
		}
		
		return (int)joint;
	}
	
//	// returns the joint at given index
//	public KinectInterop.JointType GetJointAtIndex(int index)
//	{
//		if(sensorData != null && sensorData.sensorInterface != null)
//		{
//			return sensorData.sensorInterface.GetJointAtIndex(index);
//		}
//		
//		// fallback - index matches the joint
//		return (KinectInterop.JointType)index;
//	}
	
	public KinectInterop.JointType GetParentJoint(KinectInterop.JointType joint)
	{
		if(sensorData != null && sensorData.sensorInterface != null)
		{
			return sensorData.sensorInterface.GetParentJoint(joint);
		}

		return joint;
	}

	public KinectInterop.JointType GetNextJoint(KinectInterop.JointType joint)
	{
		if(sensorData != null && sensorData.sensorInterface != null)
		{
			return sensorData.sensorInterface.GetNextJoint(joint);
		}
		
		return joint;
	}
	
	public int GetColorImageWidth()
	{
		return sensorData != null ? sensorData.colorImageWidth : 0;
	}
	
	public int GetColorImageHeight()
	{
		return sensorData != null ? sensorData.colorImageHeight : 0;
	}

	public int GetDepthImageWidth()
	{
		return sensorData != null ? sensorData.depthImageWidth : 0;
	}
	
	public int GetDepthImageHeight()
	{
		return sensorData != null ? sensorData.depthImageHeight : 0;
	}
	
	public byte[] GetRawBodyIndexMap()
	{
		return sensorData != null ? sensorData.bodyIndexImage : null;
	}
	
	public ushort[] GetRawDepthMap()
	{
		return sensorData != null ? sensorData.depthImage : null;
	}

	public ushort[] GetRawInfraredMap()
	{
		return sensorData != null ? sensorData.infraredImage : null;
	}

    public Texture2D GetUsersLblTex()
    { 
		return usersLblTex;
	}
	
	public Texture2D GetUsersClrTex()
	{ 
		return sensorData != null ? sensorData.colorImageTexture : null;
	}

	public bool IsUserDetected()
	{
		return kinectInitialized && (alUserIds.Count > 0);
	}
	
	public bool IsUserTracked(Int64 userId)
	{
		return dictUserIdToIndex.ContainsKey(userId);
	}
	
	public int GetUsersCount()
	{
		return alUserIds.Count;
	}

	public List<long> GetAllUserIds()
	{
		return new List<long>(alUserIds);
	}
	
	public Int64 GetUserIdByIndex(int i)
	{
//		if(i >= 0 && i < alUserIds.Count)
//		{
//			return alUserIds[i];
//		}
		
		if(i >= 0 && i < KinectInterop.Constants.MaxBodyCount)
		{
			return aUserIndexIds[i];
		}
		
		return 0;
	}

	public int GetUserIndexById(Int64 userId)
	{
//		for(int i = 0; i < alUserIds.Count; i++)
//		{
//			if(alUserIds[i] == userId)
//			{
//				return i;
//			}
//		}
		
		for(int i = 0; i < aUserIndexIds.Length; i++)
		{
			if(aUserIndexIds[i] == userId)
			{
				return i;
			}
		}
		
		return -1;
	}
	
	public int GetBodyIndexByUserId(Int64 userId)
	{
		if(dictUserIdToIndex.ContainsKey(userId))
		{
			int index = dictUserIdToIndex[userId];
			return index;
		}
		
		return -1;
	}

	public List<int> GetTrackedBodyIndices()
	{
		List<int> alBodyIndices = new List<int>(dictUserIdToIndex.Values);
		return alBodyIndices;
	}

	public bool IsTrackedUsersLimited()
	{
		return bLimitedUsers;
	}
	
	public Int64 GetPrimaryUserID()
	{
		return liPrimaryUserId;
	}

	public bool SetPrimaryUserID(Int64 userId)
	{
		bool bResult = false;

		if(alUserIds.Contains(userId) || (userId == 0))
		{
			liPrimaryUserId = userId;
			bResult = true;
		}

		return bResult;
	}

	public int GetDisplayedBodyIndex()
	{
		if(sensorData != null)
		{
			return sensorData.selectedBodyIndex != 255 ? sensorData.selectedBodyIndex : -1;
		}

		return -1;
	}

	public bool SetDisplayedBodyIndex(int iBodyIndex)
	{
		if(sensorData != null)
		{
			sensorData.selectedBodyIndex = (byte)(iBodyIndex >= 0 ? iBodyIndex : 255);
		}

		return false;
	}
	
	public long GetBodyFrameTimestamp()
	{
		return bodyFrame.liRelativeTime;
	}
	
	internal KinectInterop.BodyData GetUserBodyData(Int64 userId)
	{
		if(dictUserIdToIndex.ContainsKey(userId))
		{
			int index = dictUserIdToIndex[userId];
			
			if(index >= 0 && index < sensorData.bodyCount)
			{
				return bodyFrame.bodyData[index];
			}
		}
		
		return new KinectInterop.BodyData();
	}

	public Matrix4x4 GetKinectToWorldMatrix()
	{
		return kinectToWorld;
	}
	
	public Vector3 GetUserPosition(Int64 userId)
	{
		if(dictUserIdToIndex.ContainsKey(userId))
		{
			int index = dictUserIdToIndex[userId];
			
			if(index >= 0 && index < sensorData.bodyCount && 
				bodyFrame.bodyData[index].bIsTracked != 0)
			{
				return bodyFrame.bodyData[index].position;
			}
		}
		
		return Vector3.zero;
	}
	
	public Quaternion GetUserOrientation(Int64 userId, bool flip)
	{
		if(dictUserIdToIndex.ContainsKey(userId))
		{
			int index = dictUserIdToIndex[userId];
			
			if(index >= 0 && index < sensorData.bodyCount && 
			   bodyFrame.bodyData[index].bIsTracked != 0)
			{
				if(flip)
					return bodyFrame.bodyData[index].normalRotation;
				else
					return bodyFrame.bodyData[index].mirroredRotation;
			}
		}
		
		return Quaternion.identity;
	}
	
	public KinectInterop.TrackingState GetJointTrackingState(Int64 userId, int joint)
	{
		if(dictUserIdToIndex.ContainsKey(userId))
		{
			int index = dictUserIdToIndex[userId];
			
			if(index >= 0 && index < sensorData.bodyCount && 
				bodyFrame.bodyData[index].bIsTracked != 0)
			{
				if(joint >= 0 && joint < sensorData.jointCount)
				{
					return  bodyFrame.bodyData[index].joint[joint].trackingState;
				}
			}
		}
		
		return KinectInterop.TrackingState.NotTracked;
	}
	
	public bool IsJointTracked(Int64 userId, int joint)
	{
		if(dictUserIdToIndex.ContainsKey(userId))
		{
			int index = dictUserIdToIndex[userId];
			
			if(index >= 0 && index < sensorData.bodyCount && 
				bodyFrame.bodyData[index].bIsTracked != 0)
			{
				if(joint >= 0 && joint < sensorData.jointCount)
				{
					KinectInterop.JointData jointData = bodyFrame.bodyData[index].joint[joint];
					
					return ignoreInferredJoints ? (jointData.trackingState == KinectInterop.TrackingState.Tracked) : 
						(jointData.trackingState != KinectInterop.TrackingState.NotTracked);
				}
			}
		}
		
		return false;
	}
	
	public Vector3 GetJointKinectPosition(Int64 userId, int joint)
	{
		if(dictUserIdToIndex.ContainsKey(userId))
		{
			int index = dictUserIdToIndex[userId];
			
			if(index >= 0 && index < sensorData.bodyCount && 
			   bodyFrame.bodyData[index].bIsTracked != 0)
			{
				if(joint >= 0 && joint < sensorData.jointCount)
				{
					KinectInterop.JointData jointData = bodyFrame.bodyData[index].joint[joint];
					return jointData.kinectPos;
				}
			}
		}
		
		return Vector3.zero;
	}
	
	public Vector3 GetJointPosition(Int64 userId, int joint)
	{
		if(dictUserIdToIndex.ContainsKey(userId))
		{
			int index = dictUserIdToIndex[userId];
			
			if(index >= 0 && index < sensorData.bodyCount && 
				bodyFrame.bodyData[index].bIsTracked != 0)
			{
				if(joint >= 0 && joint < sensorData.jointCount)
				{
					KinectInterop.JointData jointData = bodyFrame.bodyData[index].joint[joint];
					return jointData.position;
				}
			}
		}
		
		return Vector3.zero;
	}
	
	public Vector3 GetJointDirection(Int64 userId, int joint, bool flipX, bool flipZ)
	{
		if(dictUserIdToIndex.ContainsKey(userId))
		{
			int index = dictUserIdToIndex[userId];
			
			if(index >= 0 && index < sensorData.bodyCount && 
				bodyFrame.bodyData[index].bIsTracked != 0)
			{
				if(joint >= 0 && joint < sensorData.jointCount)
				{
					KinectInterop.JointData jointData = bodyFrame.bodyData[index].joint[joint];
					Vector3 jointDir = jointData.direction;

					if(flipX)
						jointDir.x = -jointDir.x;
					
					if(flipZ)
						jointDir.z = -jointDir.z;
					
					return jointDir;
				}
			}
		}
		
		return Vector3.zero;
	}
	
	public Vector3 GetDirectionBetweenJoints(Int64 userId, int firstJoint, int secondJoint, bool flipX, bool flipZ)
	{
		if(dictUserIdToIndex.ContainsKey(userId))
		{
			int index = dictUserIdToIndex[userId];
			
			if(index >= 0 && index < sensorData.bodyCount && 
				bodyFrame.bodyData[index].bIsTracked != 0)
			{
				KinectInterop.BodyData bodyData = bodyFrame.bodyData[index];
				
				if(firstJoint >= 0 && firstJoint < sensorData.jointCount &&
					secondJoint >= 0 && secondJoint < sensorData.jointCount)
				{
					Vector3 firstJointPos = bodyData.joint[firstJoint].position;
					Vector3 secondJointPos = bodyData.joint[secondJoint].position;
					Vector3 jointDir = secondJointPos - firstJointPos;

					if(flipX)
						jointDir.x = -jointDir.x;
					
					if(flipZ)
						jointDir.z = -jointDir.z;
					
					return jointDir;
				}
			}
		}
		
		return Vector3.zero;
	}
	
	public Quaternion GetJointOrientation(Int64 userId, int joint, bool flip)
	{
		if(dictUserIdToIndex.ContainsKey(userId))
		{
			int index = dictUserIdToIndex[userId];
			
			if(index >= 0 && index < sensorData.bodyCount && 
			   bodyFrame.bodyData[index].bIsTracked != 0)
			{
				if(flip)
					return bodyFrame.bodyData[index].joint[joint].normalRotation;
				else
					return bodyFrame.bodyData[index].joint[joint].mirroredRotation;
			}
		}
		
		return Quaternion.identity;
	}

	public Vector3 GetJointPosDepthOverlay(Int64 userId, int joint, Camera camera, Rect imageRect)
	{
		if(dictUserIdToIndex.ContainsKey(userId) && camera != null)
		{
			int index = dictUserIdToIndex[userId];
			
			if(index >= 0 && index < sensorData.bodyCount && 
			   bodyFrame.bodyData[index].bIsTracked != 0)
			{
				if(joint >= 0 && joint < sensorData.jointCount)
				{
					KinectInterop.JointData jointData = bodyFrame.bodyData[index].joint[joint];
					Vector3 posJointRaw = jointData.kinectPos;
					
					if(posJointRaw != Vector3.zero)
					{
						Vector2 posDepth = MapSpacePointToDepthCoords(posJointRaw);

						if(posDepth != Vector2.zero && sensorData != null)
						{
							if(!float.IsInfinity(posDepth.x) && !float.IsInfinity(posDepth.y))
							{
								float xScaled = (float)posDepth.x * imageRect.width / sensorData.depthImageWidth;
								float yScaled = (float)posDepth.y * imageRect.height / sensorData.depthImageHeight;

								float xScreen = imageRect.x + xScaled;
								//float yScreen = camera.pixelHeight - (imageRect.y + yScaled);
								float yScreen = imageRect.y + imageRect.height - yScaled;
								
								Plane cameraPlane = new Plane(camera.transform.forward, camera.transform.position);
								float zDistance = cameraPlane.GetDistanceToPoint(posJointRaw);

								Vector3 vPosJoint = camera.ScreenToWorldPoint(new Vector3(xScreen, yScreen, zDistance));
								
								return vPosJoint;
							}
						}
					}
				}
			}
		}
		
		return Vector3.zero;
	}

	public Vector3 GetJointPosColorOverlay(Int64 userId, int joint, Camera camera, Rect imageRect)
	{
		if(dictUserIdToIndex.ContainsKey(userId) && camera != null)
		{
			int index = dictUserIdToIndex[userId];
			
			if(index >= 0 && index < sensorData.bodyCount && 
			   bodyFrame.bodyData[index].bIsTracked != 0)
			{
				if(joint >= 0 && joint < sensorData.jointCount)
				{
					KinectInterop.JointData jointData = bodyFrame.bodyData[index].joint[joint];
					Vector3 posJointRaw = jointData.kinectPos;
					
					if(posJointRaw != Vector3.zero)
					{
						Vector2 posDepth = MapSpacePointToDepthCoords(posJointRaw);
						ushort depthValue = GetDepthForPixel((int)posDepth.x, (int)posDepth.y);
						
						if(posDepth != Vector2.zero && depthValue > 0 && sensorData != null)
						{
							Vector2 posColor = MapDepthPointToColorCoords(posDepth, depthValue);

							if(!float.IsInfinity(posColor.x) && !float.IsInfinity(posColor.y))
							{
								float xScaled = (float)posColor.x * imageRect.width / sensorData.colorImageWidth;
								float yScaled = (float)posColor.y * imageRect.height / sensorData.colorImageHeight;
								
								float xScreen = imageRect.x + xScaled;
								//float yScreen = camera.pixelHeight - (imageRect.y + yScaled);
								float yScreen = imageRect.y + imageRect.height - yScaled;

								Plane cameraPlane = new Plane(camera.transform.forward, camera.transform.position);
								float zDistance = cameraPlane.GetDistanceToPoint(posJointRaw);
								//float zDistance = (jointData.kinectPos - camera.transform.position).magnitude;

								//Vector3 vPosJoint = camera.ViewportToWorldPoint(new Vector3(xNorm, yNorm, zDistance));
								Vector3 vPosJoint = camera.ScreenToWorldPoint(new Vector3(xScreen, yScreen, zDistance));

								return vPosJoint;
							}
						}
					}
				}
			}
		}

		return Vector3.zero;
	}
	
	public Vector2 GetJointDepthMapPos(Int64 userId, int joint)
	{
		Vector2 posDepth = Vector2.zero;

		Vector3 posJointRaw = GetJointKinectPosition(userId, joint);
		if(posJointRaw != Vector3.zero)
		{
			posDepth = MapSpacePointToDepthCoords(posJointRaw);

			if(posDepth != Vector2.zero)
			{
				float xScaled = (float)posDepth.x / GetDepthImageWidth();
				float yScaled = (float)posDepth.y / GetDepthImageHeight();
				
				posDepth = new Vector2(xScaled, 1f - yScaled);
			}
		}
		
		return posDepth;
	}
	
	public Vector2 GetJointColorMapPos(Int64 userId, int joint)
	{
		Vector2 posColor = Vector2.zero;

		Vector3 posJointRaw = GetJointKinectPosition(userId, joint);
		if(posJointRaw != Vector3.zero)
		{
			Vector2 posDepth = MapSpacePointToDepthCoords(posJointRaw);
			ushort depthValue = GetDepthForPixel((int)posDepth.x, (int)posDepth.y);
			
			if(posDepth != Vector2.zero && depthValue > 0)
			{
				posColor = MapDepthPointToColorCoords(posDepth, depthValue);
				
				if(!float.IsInfinity(posColor.x) && !float.IsInfinity(posColor.y))
				{
					float xScaled = (float)posColor.x / GetColorImageWidth();
					float yScaled = (float)posColor.y / GetColorImageHeight();
					
					posColor = new Vector2(xScaled, 1f - yScaled);
				}
				else
				{
					posColor = Vector2.zero;
				}
			}
		}
		
		return posColor;
	}
	
	public bool IsUserTurnedAround(Int64 userId)
	{
		if(dictUserIdToIndex.ContainsKey(userId))
		{
			int index = dictUserIdToIndex[userId];
			
			if(index >= 0 && index < sensorData.bodyCount && 
			   bodyFrame.bodyData[index].bIsTracked != 0)
			{
				return bodyFrame.bodyData[index].isTurnedAround;
			}
		}
		
		return false;
	}
	
	public bool IsLeftHandConfidenceHigh(Int64 userId)
	{
		if(dictUserIdToIndex.ContainsKey(userId))
		{
			int index = dictUserIdToIndex[userId];
			
			if(index >= 0 && index < sensorData.bodyCount && 
				bodyFrame.bodyData[index].bIsTracked != 0)
			{
				return (bodyFrame.bodyData[index].leftHandConfidence == KinectInterop.TrackingConfidence.High);
			}
		}
		
		return false;
	}
	
	public bool IsRightHandConfidenceHigh(Int64 userId)
	{
		if(dictUserIdToIndex.ContainsKey(userId))
		{
			int index = dictUserIdToIndex[userId];
			
			if(index >= 0 && index < sensorData.bodyCount && 
				bodyFrame.bodyData[index].bIsTracked != 0)
			{
				return (bodyFrame.bodyData[index].rightHandConfidence == KinectInterop.TrackingConfidence.High);
			}
		}
		
		return false;
	}
	
	public KinectInterop.HandState GetLeftHandState(Int64 userId)
	{
		if(dictUserIdToIndex.ContainsKey(userId))
		{
			int index = dictUserIdToIndex[userId];
			
			if(index >= 0 && index < sensorData.bodyCount && 
				bodyFrame.bodyData[index].bIsTracked != 0)
			{
				return bodyFrame.bodyData[index].leftHandState;
			}
		}
		
		return KinectInterop.HandState.NotTracked;
	}
	
	public KinectInterop.HandState GetRightHandState(Int64 userId)
	{
		if(dictUserIdToIndex.ContainsKey(userId))
		{
			int index = dictUserIdToIndex[userId];
			
			if(index >= 0 && index < sensorData.bodyCount && 
				bodyFrame.bodyData[index].bIsTracked != 0)
			{
				return bodyFrame.bodyData[index].rightHandState;
			}
		}
		
		return KinectInterop.HandState.NotTracked;
	}
	
	public bool GetLeftHandInteractionBox(Int64 userId, ref Vector3 leftBotBack, ref Vector3 rightTopFront, bool bValidBox)
	{
		if(dictUserIdToIndex.ContainsKey(userId))
		{
			int index = dictUserIdToIndex[userId];
			
			if(index >= 0 && index < sensorData.bodyCount && 
				bodyFrame.bodyData[index].bIsTracked != 0)
			{
				KinectInterop.BodyData bodyData = bodyFrame.bodyData[index];
				bool bResult = true;
				
				if(bodyData.joint[(int)KinectInterop.JointType.ShoulderRight].trackingState != KinectInterop.TrackingState.NotTracked &&
				   bodyData.joint[(int)KinectInterop.JointType.HipLeft].trackingState != KinectInterop.TrackingState.NotTracked)
				{
					rightTopFront.x = bodyData.joint[(int)KinectInterop.JointType.ShoulderRight].position.x;
					leftBotBack.x = rightTopFront.x - 2 * (rightTopFront.x - bodyData.joint[(int)KinectInterop.JointType.HipLeft].position.x);
				}
				else
				{
					bResult = bValidBox;
				}
					
				if(bodyData.joint[(int)KinectInterop.JointType.HipRight].trackingState != KinectInterop.TrackingState.NotTracked &&
				   bodyData.joint[(int)KinectInterop.JointType.ShoulderRight].trackingState != KinectInterop.TrackingState.NotTracked)
				{
					leftBotBack.y = bodyData.joint[(int)KinectInterop.JointType.HipRight].position.y;
					rightTopFront.y = bodyData.joint[(int)KinectInterop.JointType.ShoulderRight].position.y;
					
					float fDelta = (rightTopFront.y - leftBotBack.y) * 0.35f; // * 2 / 3;
					leftBotBack.y += fDelta;
					rightTopFront.y += fDelta;
				}
				else
				{
					bResult = bValidBox;
				}
					
				if(bodyData.joint[(int)KinectInterop.JointType.SpineBase].trackingState != KinectInterop.TrackingState.NotTracked)
				{
					//leftBotBack.z = bodyData.joint[(int)KinectInterop.JointType.SpineBase].position.z;
					leftBotBack.z = !ignoreZCoordinates ? bodyData.joint[(int)KinectInterop.JointType.SpineBase].position.z :
						(bodyData.joint[(int)KinectInterop.JointType.HandLeft].position.z + 0.1f);
					rightTopFront.z = leftBotBack.z - 0.5f;
				}
				else
				{
					bResult = bValidBox;
				}
				
				return bResult;
			}
		}
		
		return false;
	}
	
	public bool GetRightHandInteractionBox(Int64 userId, ref Vector3 leftBotBack, ref Vector3 rightTopFront, bool bValidBox)
	{
		if(dictUserIdToIndex.ContainsKey(userId))
		{
			int index = dictUserIdToIndex[userId];
			
			if(index >= 0 && index < sensorData.bodyCount && 
				bodyFrame.bodyData[index].bIsTracked != 0)
			{
				KinectInterop.BodyData bodyData = bodyFrame.bodyData[index];
				bool bResult = true;
				
				if(bodyData.joint[(int)KinectInterop.JointType.ShoulderLeft].trackingState != KinectInterop.TrackingState.NotTracked &&
				   bodyData.joint[(int)KinectInterop.JointType.HipRight].trackingState != KinectInterop.TrackingState.NotTracked)
				{
					leftBotBack.x = bodyData.joint[(int)KinectInterop.JointType.ShoulderLeft].position.x;
					rightTopFront.x = leftBotBack.x + 2 * (bodyData.joint[(int)KinectInterop.JointType.HipRight].position.x - leftBotBack.x);
				}
				else
				{
					bResult = bValidBox;
				}
					
				if(bodyData.joint[(int)KinectInterop.JointType.HipLeft].trackingState != KinectInterop.TrackingState.NotTracked &&
				   bodyData.joint[(int)KinectInterop.JointType.ShoulderLeft].trackingState != KinectInterop.TrackingState.NotTracked)
				{
					leftBotBack.y = bodyData.joint[(int)KinectInterop.JointType.HipLeft].position.y;
					rightTopFront.y = bodyData.joint[(int)KinectInterop.JointType.ShoulderLeft].position.y;
					
					float fDelta = (rightTopFront.y - leftBotBack.y) * 0.35f; // * 2 / 3;
					leftBotBack.y += fDelta;
					rightTopFront.y += fDelta;
				}
				else
				{
					bResult = bValidBox;
				}
					
				if(bodyData.joint[(int)KinectInterop.JointType.SpineBase].trackingState != KinectInterop.TrackingState.NotTracked)
				{
					//leftBotBack.z = bodyData.joint[(int)KinectInterop.JointType.SpineBase].position.z;
					leftBotBack.z = !ignoreZCoordinates ? bodyData.joint[(int)KinectInterop.JointType.SpineBase].position.z :
						(bodyData.joint[(int)KinectInterop.JointType.HandRight].position.z + 0.1f);
					rightTopFront.z = leftBotBack.z - 0.5f;
				}
				else
				{
					bResult = bValidBox;
				}
				
				return bResult;
			}
		}
		
		return false;
	}
	
	public ushort GetDepthForPixel(int x, int y)
	{
		if(sensorData != null && sensorData.depthImage != null)
		{
			int index = y * sensorData.depthImageWidth + x;
			
			if(index >= 0 && index < sensorData.depthImage.Length)
			{
				return sensorData.depthImage[index];
			}
		}

		return 0;
	}

	public ushort GetDepthForIndex(int index)
	{
		if(sensorData != null && sensorData.depthImage != null)
		{
			if(index >= 0 && index < sensorData.depthImage.Length)
			{
				return sensorData.depthImage[index];
			}
		}
		
		return 0;
	}
	
	public Vector3 MapDepthPointToSpaceCoords(Vector2 posPoint, ushort depthValue, bool bWorldCoords)
	{
		Vector3 posKinect = Vector3.zero;
		
		if(kinectInitialized)
		{
			posKinect = KinectInterop.MapDepthPointToSpaceCoords(sensorData, posPoint, depthValue);
			
			if(bWorldCoords)
			{
				posKinect = kinectToWorld.MultiplyPoint3x4(posKinect);
			}
		}
		
		return posKinect;
	}
	
	public bool MapDepthFrameToSpaceCoords(ref Vector3[] avSpaceCoords)
	{
		bool bResult = false;
		
		if(kinectInitialized && sensorData.depthImage != null)
		{
			if(avSpaceCoords == null || avSpaceCoords.Length == 0)
			{
				avSpaceCoords = new Vector3[sensorData.depthImageWidth * sensorData.depthImageHeight];
			}
			
			bResult = KinectInterop.MapDepthFrameToSpaceCoords(sensorData, ref avSpaceCoords);
		}
		
		return bResult;
	}
	
	public Vector2 MapSpacePointToDepthCoords(Vector3 posPoint)
	{
		Vector2 posDepth = Vector2.zero;
		
		if(kinectInitialized)
		{
			posDepth = KinectInterop.MapSpacePointToDepthCoords(sensorData, posPoint);
		}
		
		return posDepth;
	}
	
	public Vector2 MapDepthPointToColorCoords(Vector2 posPoint, ushort depthValue)
	{
		Vector2 posColor = Vector2.zero;
		
		if(kinectInitialized)
		{
			posColor = KinectInterop.MapDepthPointToColorCoords(sensorData, posPoint, depthValue);
		}
		
		return posColor;
	}

	public bool MapDepthFrameToColorCoords(ref Vector2[] avColorCoords)
	{
		bool bResult = false;
		
		if(kinectInitialized && sensorData.depthImage != null && sensorData.colorImage != null)
		{
			if(avColorCoords == null || avColorCoords.Length == 0)
			{
				avColorCoords = new Vector2[sensorData.depthImageWidth * sensorData.depthImageHeight];
			}
			
			bResult = KinectInterop.MapDepthFrameToColorCoords(sensorData, ref avColorCoords);
		}
		
		return bResult;
	}

	public bool MapColorFrameToDepthCoords(ref Vector2[] avDepthCoords)
	{
		bool bResult = false;
		
		if(kinectInitialized && sensorData.colorImage != null && sensorData.depthImage != null)
		{
			if(avDepthCoords == null || avDepthCoords.Length == 0)
			{
				avDepthCoords = new Vector2[sensorData.colorImageWidth * sensorData.colorImageWidth];
			}
			
			bResult = KinectInterop.MapColorFrameToDepthCoords(sensorData, ref avDepthCoords);
		}
		
		return bResult;
	}

	public Vector2 MapColorPointToDepthCoords(Vector2 colorPos, bool bReadDepthCoordsIfNeeded)
	{
		Vector2 posDepth = Vector2.zero;
		
		if(kinectInitialized && sensorData.colorImage != null && sensorData.depthImage != null)
		{
			posDepth = KinectInterop.MapColorPointToDepthCoords(sensorData, colorPos, bReadDepthCoordsIfNeeded);
		}
		
		return posDepth;
	}
	
	public void ClearKinectUsers()
	{
		if(!kinectInitialized)
			return;

		for(int i = alUserIds.Count - 1; i >= 0; i--)
		{
			Int64 userId = alUserIds[i];
			RemoveUser(userId);
		}
		
		ResetFilters();
	}

	/// <summary>
	/// Resets the Kinect data filters.
	/// </summary>
	public void ResetFilters()
	{
		if(jointPositionFilter != null)
		{
			jointPositionFilter.Reset();
		}
	}
	
	public void DetectGesture(Int64 UserId, KinectGestures.Gestures gesture)
	{
		List<KinectGestures.GestureData> gesturesData = playerGesturesData.ContainsKey(UserId) ? playerGesturesData[UserId] : new List<KinectGestures.GestureData>();
		int index = GetGestureIndex(gesture, ref gesturesData);

		if(index >= 0)
		{
			DeleteGesture(UserId, gesture);
		}
		
		KinectGestures.GestureData gestureData = new KinectGestures.GestureData();
		
		gestureData.userId = UserId;
		gestureData.gesture = gesture;
		gestureData.state = 0;
		gestureData.joint = 0;
		gestureData.progress = 0f;
		gestureData.complete = false;
		gestureData.cancelled = false;
		
		gestureData.checkForGestures = new List<KinectGestures.Gestures>();
		switch(gesture)
		{
			case KinectGestures.Gestures.ZoomIn:
				gestureData.checkForGestures.Add(KinectGestures.Gestures.ZoomOut);
				gestureData.checkForGestures.Add(KinectGestures.Gestures.Wheel);			
				break;
				
			case KinectGestures.Gestures.ZoomOut:
				gestureData.checkForGestures.Add(KinectGestures.Gestures.ZoomIn);
				gestureData.checkForGestures.Add(KinectGestures.Gestures.Wheel);			
				break;
				
			case KinectGestures.Gestures.Wheel:
				gestureData.checkForGestures.Add(KinectGestures.Gestures.ZoomIn);
				gestureData.checkForGestures.Add(KinectGestures.Gestures.ZoomOut);			
				break;
		}

		gesturesData.Add(gestureData);
		playerGesturesData[UserId] = gesturesData;
		
		if(!gesturesTrackingAtTime.ContainsKey(UserId))
		{
			gesturesTrackingAtTime[UserId] = 0f;
		}
	}
	
	public bool ResetGesture(Int64 UserId, KinectGestures.Gestures gesture)
	{
		List<KinectGestures.GestureData> gesturesData = playerGesturesData.ContainsKey(UserId) ? playerGesturesData[UserId] : null;
		int index = gesturesData != null ? GetGestureIndex(gesture, ref gesturesData) : -1;
		if(index < 0)
			return false;
		
		KinectGestures.GestureData gestureData = gesturesData[index];
		
		gestureData.state = 0;
		gestureData.joint = 0;
		gestureData.progress = 0f;
		gestureData.complete = false;
		gestureData.cancelled = false;
		gestureData.startTrackingAtTime = Time.realtimeSinceStartup + KinectInterop.Constants.MinTimeBetweenSameGestures;

		gesturesData[index] = gestureData;
		playerGesturesData[UserId] = gesturesData;

		return true;
	}
	
	public void ResetPlayerGestures(Int64 UserId)
	{
		List<KinectGestures.GestureData> gesturesData = playerGesturesData.ContainsKey(UserId) ? playerGesturesData[UserId] : null;

		if(gesturesData != null)
		{
			int listSize = gesturesData.Count;
			
			for(int i = 0; i < listSize; i++)
			{
				ResetGesture(UserId, gesturesData[i].gesture);
			}
		}
	}
	
	public bool DeleteGesture(Int64 UserId, KinectGestures.Gestures gesture)
	{
		List<KinectGestures.GestureData> gesturesData = playerGesturesData.ContainsKey(UserId) ? playerGesturesData[UserId] : null;
		int index = gesturesData != null ? GetGestureIndex(gesture, ref gesturesData) : -1;
		if(index < 0)
			return false;
		
		gesturesData.RemoveAt(index);
		playerGesturesData[UserId] = gesturesData;

		return true;
	}

	public void ClearGestures(Int64 UserId)
	{
		List<KinectGestures.GestureData> gesturesData = playerGesturesData.ContainsKey(UserId) ? playerGesturesData[UserId] : null;

		if(gesturesData != null)
		{
			gesturesData.Clear();
			playerGesturesData[UserId] = gesturesData;
		}
	}
	
	public List<KinectGestures.Gestures> GetGesturesList(Int64 UserId)
	{
		List<KinectGestures.Gestures> list = new List<KinectGestures.Gestures>();
		List<KinectGestures.GestureData> gesturesData = playerGesturesData.ContainsKey(UserId) ? playerGesturesData[UserId] : null;
		
		if(gesturesData != null)
		{
			foreach(KinectGestures.GestureData data in gesturesData)
				list.Add(data.gesture);
		}
		
		return list;
	}
	
	public int GetGesturesCount(Int64 UserId)
	{
		List<KinectGestures.GestureData> gesturesData = playerGesturesData.ContainsKey(UserId) ? playerGesturesData[UserId] : null;

		if(gesturesData != null)
		{
			return gesturesData.Count;
		}

		return 0;
	}
	
	public KinectGestures.Gestures GetGestureAtIndex(Int64 UserId, int i)
	{
		List<KinectGestures.GestureData> gesturesData = playerGesturesData.ContainsKey(UserId) ? playerGesturesData[UserId] : null;
		
		if(gesturesData != null)
		{
			if(i >= 0 && i < gesturesData.Count)
			{
				return gesturesData[i].gesture;
			}
		}
		
		return KinectGestures.Gestures.None;
	}
	
	public bool IsTrackingGesture(Int64 UserId, KinectGestures.Gestures gesture)
	{
		List<KinectGestures.GestureData> gesturesData = playerGesturesData.ContainsKey(UserId) ? playerGesturesData[UserId] : null;
		int index = gesturesData != null ? GetGestureIndex(gesture, ref gesturesData) : -1;

		return index >= 0;
	}
	
	public bool IsGestureComplete(Int64 UserId, KinectGestures.Gestures gesture, bool bResetOnComplete)
	{
		List<KinectGestures.GestureData> gesturesData = playerGesturesData.ContainsKey(UserId) ? playerGesturesData[UserId] : null;
		int index = gesturesData != null ? GetGestureIndex(gesture, ref gesturesData) : -1;

		if(index >= 0)
		{
			KinectGestures.GestureData gestureData = gesturesData[index];
			
			if(bResetOnComplete && gestureData.complete)
			{
				ResetPlayerGestures(UserId);
				return true;
			}
			
			return gestureData.complete;
		}
		
		return false;
	}
	
	public bool IsGestureCancelled(Int64 UserId, KinectGestures.Gestures gesture)
	{
		List<KinectGestures.GestureData> gesturesData = playerGesturesData.ContainsKey(UserId) ? playerGesturesData[UserId] : null;
		int index = gesturesData != null ? GetGestureIndex(gesture, ref gesturesData) : -1;

		if(index >= 0)
		{
			KinectGestures.GestureData gestureData = gesturesData[index];
			return gestureData.cancelled;
		}
		
		return false;
	}
	
	public float GetGestureProgress(Int64 UserId, KinectGestures.Gestures gesture)
	{
		List<KinectGestures.GestureData> gesturesData = playerGesturesData.ContainsKey(UserId) ? playerGesturesData[UserId] : null;
		int index = gesturesData != null ? GetGestureIndex(gesture, ref gesturesData) : -1;

		if(index >= 0)
		{
			KinectGestures.GestureData gestureData = gesturesData[index];
			return gestureData.progress;
		}
		
		return 0f;
	}
	
	public Vector3 GetGestureScreenPos(Int64 UserId, KinectGestures.Gestures gesture)
	{
		List<KinectGestures.GestureData> gesturesData = playerGesturesData.ContainsKey(UserId) ? playerGesturesData[UserId] : null;
		int index = gesturesData != null ? GetGestureIndex(gesture, ref gesturesData) : -1;

		if(index >= 0)
		{
			KinectGestures.GestureData gestureData = gesturesData[index];
			return gestureData.screenPos;
		}
		
		return Vector3.zero;
	}

	public string GetBodyFrameData(ref long liRelTime, ref float fUnityTime)
	{
		return KinectInterop.GetBodyFrameAsCsv(sensorData, ref bodyFrame, ref liRelTime, ref fUnityTime);
	}


	public bool IsPlayModeEnabled()
	{
		if(sensorData != null)
		{
			return sensorData.isPlayModeEnabled;
		}

		return false;
	}


	public void EnablePlayMode(bool bEnabled)
	{
		if(sensorData != null)
		{
			sensorData.isPlayModeEnabled = bEnabled;
			sensorData.playModeData = string.Empty;
		}
	}
	
	public bool SetBodyFrameData(string sLine)
	{
		if(sensorData != null && sensorData.isPlayModeEnabled)
		{
			sensorData.playModeData = sLine;
			return true;
		}

		return false;
	}


	// KinectManager's Internal Methods


	void Awake()
	{
		try
		{
			bool bOnceRestarted = false;
			if(System.IO.File.Exists("KMrestart.txt"))
			{
				bOnceRestarted = true;

				try 
				{
					System.IO.File.Delete("KMrestart.txt");
				} 
				catch(Exception ex)
				{
					Debug.LogError("Error deleting KMrestart.txt");
					Debug.LogError(ex.ToString());
				}
			}

			bool bNeedRestart = false;
			sensorInterfaces = KinectInterop.InitSensorInterfaces(bOnceRestarted, ref bNeedRestart);

			if(bNeedRestart)
			{
				System.IO.File.WriteAllText("KMrestart.txt", "Restarting level...");
				KinectInterop.RestartLevel(gameObject, "KM");
				return;
			}
			else
			{
				KinectInterop.SetGraphicsShaderLevel(SystemInfo.graphicsShaderLevel);

				StartKinect();
			}
		} 
		catch (Exception ex) 
		{
			Debug.LogError(ex.ToString());
			
			if(calibrationText != null)
			{
				calibrationText.GetComponent<GUIText>().text = ex.Message;
			}
		}

	}

	void StartKinect() 
	{
		try
		{
			KinectInterop.FrameSource dwFlags = KinectInterop.FrameSource.TypeBody;

			if(computeUserMap != UserMapType.None)
				dwFlags |= KinectInterop.FrameSource.TypeDepth | KinectInterop.FrameSource.TypeBodyIndex;
			if(computeColorMap)
				dwFlags |= KinectInterop.FrameSource.TypeColor;
			if(computeInfraredMap)
				dwFlags |= KinectInterop.FrameSource.TypeInfrared;
//			if(useAudioSource)
//				dwFlags |= KinectInterop.FrameSource.TypeAudio;

			// open the default sensor
			BackgroundRemovalManager brManager = gameObject.GetComponentInChildren<BackgroundRemovalManager>();
			sensorData = KinectInterop.OpenDefaultSensor(sensorInterfaces, dwFlags, sensorAngle, useMultiSourceReader, computeUserMap, brManager);

			if (sensorData == null)
			{
				if(sensorInterfaces == null || sensorInterfaces.Count == 0)
					throw new Exception("No sensor found. Make sure you have installed the SDK and the sensor is connected.");
				else
					throw new Exception("OpenDefaultSensor failed.");
			}

			sensorData.hintHeightAngle = (autoHeightAngle != AutoHeightAngle.DontUse);

			Quaternion quatTiltAngle = Quaternion.Euler(-sensorAngle, 0.0f, 0.0f);
			kinectToWorld.SetTRS(new Vector3(0.0f, sensorHeight, 0.0f), quatTiltAngle, Vector3.one);
		}
		catch(DllNotFoundException ex)
		{
			string message = ex.Message + " cannot be loaded. Please check the Kinect SDK installation.";
			
			Debug.LogError(message);
			Debug.LogException(ex);
			
			if(calibrationText != null)
			{
				calibrationText.GetComponent<GUIText>().text = message;
			}
			
			return;
		}
		catch(Exception ex)
		{
			string message = ex.Message;

			Debug.LogError(message);
			Debug.LogException(ex);
			
			if(calibrationText != null)
			{
				calibrationText.GetComponent<GUIText>().text = message;
			}
			
			return;
		}

		instance = this;
		
		bodyFrame = new KinectInterop.BodyFrameData(sensorData.bodyCount, KinectInterop.Constants.MaxJointCount); // sensorData.jointCount
		bodyFrame.bTurnAnalisys = allowTurnArounds;

		KinectInterop.SmoothParameters smoothParameters = new KinectInterop.SmoothParameters();
		
		switch(smoothing)
		{
			case Smoothing.Default:
				smoothParameters.smoothing = 0.5f;
				smoothParameters.correction = 0.5f;
				smoothParameters.prediction = 0.5f;
				smoothParameters.jitterRadius = 0.05f;
				smoothParameters.maxDeviationRadius = 0.04f;
				break;
			case Smoothing.Medium:
				smoothParameters.smoothing = 0.5f;
				smoothParameters.correction = 0.1f;
				smoothParameters.prediction = 0.5f;
				smoothParameters.jitterRadius = 0.1f;
				smoothParameters.maxDeviationRadius = 0.1f;
				break;
			case Smoothing.Aggressive:
				smoothParameters.smoothing = 0.7f;
				smoothParameters.correction = 0.3f;
				smoothParameters.prediction = 1.0f;
				smoothParameters.jitterRadius = 1.0f;
				smoothParameters.maxDeviationRadius = 1.0f;
				break;
		}
		
		// init data filters
		jointPositionFilter = new JointPositionsFilter();
		jointPositionFilter.Init(smoothParameters);
		
		// init the bone orientation constraints
		if(useBoneOrientationConstraints)
		{
			boneConstraintsFilter = new BoneOrientationsConstraint();
			boneConstraintsFilter.AddDefaultConstraints();
			boneConstraintsFilter.SetDebugText(calibrationText);
		}

		if(computeUserMap != UserMapType.None && computeUserMap != UserMapType.RawUserDepth)
		{
			usersLblTex = new Texture2D(sensorData.depthImageWidth, sensorData.depthImageHeight, TextureFormat.ARGB32, false);

			usersMapSize = sensorData.depthImageWidth * sensorData.depthImageHeight;
			usersHistogramImage = new Color32[usersMapSize];
			usersPrevState = new ushort[usersMapSize];
	        usersHistogramMap = new float[5001];
		}
		
		if(computeColorMap)
		{
			// Initialize color map related stuff
			//usersClrTex = new Texture2D(sensorData.colorImageWidth, sensorData.colorImageHeight, TextureFormat.RGBA32, false);
			usersClrSize = sensorData.colorImageWidth * sensorData.colorImageHeight;
		}

		if(avatarControllers.Count == 0)
		{
			MonoBehaviour[] monoScripts = FindObjectsOfType(typeof(MonoBehaviour)) as MonoBehaviour[];

			foreach(MonoBehaviour monoScript in monoScripts)
			{
				if(typeof(AvatarController).IsAssignableFrom(monoScript.GetType()) && monoScript.enabled)
				{
					AvatarController avatar = (AvatarController)monoScript;
					avatarControllers.Add(avatar);
				}
			}
		}

		if(gestureManager == null)
		{
			MonoBehaviour[] monoScripts = FindObjectsOfType(typeof(MonoBehaviour)) as MonoBehaviour[];
			
			foreach(MonoBehaviour monoScript in monoScripts)
			{
				if(typeof(KinectGestures).IsAssignableFrom(monoScript.GetType()) && monoScript.enabled)
				{
					gestureManager = (KinectGestures)monoScript;
					break;
				}
			}

		}

		if(gestureListeners.Count == 0)
		{
			MonoBehaviour[] monoScripts = FindObjectsOfType(typeof(MonoBehaviour)) as MonoBehaviour[];
			
			foreach(MonoBehaviour monoScript in monoScripts)
			{
				if(typeof(KinectGestures.GestureListenerInterface).IsAssignableFrom(monoScript.GetType()) &&
				   monoScript.enabled)
				{
					//KinectGestures.GestureListenerInterface gl = (KinectGestures.GestureListenerInterface)monoScript;
					gestureListeners.Add(monoScript);
				}
			}
		}
		
        // Initialize user list to contain all users.
        //alUserIds = new List<Int64>();
        //dictUserIdToIndex = new Dictionary<Int64, int>();

//		// start the background reader
//		kinectReaderThread = new System.Threading.Thread(UpdateKinectStreamsThread);
//		kinectReaderThread.Name = "KinectReaderThread";
//		kinectReaderThread.IsBackground = true;
//		kinectReaderThread.Start();
//		kinectReaderRunning = true;

		kinectInitialized = true;

#if USE_SINGLE_KM_IN_MULTIPLE_SCENES
		DontDestroyOnLoad(gameObject);
#endif
		
		// GUI Text.
		if(calibrationText != null)
		{
			calibrationText.GetComponent<GUIText>().text = "WAITING FOR USERS";
		}
		
		Debug.Log("Waiting for users.");
	}
	
	void OnDestroy() 
	{
		//Debug.Log("KM was destroyed");

		// shut down the Kinect on quitting.
		if(kinectInitialized)
		{
			// stop the background thread
			kinectReaderRunning = false;
			kinectReaderThread = null;

			// close the sensor
			KinectInterop.CloseSensor(sensorData);
			
//			KinectInterop.ShutdownKinectSensor();

			instance = null;
		}
	}

	void OnGUI()
    {
		if(kinectInitialized)
		{
			if(displayUserMap && !sensorData.color2DepthTexture &&
			   (computeUserMap != UserMapType.None && computeUserMap != UserMapType.RawUserDepth))
	        {
				if(usersMapRect.width == 0 || usersMapRect.height == 0)
				{
					Rect cameraRect = Camera.main != null ? Camera.main.pixelRect : new Rect(0, 0, Screen.width, Screen.height);
					
					if(DisplayMapsWidthPercent == 0f)
					{
						DisplayMapsWidthPercent = (sensorData.depthImageWidth / 2) * 100 / cameraRect.width;
					}
					
					float displayMapsWidthPercent = DisplayMapsWidthPercent / 100f;
					float displayMapsHeightPercent = displayMapsWidthPercent * sensorData.depthImageHeight / sensorData.depthImageWidth;
					
					float displayWidth = cameraRect.width * displayMapsWidthPercent;
					float displayHeight = cameraRect.width * displayMapsHeightPercent;
					
					usersMapRect = new Rect(cameraRect.width - displayWidth, cameraRect.height, displayWidth, -displayHeight);
				}

	            GUI.DrawTexture(usersMapRect, usersLblTex);
	        }
			else if(computeColorMap && displayColorMap)
			{
				if(usersClrRect.width == 0 || usersClrRect.height == 0)
				{
					Rect cameraRect = Camera.main != null ? Camera.main.pixelRect : new Rect(0, 0, Screen.width, Screen.height);
					
					if(DisplayMapsWidthPercent == 0f)
					{
						DisplayMapsWidthPercent = (sensorData.depthImageWidth / 2) * 100 / cameraRect.width;
					}
					
					float displayMapsWidthPercent = DisplayMapsWidthPercent / 100f;
					float displayMapsHeightPercent = displayMapsWidthPercent * sensorData.colorImageHeight / sensorData.colorImageWidth;
					
					float displayWidth = cameraRect.width * displayMapsWidthPercent;
					float displayHeight = cameraRect.width * displayMapsHeightPercent;
					
					usersClrRect = new Rect(cameraRect.width - displayWidth, cameraRect.height, displayWidth, -displayHeight);
						
//					if(computeUserMap && displayColorMap)
//					{
//						usersMapRect.x -= cameraRect.width * displayMapsWidthPercent;
//					}
				}

				//GUI.DrawTexture(usersClrRect, usersClrTex);
				GUI.DrawTexture(usersClrRect, sensorData.colorImageTexture);
			}
		}
    }

	private void UpdateKinectStreams()
	{
		if(kinectInitialized)
		{
			bLimitedUsers = showTrackedUsersOnly && 
				(maxTrackedUsers < 6 || minUserDistance > 0.5f || maxUserDistance != 0f || maxLeftRightDistance != 0f);
			KinectInterop.UpdateSensorData(sensorData);
			
			if(useMultiSourceReader)
			{
				KinectInterop.GetMultiSourceFrame(sensorData);
			}
			
			if(computeColorMap)
			{
				if((sensorData.newColorImage = KinectInterop.PollColorFrame(sensorData)))
				{
					//UpdateColorMap();
				}
			}
			
			if(computeUserMap != UserMapType.None)
			{
				sensorData.firstUserIndex = liPrimaryUserId != 0 && dictUserIdToIndex.ContainsKey(liPrimaryUserId) ? 
					dictUserIdToIndex[liPrimaryUserId] : -1;
				
				if((sensorData.newDepthImage = KinectInterop.PollDepthFrame(sensorData, computeUserMap, bLimitedUsers, dictUserIdToIndex.Values)))
				{
					//UpdateUserMap(computeUserMap);
				}
			}
			
			if(computeInfraredMap)
			{
				if((sensorData.newInfraredImage = KinectInterop.PollInfraredFrame(sensorData)))
				{
					//UpdateInfraredMap();
				}
			}
			
			sensorData.newBodyFrame = false;
			if(sensorData == null || !sensorData.isPlayModeEnabled)
			{
				sensorData.newBodyFrame = KinectInterop.PollBodyFrame(sensorData, ref bodyFrame, ref kinectToWorld, ignoreZCoordinates);
			}
			else
			{
				if(sensorData.playModeData.Length != 0)
				{
					sensorData.newBodyFrame = KinectInterop.SetBodyFrameFromCsv(sensorData.playModeData, sensorData, ref bodyFrame, ref kinectToWorld);
					sensorData.playModeData = string.Empty;
				}
			}
			
			if(sensorData.newBodyFrame)
			{
				if(smoothing != Smoothing.None)
				{
					jointPositionFilter.UpdateFilter(ref bodyFrame);
				}
				
			}
			
			if(useMultiSourceReader)
			{
				KinectInterop.FreeMultiSourceFrame(sensorData);
			}
		}
	}

	private void UpdateKinectStreamsThread()
	{
		while(kinectReaderRunning)
		{
			UpdateKinectStreams();
			System.Threading.Thread.Sleep(10);
		}
	}
	
	private void ProcessKinectStreams()
	{
		if(sensorData.colorImageBufferReady)
		{
			KinectInterop.RenderColorTexture(sensorData);
			UpdateColorMap();
		}
		
		bool newDepthImage = sensorData.bodyIndexBufferReady || sensorData.depthImageBufferReady;

		if(sensorData.bodyIndexBufferReady)
		{
			KinectInterop.RenderBodyIndexTexture(sensorData, computeUserMap);
		}

		if(sensorData.depthImageBufferReady)
		{
			KinectInterop.RenderDepthImageTexture(sensorData);
		}

		if(newDepthImage)
		{
			UpdateUserMap(computeUserMap);
		}

		UpdateInfraredMap();

		if(sensorData.bodyFrameReady)
		{
			ProcessBodyFrameData();
			
			// frame is released
			lock(sensorData.bodyFrameLock)
			{
				sensorData.bodyFrameReady = false;
			}
		}
	}
	
	void Update() 
	{
		if(kinectInitialized)
		{
			if(!kinectReaderRunning)
			{
				UpdateKinectStreams();
			}

			ProcessKinectStreams();

			if(!lateUpdateAvatars)
			{
				foreach (AvatarController controller in avatarControllers)
				{
					Int64 userId = controller ? controller.playerId : 0;
					
					if(userId != 0 && dictUserIdToIndex.ContainsKey(userId))
					{
						//Int64 userId = alUserIds[userIndex];
						controller.UpdateAvatar(userId);
					}
				}
			}

			foreach(Int64 userId in alUserIds)
			{
				if(!playerGesturesData.ContainsKey(userId))
					continue;

				CheckForGestures(userId);
				
				List<KinectGestures.GestureData> gesturesData = playerGesturesData[userId];
				int userIndex = GetUserIndexById(userId);
				
				foreach(KinectGestures.GestureData gestureData in gesturesData)
				{
					if(gestureData.complete)
					{
//						if(gestureData.gesture == KinectGestures.Gestures.Click)
//						{
//							if(controlMouseCursor)
//							{
//								MouseControl.MouseClick();
//							}
//						}
				
						foreach(KinectGestures.GestureListenerInterface listener in gestureListeners)
						{
							if(listener != null && listener.GestureCompleted(userId, userIndex, gestureData.gesture, (KinectInterop.JointType)gestureData.joint, gestureData.screenPos))
							{
								ResetPlayerGestures(userId);
							}
						}
					}
					else if(gestureData.cancelled)
					{
						foreach(KinectGestures.GestureListenerInterface listener in gestureListeners)
						{
							if(listener != null && listener.GestureCancelled(userId, userIndex, gestureData.gesture, (KinectInterop.JointType)gestureData.joint))
							{
								ResetGesture(userId, gestureData.gesture);
							}
						}
					}
					else if(gestureData.progress >= 0.1f)
					{
//						if((gestureData.gesture == KinectGestures.Gestures.RightHandCursor || 
//						    gestureData.gesture == KinectGestures.Gestures.LeftHandCursor) && 
//						   gestureData.progress >= 0.5f)
//						{
//							if(handCursor != null)
//							{
//								handCursor.transform.position = Vector3.Lerp(handCursor.transform.position, gestureData.screenPos, 3 * Time.deltaTime);
//							}
//							
//							if(controlMouseCursor)
//							{
//								MouseControl.MouseMove(gestureData.screenPos);
//							}
//						}
						
						foreach(KinectGestures.GestureListenerInterface listener in gestureListeners)
						{
							if(listener != null)
							{
								listener.GestureInProgress(userId, userIndex, gestureData.gesture, gestureData.progress, 
								                           (KinectInterop.JointType)gestureData.joint, gestureData.screenPos);
							}
						}
					}
				}
			}
			
		}
	}

	void LateUpdate()
	{
		if(lateUpdateAvatars)
		{
			foreach (AvatarController controller in avatarControllers)
			{
				//int userIndex = controller ? controller.playerIndex : -1;
				Int64 userId = controller ? controller.playerId : 0;
				
				//if((userIndex >= 0) && (userIndex < alUserIds.Count))
				if(userId != 0 && dictUserIdToIndex.ContainsKey(userId))
				{
					//Int64 userId = alUserIds[userIndex];
					controller.UpdateAvatar(userId);
				}
			}
		}
	}
	
	// Update the color image
	void UpdateColorMap()
	{
		//usersClrTex.LoadRawTextureData(sensorData.colorImage);

		if(sensorData != null && sensorData.sensorInterface != null && sensorData.colorImageTexture != null)
		{
			if(sensorData.sensorInterface.IsFaceTrackingActive() &&
			   sensorData.sensorInterface.IsDrawFaceRect())
			{
				// visualize face tracker (face rectangles)
				//sensorData.sensorInterface.VisualizeFaceTrackerOnColorTex(usersClrTex);
				sensorData.sensorInterface.VisualizeFaceTrackerOnColorTex(sensorData.colorImageTexture);
				sensorData.colorImageTexture.Apply();
			}
		}

		//usersClrTex.Apply();
	}
	
	void UpdateUserMap(UserMapType userMapType)
    {
		if(sensorData != null && sensorData.sensorInterface != null && 
		   !sensorData.sensorInterface.IsBackgroundRemovalActive())
		{
			if(!KinectInterop.IsDirectX11Available())
			{
				if(userMapType != UserMapType.RawUserDepth)
				{
					UpdateUserHistogramImage(userMapType);
					usersLblTex.SetPixels32(usersHistogramImage);
				}
			}
			else
			{
				if(userMapType == UserMapType.CutOutTexture)
				{
					if(!sensorData.color2DepthTexture && sensorData.depth2ColorTexture && 
					   KinectInterop.RenderDepth2ColorTex(sensorData))
					{
						KinectInterop.RenderTex2Tex2D(sensorData.depth2ColorTexture, ref usersLblTex);
					}
					else if(!sensorData.color2DepthTexture)
					{
						KinectInterop.RenderTex2Tex2D(sensorData.bodyIndexTexture, ref usersLblTex);
					}
				}
				else if(userMapType == UserMapType.BodyTexture && sensorData.bodyIndexTexture)
				{
					KinectInterop.RenderTex2Tex2D(sensorData.bodyIndexTexture, ref usersLblTex);
				}
				else if(userMapType == UserMapType.UserTexture && sensorData.depthImageTexture)
				{
					KinectInterop.RenderTex2Tex2D(sensorData.depthImageTexture, ref usersLblTex);
				}
			}
			
			if(userMapType != UserMapType.RawUserDepth)
			{
				// draw skeleton lines
				if(displaySkeletonLines)
				{
					for(int i = 0; i < alUserIds.Count; i++)
					{
						Int64 liUserId = alUserIds[i];
						int index = dictUserIdToIndex[liUserId];
						
						if(index >= 0 && index < sensorData.bodyCount)
						{
							DrawSkeleton(usersLblTex, ref bodyFrame.bodyData[index]);
						}
					}
				}
				
				usersLblTex.Apply();
			}
		}
    }

	void UpdateInfraredMap()
	{
		// does nothing at the moment
	}
	
	// Update the user histogram map
	void UpdateUserHistogramImage(UserMapType userMapType)
	{
		int numOfPoints = 0;
		Array.Clear(usersHistogramMap, 0, usersHistogramMap.Length);
		
		for (int i = 0; i < usersMapSize; i++)
		{
			if (sensorData.bodyIndexImage[i] != 255)
			{
				ushort depth = sensorData.depthImage[i];
				if(depth > 5000)
					depth = 5000;

				usersHistogramMap[depth]++;
				numOfPoints++;
			}
		}
		
		if (numOfPoints > 0)
		{
			for (int i = 1; i < usersHistogramMap.Length; i++)
			{   
				usersHistogramMap[i] += usersHistogramMap[i - 1];
			}
			
			for (int i = 0; i < usersHistogramMap.Length; i++)
			{
				usersHistogramMap[i] = 1.0f - (usersHistogramMap[i] / numOfPoints);
			}
		}

		//List<int> alTrackedIndexes = new List<int>(dictUserIdToIndex.Values);
		byte btSelBI = sensorData.selectedBodyIndex;
		Color32 clrClear = Color.clear;

		string sTrackedIndices = string.Empty;
		foreach(int bodyIndex in dictUserIdToIndex.Values)
		{
			sTrackedIndices += (char)(0x30 + bodyIndex);
		}
		
		for (int i = 0; i < usersMapSize; i++)
		{
			ushort userMap = sensorData.bodyIndexImage[i];
			ushort userDepth = sensorData.depthImage[i];

			if(userDepth > 5000)
				userDepth = 5000;
			
			ushort nowUserPixel = userMap != 255 ? (ushort)((userMap << 13) | userDepth) : userDepth;
			ushort wasUserPixel = usersPrevState[i];
			
			// draw only the changed pixels
			if(nowUserPixel != wasUserPixel)
			{
				usersPrevState[i] = nowUserPixel;

				bool bUserTracked = btSelBI != 255 ? btSelBI == (byte)userMap : 
					//(bLimitedUsers ? alTrackedIndexes.Contains(userMap): userMap != 255);
					(bLimitedUsers ? sTrackedIndices.IndexOf((char)(0x30 + userMap)) >= 0 : userMap != 255);

				if(!bUserTracked)
				{
					usersHistogramImage[i] = clrClear;
				}
				else
				{
					if(userMapType == UserMapType.CutOutTexture && sensorData.colorImage != null)
					{
						Vector2 vColorPos = Vector2.zero;

						if(sensorData.depth2ColorCoords != null)
						{
							vColorPos = sensorData.depth2ColorCoords[i];
						}
						else
						{
							Vector2 vDepthPos = Vector2.zero;
							vDepthPos.x = i % sensorData.depthImageWidth;
							vDepthPos.y = i / sensorData.depthImageWidth;

							vColorPos = KinectInterop.MapDepthPointToColorCoords(sensorData, vDepthPos, userDepth);
						}

						if(!float.IsInfinity(vColorPos.x) && !float.IsInfinity(vColorPos.y))
						{
							int cx = (int)vColorPos.x;
							int cy = (int)vColorPos.y;
							int colorIndex = cx + cy * sensorData.colorImageWidth;

							if(colorIndex >= 0 && colorIndex < usersClrSize)
							{
								int ci = colorIndex << 2;
								Color32 colorPixel = new Color32(sensorData.colorImage[ci], sensorData.colorImage[ci + 1], sensorData.colorImage[ci + 2], 255);
								
								usersHistogramImage[i] = colorPixel;
							}
						}
					}
					else
					{
						// Create a blending color based on the depth histogram
						float histDepth = usersHistogramMap[userDepth];
						Color c = new Color(histDepth, histDepth, histDepth, 0.9f);
						
						switch(userMap % 4)
						{
						case 0:
							usersHistogramImage[i] = Color.red * c;
							break;
						case 1:
							usersHistogramImage[i] = Color.green * c;
							break;
						case 2:
							usersHistogramImage[i] = Color.blue * c;
							break;
						case 3:
							usersHistogramImage[i] = Color.magenta * c;
							break;
						}
					}
				}
				
			}
		}
		
	}
	
	// Processes body frame data
	private void ProcessBodyFrameData()
	{
		List<Int64> addedUsers = new List<Int64>();
		List<int> addedIndexes = new List<int>();

		List<Int64> lostUsers = new List<Int64>();
		lostUsers.AddRange(alUserIds);

		if((autoHeightAngle == AutoHeightAngle.ShowInfoOnly || autoHeightAngle == AutoHeightAngle.AutoUpdateAndShowInfo) && 
		   (sensorData.sensorHgtDetected != 0f || sensorData.sensorRotDetected.eulerAngles.x != 0f) &&
		   calibrationText != null)
		{
			float angle = sensorData.sensorRotDetected.eulerAngles.x;
			angle = angle > 180f ? (angle - 360f) : angle;

			calibrationText.GetComponent<GUIText>().text = string.Format("Sensor Height: {0:F1} m, Angle: {1:F0} deg", sensorData.sensorHgtDetected, -angle);
		}

		if((autoHeightAngle == AutoHeightAngle.AutoUpdate || autoHeightAngle == AutoHeightAngle.AutoUpdateAndShowInfo) && 
		   (sensorData.sensorHgtDetected != 0f || sensorData.sensorRotDetected.eulerAngles.x != 0f))
		{
			float angle = sensorData.sensorRotDetected.eulerAngles.x;
			angle = angle > 180f ? (angle - 360f) : angle;
			sensorAngle = -angle;

			float height = sensorData.sensorHgtDetected > 0f ? sensorData.sensorHgtDetected : sensorHeight;
			sensorHeight = height;

			Quaternion quatTiltAngle = Quaternion.Euler(-sensorAngle, 0.0f, 0.0f);
			kinectToWorld.SetTRS(new Vector3(0.0f, sensorHeight, 0.0f), quatTiltAngle, Vector3.one);
		}
		
		int trackedUsers = 0;
		
		for(int i = 0; i < sensorData.bodyCount; i++)
		{
			KinectInterop.BodyData bodyData = bodyFrame.bodyData[i];
			Int64 userId = bodyData.liTrackingID;
			
			if(bodyData.bIsTracked != 0 && Mathf.Abs(bodyData.position.z) >= minUserDistance &&
			   (maxUserDistance <= 0f || Mathf.Abs(bodyData.position.z) <= maxUserDistance) &&
			   (maxLeftRightDistance <= 0f || Mathf.Abs(bodyData.position.x) <= maxLeftRightDistance) &&
			   (maxTrackedUsers < 0 || trackedUsers < maxTrackedUsers))
			{
				Vector3 bodyPos = bodyData.position;

				if(liPrimaryUserId == 0)
				{
					bool bClosestUser = true;
					int iClosestUserIndex = i;
					
					if(detectClosestUser)
					{
						for(int j = 0; j < sensorData.bodyCount; j++)
						{
							if(j != i)
							{
								KinectInterop.BodyData bodyDataOther = bodyFrame.bodyData[j];
								
								if((bodyDataOther.bIsTracked != 0) && 
									(Mathf.Abs(bodyDataOther.position.z) < Mathf.Abs(bodyPos.z)))
								{
									bClosestUser = false;
									iClosestUserIndex = j;
									break;
								}
							}
						}
					}
					
					if(bClosestUser)
					{
						if(!addedUsers.Contains(userId))
						{
							addedUsers.Add(userId);
							addedIndexes.Add(iClosestUserIndex);
							trackedUsers++;
						}
						
					}
				}
				
				if(!addedUsers.Contains(userId))
				{
					addedUsers.Add(userId);
					addedIndexes.Add(i);
					trackedUsers++;
				}

				// convert Kinect positions to world positions
				bodyFrame.bodyData[i].position = bodyPos;
				//string debugText = String.Empty;

				// process special cases
				ProcessBodySpecialData(ref bodyData);

////// 		turnaround mode start
				// determine if the user is turned around
				//float bodyTurnAngle = 0f;
				//float neckTiltAngle = 0f;

				if(allowTurnArounds && // sensorData.sensorInterface.IsFaceTrackingActive() &&
				   bodyData.joint[(int)KinectInterop.JointType.Neck].trackingState != KinectInterop.TrackingState.NotTracked)
				{
					//bodyTurnAngle = bodyData.bodyTurnAngle > 180f ? bodyData.bodyTurnAngle - 360f : bodyData.bodyTurnAngle;
					//neckTiltAngle = Vector3.Angle(Vector3.up, bodyData.joint[(int)KinectInterop.JointType.Neck].direction.normalized);

					//if(neckTiltAngle < 20f)
					{
						bool bTurnedAround = sensorData.sensorInterface.IsBodyTurned(ref bodyData);
						
						if(bTurnedAround && bodyData.turnAroundFactor < 1f)
						{
							bodyData.turnAroundFactor += 5f * Time.deltaTime;
							if(bodyData.turnAroundFactor > 1f)
								bodyData.turnAroundFactor = 1f;
						}
						else if(!bTurnedAround && bodyData.turnAroundFactor > 0f)
						{
							bodyData.turnAroundFactor -= 5f * Time.deltaTime;
							if(bodyData.turnAroundFactor < 0f)
								bodyData.turnAroundFactor = 0f;
						}

						bodyData.isTurnedAround = (bodyData.turnAroundFactor >= 1f) ? true : (bodyData.turnAroundFactor <= 0f ? false : bodyData.isTurnedAround);
						//bodyData.isTurnedAround = bTurnedAround;  // false;

//						RaiseHandListener handListener = RaiseHandListener.Instance;
//						if(handListener != null)
//						{
//							if(handListener.IsRaiseRightHand())
//							{
//								bodyData.isTurnedAround = true;
//							}
//							if(handListener.IsRaiseLeftHand())
//							{
//								bodyData.isTurnedAround = false;
//							}
//						}
						
						if(bodyData.isTurnedAround)
						{
							// switch left and right joints
							SwitchJointsData(ref bodyData, (int)KinectInterop.JointType.ShoulderLeft, (int)KinectInterop.JointType.ShoulderRight);
							SwitchJointsData(ref bodyData, (int)KinectInterop.JointType.ElbowLeft, (int)KinectInterop.JointType.ElbowRight);
							SwitchJointsData(ref bodyData, (int)KinectInterop.JointType.WristLeft, (int)KinectInterop.JointType.WristRight);
							SwitchJointsData(ref bodyData, (int)KinectInterop.JointType.HandLeft, (int)KinectInterop.JointType.HandRight);
							SwitchJointsData(ref bodyData, (int)KinectInterop.JointType.ThumbLeft, (int)KinectInterop.JointType.ThumbRight);
							SwitchJointsData(ref bodyData, (int)KinectInterop.JointType.HandTipLeft, (int)KinectInterop.JointType.HandTipRight);
							
							SwitchJointsData(ref bodyData, (int)KinectInterop.JointType.HipLeft, (int)KinectInterop.JointType.HipRight);
							SwitchJointsData(ref bodyData, (int)KinectInterop.JointType.KneeLeft, (int)KinectInterop.JointType.KneeRight);
							SwitchJointsData(ref bodyData, (int)KinectInterop.JointType.AnkleLeft, (int)KinectInterop.JointType.AnkleRight);
							SwitchJointsData(ref bodyData, (int)KinectInterop.JointType.FootLeft, (int)KinectInterop.JointType.FootRight);

							// recalculate the bone dirs and special data
							KinectInterop.RecalcBoneDirs(sensorData, ref bodyData);
							//ProcessBodySpecialData(ref bodyData);
						}
					}
				}
				
//				if(allowTurnArounds && calibrationText)
//				{
//					calibrationText.GetComponent<GUIText>().text = string.Format("{0} - BodyAngle: {1:000}", 
//					    (!bodyData.isTurnedAround ? "FACE" : "BACK"), bodyData.bodyTurnAngle);
//				}

////// 		turnaround mode end

				CalculateJointOrients(ref bodyData);

				if(sensorData != null && sensorData.sensorInterface != null)
				{
					// do sensor-specific fixes of joint positions and orientations
					sensorData.sensorInterface.FixJointOrientations(sensorData, ref bodyData);
				}

				// filter orientation constraints
				if(useBoneOrientationConstraints && boneConstraintsFilter != null)
				{
					boneConstraintsFilter.Constrain(ref bodyData);
				}
				
				lostUsers.Remove(userId);
				bodyFrame.bodyData[i] = bodyData;
				dictUserIdToTime[userId] = Time.time;
			}
			else
			{
				bodyFrame.bodyData[i].bIsTracked = 0;
			}
		}
		
		if(lostUsers.Count > 0)
		{
			foreach(Int64 userId in lostUsers)
			{
				if((Time.time - dictUserIdToTime[userId]) > waitTimeBeforeRemove)
				{
					RemoveUser(userId);
				}
			}
			
			lostUsers.Clear();
		}

		// calibrate newly detected users
		if(addedUsers.Count > 0)
		{
			for(int i = 0; i < addedUsers.Count; i++)
			{
				Int64 userId = addedUsers[i];
				int userIndex = addedIndexes[i];

				CalibrateUser(userId, userIndex);
			}
			
			addedUsers.Clear();
			addedIndexes.Clear();
		}
	}

	private void ProcessBodySpecialData(ref KinectInterop.BodyData bodyData)
	{
		if(bodyData.joint[(int)KinectInterop.JointType.HipLeft].trackingState == KinectInterop.TrackingState.NotTracked &&
		   bodyData.joint[(int)KinectInterop.JointType.SpineBase].trackingState != KinectInterop.TrackingState.NotTracked &&
		   bodyData.joint[(int)KinectInterop.JointType.HipRight].trackingState != KinectInterop.TrackingState.NotTracked)
		{
			bodyData.joint[(int)KinectInterop.JointType.HipLeft].trackingState = KinectInterop.TrackingState.Inferred;
			
			bodyData.joint[(int)KinectInterop.JointType.HipLeft].kinectPos = bodyData.joint[(int)KinectInterop.JointType.SpineBase].kinectPos +
				(bodyData.joint[(int)KinectInterop.JointType.SpineBase].kinectPos - bodyData.joint[(int)KinectInterop.JointType.HipRight].kinectPos);
			bodyData.joint[(int)KinectInterop.JointType.HipLeft].position = bodyData.joint[(int)KinectInterop.JointType.SpineBase].position +
				(bodyData.joint[(int)KinectInterop.JointType.SpineBase].position - bodyData.joint[(int)KinectInterop.JointType.HipRight].position);
			bodyData.joint[(int)KinectInterop.JointType.HipLeft].direction = bodyData.joint[(int)KinectInterop.JointType.HipLeft].position -
				bodyData.joint[(int)KinectInterop.JointType.SpineBase].position;
		}
		
		if(bodyData.joint[(int)KinectInterop.JointType.HipRight].trackingState == KinectInterop.TrackingState.NotTracked &&
		   bodyData.joint[(int)KinectInterop.JointType.SpineBase].trackingState != KinectInterop.TrackingState.NotTracked &&
		   bodyData.joint[(int)KinectInterop.JointType.HipLeft].trackingState != KinectInterop.TrackingState.NotTracked)
		{
			bodyData.joint[(int)KinectInterop.JointType.HipRight].trackingState = KinectInterop.TrackingState.Inferred;
			
			bodyData.joint[(int)KinectInterop.JointType.HipRight].kinectPos = bodyData.joint[(int)KinectInterop.JointType.SpineBase].kinectPos +
				(bodyData.joint[(int)KinectInterop.JointType.SpineBase].kinectPos - bodyData.joint[(int)KinectInterop.JointType.HipLeft].kinectPos);
			bodyData.joint[(int)KinectInterop.JointType.HipRight].position = bodyData.joint[(int)KinectInterop.JointType.SpineBase].position +
				(bodyData.joint[(int)KinectInterop.JointType.SpineBase].position - bodyData.joint[(int)KinectInterop.JointType.HipLeft].position);
			bodyData.joint[(int)KinectInterop.JointType.HipRight].direction = bodyData.joint[(int)KinectInterop.JointType.HipRight].position -
				bodyData.joint[(int)KinectInterop.JointType.SpineBase].position;
		}
		
		if((bodyData.joint[(int)KinectInterop.JointType.ShoulderLeft].trackingState == KinectInterop.TrackingState.NotTracked &&
		    bodyData.joint[(int)KinectInterop.JointType.SpineShoulder].trackingState != KinectInterop.TrackingState.NotTracked &&
		    bodyData.joint[(int)KinectInterop.JointType.ShoulderRight].trackingState != KinectInterop.TrackingState.NotTracked))
		{
			bodyData.joint[(int)KinectInterop.JointType.ShoulderLeft].trackingState = KinectInterop.TrackingState.Inferred;
			
			bodyData.joint[(int)KinectInterop.JointType.ShoulderLeft].kinectPos = bodyData.joint[(int)KinectInterop.JointType.SpineShoulder].kinectPos +
				(bodyData.joint[(int)KinectInterop.JointType.SpineShoulder].kinectPos - bodyData.joint[(int)KinectInterop.JointType.ShoulderRight].kinectPos);
			bodyData.joint[(int)KinectInterop.JointType.ShoulderLeft].position = bodyData.joint[(int)KinectInterop.JointType.SpineShoulder].position +
				(bodyData.joint[(int)KinectInterop.JointType.SpineShoulder].position - bodyData.joint[(int)KinectInterop.JointType.ShoulderRight].position);
			bodyData.joint[(int)KinectInterop.JointType.ShoulderLeft].direction = bodyData.joint[(int)KinectInterop.JointType.ShoulderLeft].position -
				bodyData.joint[(int)KinectInterop.JointType.SpineShoulder].position;
		}
		
		if((bodyData.joint[(int)KinectInterop.JointType.ShoulderRight].trackingState == KinectInterop.TrackingState.NotTracked &&
		    bodyData.joint[(int)KinectInterop.JointType.SpineShoulder].trackingState != KinectInterop.TrackingState.NotTracked &&
		    bodyData.joint[(int)KinectInterop.JointType.ShoulderLeft].trackingState != KinectInterop.TrackingState.NotTracked))
		{
			bodyData.joint[(int)KinectInterop.JointType.ShoulderRight].trackingState = KinectInterop.TrackingState.Inferred;
			
			bodyData.joint[(int)KinectInterop.JointType.ShoulderRight].kinectPos = bodyData.joint[(int)KinectInterop.JointType.SpineShoulder].kinectPos +
				(bodyData.joint[(int)KinectInterop.JointType.SpineShoulder].kinectPos - bodyData.joint[(int)KinectInterop.JointType.ShoulderLeft].kinectPos);
			bodyData.joint[(int)KinectInterop.JointType.ShoulderRight].position = bodyData.joint[(int)KinectInterop.JointType.SpineShoulder].position +
				(bodyData.joint[(int)KinectInterop.JointType.SpineShoulder].position - bodyData.joint[(int)KinectInterop.JointType.ShoulderLeft].position);
			bodyData.joint[(int)KinectInterop.JointType.ShoulderRight].direction = bodyData.joint[(int)KinectInterop.JointType.ShoulderRight].position -
				bodyData.joint[(int)KinectInterop.JointType.SpineShoulder].position;
		}
		
		// calculate special directions
		if(bodyData.joint[(int)KinectInterop.JointType.HipLeft].trackingState != KinectInterop.TrackingState.NotTracked &&
		   bodyData.joint[(int)KinectInterop.JointType.HipRight].trackingState != KinectInterop.TrackingState.NotTracked)
		{
			Vector3 posRHip = bodyData.joint[(int)KinectInterop.JointType.HipRight].position;
			Vector3 posLHip = bodyData.joint[(int)KinectInterop.JointType.HipLeft].position;
			
			bodyData.hipsDirection = posRHip - posLHip;
			bodyData.hipsDirection -= Vector3.Project(bodyData.hipsDirection, Vector3.up);
		}
		
		if(bodyData.joint[(int)KinectInterop.JointType.ShoulderLeft].trackingState != KinectInterop.TrackingState.NotTracked &&
		   bodyData.joint[(int)KinectInterop.JointType.ShoulderRight].trackingState != KinectInterop.TrackingState.NotTracked)
		{
			Vector3 posRShoulder = bodyData.joint[(int)KinectInterop.JointType.ShoulderRight].position;
			Vector3 posLShoulder = bodyData.joint[(int)KinectInterop.JointType.ShoulderLeft].position;
			
			bodyData.shouldersDirection = posRShoulder - posLShoulder;
			bodyData.shouldersDirection -= Vector3.Project(bodyData.shouldersDirection, Vector3.up);
			
			Vector3 shouldersDir = bodyData.shouldersDirection;
			shouldersDir.z = -shouldersDir.z;
			
			Quaternion turnRot = Quaternion.FromToRotation(Vector3.right, shouldersDir);
			bodyData.bodyTurnAngle = turnRot.eulerAngles.y;
		}
		
//				if(bodyData.joint[(int)KinectInterop.JointType.ElbowLeft].trackingState != KinectInterop.TrackingState.NotTracked &&
//				   bodyData.joint[(int)KinectInterop.JointType.WristLeft].trackingState != KinectInterop.TrackingState.NotTracked)
//				{
//					Vector3 pos1 = bodyData.joint[(int)KinectInterop.JointType.ElbowLeft].position;
//					Vector3 pos2 = bodyData.joint[(int)KinectInterop.JointType.WristLeft].position;
//					
//					bodyData.leftArmDirection = pos2 - pos1;
//				}

//				if(allowHandRotations && bodyData.leftArmDirection != Vector3.zero &&
//				   bodyData.joint[(int)KinectInterop.JointType.WristLeft].trackingState != KinectInterop.TrackingState.NotTracked &&
//				   bodyData.joint[(int)KinectInterop.JointType.ThumbLeft].trackingState != KinectInterop.TrackingState.NotTracked)
//				{
//					Vector3 pos1 = bodyData.joint[(int)KinectInterop.JointType.WristLeft].position;
//					Vector3 pos2 = bodyData.joint[(int)KinectInterop.JointType.ThumbLeft].position;
//
//					Vector3 armDir = bodyData.leftArmDirection;
//					armDir.z = -armDir.z;
//					
//					bodyData.leftThumbDirection = pos2 - pos1;
//					bodyData.leftThumbDirection.z = -bodyData.leftThumbDirection.z;
//					bodyData.leftThumbDirection -= Vector3.Project(bodyData.leftThumbDirection, armDir);
//					
//					bodyData.leftThumbForward = Quaternion.AngleAxis(bodyData.bodyTurnAngle, Vector3.up) * Vector3.forward;
//					bodyData.leftThumbForward -= Vector3.Project(bodyData.leftThumbForward, armDir);
//
//					if(bodyData.leftThumbForward.sqrMagnitude < 0.01f)
//					{
//						bodyData.leftThumbForward = Vector3.zero;
//					}
//				}
//				else
//				{
//					if(bodyData.leftThumbDirection != Vector3.zero)
//					{
//						bodyData.leftThumbDirection = Vector3.zero;
//						bodyData.leftThumbForward = Vector3.zero;
//					}
//				}

//				if(bodyData.joint[(int)KinectInterop.JointType.ElbowRight].trackingState != KinectInterop.TrackingState.NotTracked &&
//				   bodyData.joint[(int)KinectInterop.JointType.WristRight].trackingState != KinectInterop.TrackingState.NotTracked)
//				{
//					Vector3 pos1 = bodyData.joint[(int)KinectInterop.JointType.ElbowRight].position;
//					Vector3 pos2 = bodyData.joint[(int)KinectInterop.JointType.WristRight].position;
//					
//					bodyData.rightArmDirection = pos2 - pos1;
//				}

//				if(allowHandRotations && bodyData.rightArmDirection != Vector3.zero &&
//				   bodyData.joint[(int)KinectInterop.JointType.WristRight].trackingState != KinectInterop.TrackingState.NotTracked &&
//				   bodyData.joint[(int)KinectInterop.JointType.ThumbRight].trackingState != KinectInterop.TrackingState.NotTracked)
//				{
//					Vector3 pos1 = bodyData.joint[(int)KinectInterop.JointType.WristRight].position;
//					Vector3 pos2 = bodyData.joint[(int)KinectInterop.JointType.ThumbRight].position;
//
//					Vector3 armDir = bodyData.rightArmDirection;
//					armDir.z = -armDir.z;
//					
//					bodyData.rightThumbDirection = pos2 - pos1;
//					bodyData.rightThumbDirection.z = -bodyData.rightThumbDirection.z;
//					bodyData.rightThumbDirection -= Vector3.Project(bodyData.rightThumbDirection, armDir);
//
//					bodyData.rightThumbForward = Quaternion.AngleAxis(bodyData.bodyTurnAngle, Vector3.up) * Vector3.forward;
//					bodyData.rightThumbForward -= Vector3.Project(bodyData.rightThumbForward, armDir);
//
//					if(bodyData.rightThumbForward.sqrMagnitude < 0.01f)
//					{
//						bodyData.rightThumbForward = Vector3.zero;
//					}
//				}
//				else
//				{
//					if(bodyData.rightThumbDirection != Vector3.zero)
//					{
//						bodyData.rightThumbDirection = Vector3.zero;
//						bodyData.rightThumbForward = Vector3.zero;
//					}
//				}
		
		if(bodyData.joint[(int)KinectInterop.JointType.KneeLeft].trackingState != KinectInterop.TrackingState.NotTracked &&
		   bodyData.joint[(int)KinectInterop.JointType.AnkleLeft].trackingState != KinectInterop.TrackingState.NotTracked &&
		   bodyData.joint[(int)KinectInterop.JointType.FootLeft].trackingState != KinectInterop.TrackingState.NotTracked)
		{
			Vector3 vFootProjected = Vector3.Project(bodyData.joint[(int)KinectInterop.JointType.FootLeft].direction, bodyData.joint[(int)KinectInterop.JointType.AnkleLeft].direction);
			
			bodyData.joint[(int)KinectInterop.JointType.AnkleLeft].kinectPos += vFootProjected;
			bodyData.joint[(int)KinectInterop.JointType.AnkleLeft].position += vFootProjected;
			bodyData.joint[(int)KinectInterop.JointType.FootLeft].direction -= vFootProjected;
		}
		
		if(bodyData.joint[(int)KinectInterop.JointType.KneeRight].trackingState != KinectInterop.TrackingState.NotTracked &&
		   bodyData.joint[(int)KinectInterop.JointType.AnkleRight].trackingState != KinectInterop.TrackingState.NotTracked &&
		   bodyData.joint[(int)KinectInterop.JointType.FootRight].trackingState != KinectInterop.TrackingState.NotTracked)
		{
			Vector3 vFootProjected = Vector3.Project(bodyData.joint[(int)KinectInterop.JointType.FootRight].direction, bodyData.joint[(int)KinectInterop.JointType.AnkleRight].direction);
			
			bodyData.joint[(int)KinectInterop.JointType.AnkleRight].kinectPos += vFootProjected;
			bodyData.joint[(int)KinectInterop.JointType.AnkleRight].position += vFootProjected;
			bodyData.joint[(int)KinectInterop.JointType.FootRight].direction -= vFootProjected;
		}
	}
	
	private void SwitchJointsData(ref KinectInterop.BodyData bodyData, int jointL, int jointR)
	{
		KinectInterop.TrackingState trackingStateL = bodyData.joint[jointL].trackingState;
		Vector3 kinectPosL = bodyData.joint[jointL].kinectPos;
		Vector3 positionL = bodyData.joint[jointL].position;

		KinectInterop.TrackingState trackingStateR = bodyData.joint[jointR].trackingState;
		Vector3 kinectPosR = bodyData.joint[jointR].kinectPos;
		Vector3 positionR = bodyData.joint[jointR].position;

		bodyData.joint[jointL].trackingState = trackingStateR;
		bodyData.joint[jointL].kinectPos = kinectPosR; // new Vector3(kinectPosR.x, kinectPosL.y, kinectPosL.z);
		bodyData.joint[jointL].position = positionR; // new Vector3(positionR.x, positionL.y, positionL.z);

		bodyData.joint[jointR].trackingState = trackingStateL;
		bodyData.joint[jointR].kinectPos = kinectPosL; // new Vector3(kinectPosL.x, kinectPosR.y, kinectPosR.z);
		bodyData.joint[jointR].position = positionL; // new Vector3(positionL.x, positionR.y, positionR.z);
	}

	private int GetEmptyUserSlot()
	{
		int uidIndex = -1;

		for(int i = aUserIndexIds.Length - 1; i >= 0; i--)
		{
			if(aUserIndexIds[i] == 0)
			{
				uidIndex = i;
			}
			else if(uidIndex >= 0)
			{
				break;
			}
		}

		return uidIndex;
	}
	
    private void CalibrateUser(Int64 userId, int bodyIndex)
    {
		if(!alUserIds.Contains(userId))
		{
			if(CheckForCalibrationPose(userId, bodyIndex, playerCalibrationPose))
			{
				int uidIndex = GetEmptyUserSlot();

				// check if to add or insert the new id
				bool bInsertId = false;
//				for(int i = 0; i < avatarControllers.Count; i++)
//				{
//					AvatarController avatar = avatarControllers[i];
//					
//					if(avatar && avatar.playerId != 0)
//					{
//						for(int u = 0; u < alUserIds.Count; u++)
//						{
//							if(avatar.playerId == alUserIds[u] && avatar.playerIndex > u)
//							{
//								bInsertId = true;
//								uidIndex = 0;
//								break;
//							}
//						}
//
//						if(bInsertId)
//							break;
//					}
//				}

				Debug.Log("Adding user " + uidIndex + ", ID: " + userId + ", Body: " + bodyIndex);
				dictUserIdToIndex[userId] = bodyIndex;

				if(uidIndex >= 0)
				{
					aUserIndexIds[uidIndex] = userId;
				}

				dictUserIdToTime[userId] = Time.time;
				
				if(!bInsertId)
					alUserIds.Add(userId);
				else
					alUserIds.Insert(uidIndex, userId);

				if(liPrimaryUserId == 0 && aUserIndexIds.Length > 0)
				{
					liPrimaryUserId = aUserIndexIds[0];  // userId
					
					if(liPrimaryUserId != 0)
					{
						if(calibrationText != null && calibrationText.GetComponent<GUIText>().text != "")
						{
							calibrationText.GetComponent<GUIText>().text = "";
						}
					}
				}
				
				for(int i = 0; i < avatarControllers.Count; i++)
				{
					AvatarController avatar = avatarControllers[i];

					//if(avatar && avatar.playerIndex == uidIndex)
					if(avatar && avatar.playerIndex == uidIndex && avatar.playerId == 0)
					{
						avatar.playerId = userId;
						avatar.SuccessfulCalibration(userId);
					}
				}
				
				foreach(KinectGestures.Gestures gesture in playerCommonGestures)
				{
					DetectGesture(userId, gesture);
				}
				
				// notify the gesture listeners about the new user
				foreach(KinectGestures.GestureListenerInterface listener in gestureListeners)
				{
					if(listener != null)
					{
						listener.UserDetected(userId, uidIndex);
					}
				}
				
				ResetFilters();
			}
		}
    }
	
	private void RemoveUser(Int64 userId)
	{
		//int uidIndex = alUserIds.IndexOf(userId);
		int uidIndex = Array.IndexOf(aUserIndexIds, userId);
		Debug.Log("Removing user " + uidIndex + ", ID: " + userId + ", Body: " + dictUserIdToIndex[userId]);
		
		for(int i = 0; i < avatarControllers.Count; i++)
		{
			AvatarController avatar = avatarControllers[i];

			//if(avatar && avatar.playerIndex >= uidIndex && avatar.playerIndex < alUserIds.Count)
			if(avatar && avatar.playerId == userId)
			{
				avatar.ResetToInitialPosition();
				avatar.playerId = 0;
			}
		}

		// notify the gesture listeners about the user loss
		foreach(KinectGestures.GestureListenerInterface listener in gestureListeners)
		{
			if(listener != null)
			{
				listener.UserLost(userId, uidIndex);
			}
		}

		ClearGestures(userId);

		if(playerCalibrationData.ContainsKey(userId))
		{
			playerCalibrationData.Remove(userId);
		}

		List<Int64> alCalDataKeys = new List<Int64>(playerCalibrationData.Keys);

		foreach(Int64 calUserID in alCalDataKeys)
		{
			KinectGestures.GestureData gestureData = playerCalibrationData[calUserID];

			if((gestureData.timestamp + 60f) < Time.realtimeSinceStartup)
			{
				playerCalibrationData.Remove(calUserID);
			}
		}

		alCalDataKeys.Clear();
		
		dictUserIdToIndex.Remove(userId);
		dictUserIdToTime.Remove(userId);
		alUserIds.Remove(userId);

		if(uidIndex >= 0)
		{
			aUserIndexIds[uidIndex] = 0;
		}

		if(liPrimaryUserId == userId)
		{
			if(aUserIndexIds.Length > 0)
			{
				liPrimaryUserId = aUserIndexIds[0];
			}
			else
			{
				liPrimaryUserId = 0;
			}
		}
		
//		for(int i = 0; i < avatarControllers.Count; i++)
//		{
//			AvatarController avatar = avatarControllers[i];
//			
//			if(avatar && avatar.playerIndex >= uidIndex && avatar.playerIndex < alUserIds.Count)
//			{
//				avatar.SuccessfulCalibration(alUserIds[avatar.playerIndex]);
//			}
//		}
		
		if(alUserIds.Count == 0)
		{
			Debug.Log("Waiting for users.");
			
			if(calibrationText != null)
			{
				calibrationText.GetComponent<GUIText>().text = "WAITING FOR USERS";
			}
		}
	}
	
	private void DrawSkeleton(Texture2D aTexture, ref KinectInterop.BodyData bodyData)
	{
		int jointsCount = sensorData.jointCount;
		
		for(int i = 0; i < jointsCount; i++)
		{
			int parent = (int)sensorData.sensorInterface.GetParentJoint((KinectInterop.JointType)i);
			
			if(bodyData.joint[i].trackingState != KinectInterop.TrackingState.NotTracked && 
			   bodyData.joint[parent].trackingState != KinectInterop.TrackingState.NotTracked)
			{
				Vector2 posParent = KinectInterop.MapSpacePointToDepthCoords(sensorData, bodyData.joint[parent].kinectPos);
				Vector2 posJoint = KinectInterop.MapSpacePointToDepthCoords(sensorData, bodyData.joint[i].kinectPos);
				
				if(posParent != Vector2.zero && posJoint != Vector2.zero)
				{
					//Color lineColor = playerJointsTracked[i] && playerJointsTracked[parent] ? Color.red : Color.yellow;
					KinectInterop.DrawLine(aTexture, (int)posParent.x, (int)posParent.y, (int)posJoint.x, (int)posJoint.y, Color.yellow);
				}
			}
		}
		
		//aTexture.Apply();
	}

	private void CalculateJointOrients(ref KinectInterop.BodyData bodyData)
	{
		int jointCount = bodyData.joint.Length;

		for(int j = 0; j < jointCount; j++)
		{
			int joint = j;

			KinectInterop.JointData jointData = bodyData.joint[joint];
			bool bJointValid = ignoreInferredJoints ? jointData.trackingState == KinectInterop.TrackingState.Tracked : jointData.trackingState != KinectInterop.TrackingState.NotTracked;

			if(bJointValid)
			{
				int nextJoint = (int)sensorData.sensorInterface.GetNextJoint((KinectInterop.JointType)joint);
				if(nextJoint != joint && nextJoint >= 0 && nextJoint < sensorData.jointCount)
				{
					KinectInterop.JointData nextJointData = bodyData.joint[nextJoint];
					bool bNextJointValid = ignoreInferredJoints ? nextJointData.trackingState == KinectInterop.TrackingState.Tracked : nextJointData.trackingState != KinectInterop.TrackingState.NotTracked;

					Vector3 baseDir = KinectInterop.JointBaseDir[nextJoint];
					Vector3 jointDir = nextJointData.direction;
					jointDir = new Vector3(jointDir.x, jointDir.y, -jointDir.z).normalized;
					
					Quaternion jointOrientNormal = jointData.normalRotation;
					if(bNextJointValid)
					{
						jointOrientNormal = Quaternion.FromToRotation(baseDir, jointDir);
					}
						
					if((joint == (int)KinectInterop.JointType.ShoulderLeft) ||
					   (joint == (int)KinectInterop.JointType.ShoulderRight))
					{
						float angle = -bodyData.bodyTurnAngle;
						Vector3 axis = jointDir;
						Quaternion armTurnRotation = Quaternion.AngleAxis(angle, axis);
						
						jointData.normalRotation = armTurnRotation * jointOrientNormal;
					}
					else if((joint == (int)KinectInterop.JointType.ElbowLeft) ||
					        (joint == (int)KinectInterop.JointType.WristLeft) 
					        || (joint == (int)KinectInterop.JointType.HandLeft))
					{
//						if(joint == (int)KinectInterop.JointType.WristLeft)
//						{
//							KinectInterop.JointData handData = bodyData.joint[(int)KinectInterop.JointType.HandLeft];
//							KinectInterop.JointData handTipData = bodyData.joint[(int)KinectInterop.JointType.HandTipLeft];
//							
//							if(handData.trackingState != KinectInterop.TrackingState.NotTracked &&
//							   handTipData.trackingState != KinectInterop.TrackingState.NotTracked)
//							{
//								jointDir = handData.direction + handTipData.direction;
//								jointDir = new Vector3(jointDir.x, jointDir.y, -jointDir.z).normalized;
//							}
//						}
						
						KinectInterop.JointData shCenterData = bodyData.joint[(int)KinectInterop.JointType.SpineShoulder];
						if(shCenterData.trackingState != KinectInterop.TrackingState.NotTracked &&
						   jointDir != Vector3.zero && shCenterData.direction != Vector3.zero &&
						   Mathf.Abs(Vector3.Dot(jointDir, shCenterData.direction.normalized)) < 0.5f)
						{
							Vector3 spineDir = shCenterData.direction;
							spineDir = new Vector3(spineDir.x, spineDir.y, -spineDir.z).normalized;
							
							Vector3 fwdDir = Vector3.Cross(-jointDir, spineDir).normalized;
							Vector3 upDir = Vector3.Cross(fwdDir, -jointDir).normalized;
							jointOrientNormal = Quaternion.LookRotation(fwdDir, upDir);
						}
//						else
//						{
//							jointOrientNormal = Quaternion.FromToRotation(baseDir, jointDir);
//						}
						
						bool bRotated = (allowedHandRotations == AllowedRotations.None) &&
										(joint != (int)KinectInterop.JointType.ElbowLeft);  // false;
						if((allowedHandRotations == AllowedRotations.All) && 
						   (sensorData.sensorIntPlatform == KinectInterop.DepthSensorPlatform.KinectSDKv2) 
						   && (joint != (int)KinectInterop.JointType.HandLeft))
						{
//							KinectInterop.JointData handData = bodyData.joint[(int)KinectInterop.JointType.HandLeft];
//							KinectInterop.JointData handTipData = bodyData.joint[(int)KinectInterop.JointType.HandTipLeft];
							KinectInterop.JointData thumbData = bodyData.joint[(int)KinectInterop.JointType.ThumbLeft];

//							if(handData.trackingState != KinectInterop.TrackingState.NotTracked &&
//							   handTipData.trackingState != KinectInterop.TrackingState.NotTracked &&
							if(thumbData.trackingState != KinectInterop.TrackingState.NotTracked)
							{
								Vector3 rightDir = -nextJointData.direction; // -(handData.direction + handTipData.direction);
								rightDir = new Vector3(rightDir.x, rightDir.y, -rightDir.z).normalized;

								Vector3 fwdDir = thumbData.direction;
								fwdDir = new Vector3(fwdDir.x, fwdDir.y, -fwdDir.z).normalized;

								if(rightDir != Vector3.zero && fwdDir != Vector3.zero)
								{
									Vector3 upDir = Vector3.Cross(fwdDir, rightDir).normalized;
									fwdDir = Vector3.Cross(rightDir, upDir).normalized;
									
									jointData.normalRotation = Quaternion.LookRotation(fwdDir, upDir);
									//bRotated = true;

//									// fix invalid wrist rotation
//									KinectInterop.JointData elbowData = bodyData.joint[(int)KinectInterop.JointType.ElbowLeft];
//									if(elbowData.trackingState != KinectInterop.TrackingState.NotTracked)
//									{
//										Quaternion quatLocalRot = Quaternion.Inverse(elbowData.normalRotation) * jointData.normalRotation;
//										float angleY = quatLocalRot.eulerAngles.y;
//										
//										if(angleY >= 90f && angleY < 270f && bodyData.leftHandOrientation != Quaternion.identity)
//										{
//											jointData.normalRotation = bodyData.leftHandOrientation;
//										}
//										
//										bodyData.leftHandOrientation = jointData.normalRotation;
//									}

									//bRotated = true;
								}
							}

							bRotated = true;
						}

						if(!bRotated)
						{
							float angle = -bodyData.bodyTurnAngle;
							Vector3 axis = jointDir;
							Quaternion armTurnRotation = Quaternion.AngleAxis(angle, axis);

							jointData.normalRotation = //(allowedHandRotations != AllowedRotations.None || joint == (int)KinectInterop.JointType.ElbowLeft) ? 
								armTurnRotation * jointOrientNormal; // : armTurnRotation;
						}
					}
					else if((joint == (int)KinectInterop.JointType.ElbowRight) ||
					        (joint == (int)KinectInterop.JointType.WristRight) 
					        || (joint == (int)KinectInterop.JointType.HandRight))
					{
//						if(joint == (int)KinectInterop.JointType.WristRight)
//						{
//							KinectInterop.JointData handData = bodyData.joint[(int)KinectInterop.JointType.HandRight];
//							KinectInterop.JointData handTipData = bodyData.joint[(int)KinectInterop.JointType.HandTipRight];
//
//							if(handData.trackingState != KinectInterop.TrackingState.NotTracked &&
//							   handTipData.trackingState != KinectInterop.TrackingState.NotTracked)
//							{
//								jointDir = handData.direction + handTipData.direction;
//								jointDir = new Vector3(jointDir.x, jointDir.y, -jointDir.z).normalized;
//							}
//						}

						KinectInterop.JointData shCenterData = bodyData.joint[(int)KinectInterop.JointType.SpineShoulder];
						if(shCenterData.trackingState != KinectInterop.TrackingState.NotTracked &&
						   jointDir != Vector3.zero && shCenterData.direction != Vector3.zero &&
						   Mathf.Abs(Vector3.Dot(jointDir, shCenterData.direction.normalized)) < 0.5f)
						{
							Vector3 spineDir = shCenterData.direction;
							spineDir = new Vector3(spineDir.x, spineDir.y, -spineDir.z).normalized;
							
							Vector3 fwdDir = Vector3.Cross(jointDir, spineDir).normalized;
							Vector3 upDir = Vector3.Cross(fwdDir, jointDir).normalized;
							jointOrientNormal = Quaternion.LookRotation(fwdDir, upDir);
						}
//						else
//						{
//							jointOrientNormal = Quaternion.FromToRotation(baseDir, jointDir);
//						}

						bool bRotated = (allowedHandRotations == AllowedRotations.None) &&
										(joint != (int)KinectInterop.JointType.ElbowRight);  // false;
						if((allowedHandRotations == AllowedRotations.All) &&
						   (sensorData.sensorIntPlatform == KinectInterop.DepthSensorPlatform.KinectSDKv2) 
						   && (joint != (int)KinectInterop.JointType.HandRight))
						{
//							KinectInterop.JointData handData = bodyData.joint[(int)KinectInterop.JointType.HandRight];
//							KinectInterop.JointData handTipData = bodyData.joint[(int)KinectInterop.JointType.HandTipRight];
							KinectInterop.JointData thumbData = bodyData.joint[(int)KinectInterop.JointType.ThumbRight];

//							if(handData.trackingState != KinectInterop.TrackingState.NotTracked &&
//							   handTipData.trackingState != KinectInterop.TrackingState.NotTracked &&
							if(thumbData.trackingState != KinectInterop.TrackingState.NotTracked)
							{
								Vector3 rightDir = nextJointData.direction; // handData.direction + handTipData.direction;
								rightDir = new Vector3(rightDir.x, rightDir.y, -rightDir.z).normalized;

								Vector3 fwdDir = thumbData.direction;
								fwdDir = new Vector3(fwdDir.x, fwdDir.y, -fwdDir.z).normalized;

								if(rightDir != Vector3.zero && fwdDir != Vector3.zero)
								{
									Vector3 upDir = Vector3.Cross(fwdDir, rightDir).normalized;
									fwdDir = Vector3.Cross(rightDir, upDir).normalized;
									
									jointData.normalRotation = Quaternion.LookRotation(fwdDir, upDir);
									//bRotated = true;
									
//									// fix invalid wrist rotation
//									KinectInterop.JointData elbowData = bodyData.joint[(int)KinectInterop.JointType.ElbowRight];
//									if(elbowData.trackingState != KinectInterop.TrackingState.NotTracked)
//									{
//										Quaternion quatLocalRot = Quaternion.Inverse(elbowData.normalRotation) * jointData.normalRotation;
//										float angleY = quatLocalRot.eulerAngles.y;
//										
//										if(angleY >= 90f && angleY < 270f && bodyData.rightHandOrientation != Quaternion.identity)
//										{
//											jointData.normalRotation = bodyData.rightHandOrientation;
//										}
//										
//										bodyData.rightHandOrientation = jointData.normalRotation;
//									}

									//bRotated = true;
								}
							}

							bRotated = true;
						}

						if(!bRotated)
						{
							float angle = -bodyData.bodyTurnAngle;
							Vector3 axis = jointDir;
							Quaternion armTurnRotation = Quaternion.AngleAxis(angle, axis);

							jointData.normalRotation = //(allowedHandRotations != AllowedRotations.None || joint == (int)KinectInterop.JointType.ElbowRight) ? 
								armTurnRotation * jointOrientNormal; // : armTurnRotation;
						}
					}
					else
					{
						jointData.normalRotation = jointOrientNormal;
					}
					
					if((joint == (int)KinectInterop.JointType.SpineMid) || 
					   (joint == (int)KinectInterop.JointType.SpineShoulder) || 
					   (joint == (int)KinectInterop.JointType.Neck))
					{
						Vector3 baseDir2 = Vector3.right;
						Vector3 jointDir2 = Vector3.Lerp(bodyData.shouldersDirection, -bodyData.shouldersDirection, bodyData.turnAroundFactor);
						jointDir2.z = -jointDir2.z;
						
						jointData.normalRotation *= Quaternion.FromToRotation(baseDir2, jointDir2);
					}
					else if((joint == (int)KinectInterop.JointType.SpineBase) ||
					   (joint == (int)KinectInterop.JointType.HipLeft) || (joint == (int)KinectInterop.JointType.HipRight) ||
					   (joint == (int)KinectInterop.JointType.KneeLeft) || (joint == (int)KinectInterop.JointType.KneeRight) ||
					   (joint == (int)KinectInterop.JointType.AnkleLeft) || (joint == (int)KinectInterop.JointType.AnkleRight))
					{
						Vector3 baseDir2 = Vector3.right;
						Vector3 jointDir2 = Vector3.Lerp(bodyData.hipsDirection, -bodyData.hipsDirection, bodyData.turnAroundFactor);
						jointDir2.z = -jointDir2.z;
						
						jointData.normalRotation *= Quaternion.FromToRotation(baseDir2, jointDir2);
					}
					
					if(joint == (int)KinectInterop.JointType.Neck && 
					   sensorData != null && sensorData.sensorInterface != null)
					{
						if(sensorData.sensorInterface.IsFaceTrackingActive() && 
						   sensorData.sensorInterface.IsFaceTracked(bodyData.liTrackingID))
						{
							KinectInterop.JointData neckData = bodyData.joint[(int)KinectInterop.JointType.Neck];
							KinectInterop.JointData headData = bodyData.joint[(int)KinectInterop.JointType.Head];

							if(neckData.trackingState == KinectInterop.TrackingState.Tracked &&
							   headData.trackingState == KinectInterop.TrackingState.Tracked)
							{
								Quaternion headRotation = Quaternion.identity;
								if(sensorData.sensorInterface.GetHeadRotation(bodyData.liTrackingID, ref headRotation))
								{
									Vector3 rotAngles = headRotation.eulerAngles;
									rotAngles.x = -rotAngles.x;
									rotAngles.y = -rotAngles.y;
									
									bodyData.headOrientation = bodyData.headOrientation != Quaternion.identity ?
										Quaternion.Slerp(bodyData.headOrientation, Quaternion.Euler(rotAngles), 5f * Time.deltaTime) :
											Quaternion.Euler(rotAngles);
									
									jointData.normalRotation = bodyData.headOrientation;
								}
							}
						}
					}
					
					Vector3 mirroredAngles = jointData.normalRotation.eulerAngles;
					mirroredAngles.y = -mirroredAngles.y;
					mirroredAngles.z = -mirroredAngles.z;
					
					jointData.mirroredRotation = Quaternion.Euler(mirroredAngles);
				}
				else
				{
					// get the orientation of the parent joint
					int prevJoint = (int)sensorData.sensorInterface.GetParentJoint((KinectInterop.JointType)joint);
					if(prevJoint != joint && prevJoint >= 0 && prevJoint < sensorData.jointCount &&
					   joint != (int)KinectInterop.JointType.ThumbLeft && joint != (int)KinectInterop.JointType.ThumbRight)
					{
//						if((allowedHandRotations == AllowedRotations.All) && 
//						   (joint == (int)KinectInterop.JointType.ThumbLeft ||
//						    joint == (int)KinectInterop.JointType.ThumbRight))
//						{
//							Vector3 jointDir = jointData.direction;
//							jointDir = new Vector3(jointDir.x, jointDir.y, -jointDir.z).normalized;
//
//							Vector3 baseDir = KinectInterop.JointBaseDir[joint];
//							jointData.normalRotation = Quaternion.FromToRotation(baseDir, jointDir);
//
//							Vector3 mirroredAngles = jointData.normalRotation.eulerAngles;
//							mirroredAngles.y = -mirroredAngles.y;
//							mirroredAngles.z = -mirroredAngles.z;
//							
//							jointData.mirroredRotation = Quaternion.Euler(mirroredAngles);
//						}
//						else
						{
							jointData.normalRotation = bodyData.joint[prevJoint].normalRotation;
							jointData.mirroredRotation = bodyData.joint[prevJoint].mirroredRotation;
						}
					}
					else
					{
						jointData.normalRotation = Quaternion.identity;
						jointData.mirroredRotation = Quaternion.identity;
					}
				}
			}

			bodyData.joint[joint] = jointData;
			
			if(joint == (int)KinectInterop.JointType.SpineBase)
			{
				bodyData.normalRotation = jointData.normalRotation;
				bodyData.mirroredRotation = jointData.mirroredRotation;
			}
		}
	}

	private void CheckForGestures(Int64 UserId)
	{
		if(!gestureManager || !playerGesturesData.ContainsKey(UserId) || !gesturesTrackingAtTime.ContainsKey(UserId))
			return;
		
		if(Time.realtimeSinceStartup >= gesturesTrackingAtTime[UserId])
		{
			int iAllJointsCount = sensorData.jointCount;
			bool[] playerJointsTracked = new bool[iAllJointsCount];
			Vector3[] playerJointsPos = new Vector3[iAllJointsCount];
			
			int[] aiNeededJointIndexes = gestureManager.GetNeededJointIndexes(instance);
			int iNeededJointsCount = aiNeededJointIndexes.Length;
			
			for(int i = 0; i < iNeededJointsCount; i++)
			{
				int joint = aiNeededJointIndexes[i];
				
				if(joint >= 0 && IsJointTracked(UserId, joint))
				{
					playerJointsTracked[joint] = true;
					playerJointsPos[joint] = GetJointPosition(UserId, joint);
				}
			}
			
			List<KinectGestures.GestureData> gesturesData = playerGesturesData[UserId];
			
			int listGestureSize = gesturesData.Count;
			float timestampNow = Time.realtimeSinceStartup;
			string sDebugGestures = string.Empty;  // "Tracked Gestures:\n";
			
			for(int g = 0; g < listGestureSize; g++)
			{
				KinectGestures.GestureData gestureData = gesturesData[g];
				
				if((timestampNow >= gestureData.startTrackingAtTime) && 
					!IsConflictingGestureInProgress(gestureData, ref gesturesData))
				{
					gestureManager.CheckForGesture(UserId, ref gestureData, Time.realtimeSinceStartup, 
						ref playerJointsPos, ref playerJointsTracked);
					gesturesData[g] = gestureData;

					if(gestureData.complete)
					{
						gesturesTrackingAtTime[UserId] = timestampNow + minTimeBetweenGestures;
					}
					
					if(UserId == liPrimaryUserId)
					{
						sDebugGestures += string.Format("{0} - state: {1}, time: {2:F1}, progress: {3}%\n", 
														gestureData.gesture, gestureData.state, 
						                                gestureData.timestamp,
														(int)(gestureData.progress * 100 + 0.5f));
					}
				}
			}
			
			playerGesturesData[UserId] = gesturesData;
			
			if(gesturesDebugText && (UserId == liPrimaryUserId))
			{
				for(int i = 0; i < iNeededJointsCount; i++)
				{
					int joint = aiNeededJointIndexes[i];

					sDebugGestures += string.Format("\n {0}: {1}", (KinectInterop.JointType)joint,
					                                playerJointsTracked[joint] ? playerJointsPos[joint].ToString() : "");
				}

				gesturesDebugText.GetComponent<GUIText>().text = sDebugGestures;
			}
		}
	}
	
	private bool IsConflictingGestureInProgress(KinectGestures.GestureData gestureData, ref List<KinectGestures.GestureData> gesturesData)
	{
		foreach(KinectGestures.Gestures gesture in gestureData.checkForGestures)
		{
			int index = GetGestureIndex(gesture, ref gesturesData);
			
			if(index >= 0)
			{
				if(gesturesData[index].progress > 0f)
					return true;
			}
		}
		
		return false;
	}
	
	// return the index of gesture in the list, or -1 if not found
	private int GetGestureIndex(KinectGestures.Gestures gesture, ref List<KinectGestures.GestureData> gesturesData)
	{
		int listSize = gesturesData.Count;
	
		for(int i = 0; i < listSize; i++)
		{
			if(gesturesData[i].gesture == gesture)
				return i;
		}
		
		return -1;
	}
	
	// check if the calibration pose is complete for given user
	protected virtual bool CheckForCalibrationPose(Int64 UserId, int bodyIndex, KinectGestures.Gestures calibrationGesture)
	{
		if(calibrationGesture == KinectGestures.Gestures.None)
			return true;
		if(!gestureManager)
			return false;

		KinectGestures.GestureData gestureData = playerCalibrationData.ContainsKey(UserId) ? 
			playerCalibrationData[UserId] : new KinectGestures.GestureData();
		
		if(gestureData.userId != UserId)
		{
			gestureData.userId = UserId;
			gestureData.gesture = calibrationGesture;
			gestureData.state = 0;
			gestureData.timestamp = Time.realtimeSinceStartup;
			gestureData.joint = 0;
			gestureData.progress = 0f;
			gestureData.complete = false;
			gestureData.cancelled = false;
		}
		
		int iAllJointsCount = sensorData.jointCount;
		bool[] playerJointsTracked = new bool[iAllJointsCount];
		Vector3[] playerJointsPos = new Vector3[iAllJointsCount];
		
		int[] aiNeededJointIndexes = gestureManager.GetNeededJointIndexes(instance);
		int iNeededJointsCount = aiNeededJointIndexes.Length;
		
		for(int i = 0; i < iNeededJointsCount; i++)
		{
			int joint = aiNeededJointIndexes[i];
			
			if(joint >= 0)
			{
				KinectInterop.JointData jointData = bodyFrame.bodyData[bodyIndex].joint[joint];
				
				playerJointsTracked[joint] = jointData.trackingState != KinectInterop.TrackingState.NotTracked;
				playerJointsPos[joint] = jointData.kinectPos;
			}
		}
		
		gestureManager.CheckForGesture(UserId, ref gestureData, Time.realtimeSinceStartup, 
			ref playerJointsPos, ref playerJointsTracked);
		playerCalibrationData[UserId] = gestureData;

		if(gestureData.complete)
		{
			gestureData.userId = 0;
			playerCalibrationData[UserId] = gestureData;

			return true;
		}

		return false;
	}
	
}

