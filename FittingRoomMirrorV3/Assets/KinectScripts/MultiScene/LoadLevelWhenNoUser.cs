using UnityEngine;
using System.Collections;

public class LoadLevelWhenNoUser : MonoBehaviour 
{
	public int nextLevel = -1;

	public bool validateKinectManager = true;

	public GUIText debugText;

	private bool levelLoaded = false;


	void Start()
	{
		if(validateKinectManager && debugText != null)
		{
			KinectManager manager = KinectManager.Instance;

			if(manager == null || !manager.IsInitialized())
			{
				debugText.GetComponent<GUIText>().text = "KinectManager is not initialized!";
				levelLoaded = true;
			}
		}
	}

	
	void Update() 
	{
		if(!levelLoaded && nextLevel >= 0)
		{
			KinectManager manager = KinectManager.Instance;
			
			if(manager != null && !manager.IsUserDetected())
			{
				levelLoaded = true;
				Application.LoadLevel(nextLevel);
			}
		}
	}
	
}
