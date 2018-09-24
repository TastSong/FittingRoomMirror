using UnityEngine;
using System.Collections;

public class LoadLevelWhenUserDetected : MonoBehaviour 
{
	public KinectGestures.Gestures expectedUserPose = KinectGestures.Gestures.None;
	
	public int nextLevel = -1;

	public bool validateKinectManager = true;

	public GUIText debugText;


	private bool levelLoaded = false;
	private KinectGestures.Gestures savedCalibrationPose;


	void Start()
	{
		KinectManager manager = KinectManager.Instance;
		
		if(validateKinectManager && debugText != null)
		{
			if(manager == null || !manager.IsInitialized())
			{
				debugText.GetComponent<GUIText>().text = "KinectManager is not initialized!";
				levelLoaded = true;
			}
		}

		if(manager != null && manager.IsInitialized())
		{
			savedCalibrationPose = manager.playerCalibrationPose;
			manager.playerCalibrationPose = expectedUserPose;
		}
	}

	
	void Update() 
	{
		if(!levelLoaded && nextLevel >= 0)
		{
			KinectManager manager = KinectManager.Instance;
			
			if(manager != null && manager.IsUserDetected())
			{
				manager.playerCalibrationPose = savedCalibrationPose;

				levelLoaded = true;
				Application.LoadLevel(nextLevel);
			}
		}
	}
	
}
