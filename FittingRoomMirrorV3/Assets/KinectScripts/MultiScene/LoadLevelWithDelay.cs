using UnityEngine;
using System.Collections;

public class LoadLevelWithDelay : MonoBehaviour 
{
	public float waitSeconds = 0f;

	public int nextLevel = -1;

	public bool validateKinectManager = true;

	public GUIText debugText;

	private float timeToLoadLevel = 0f;
	private bool levelLoaded = false;


	void Start()
	{
		timeToLoadLevel = Time.realtimeSinceStartup + waitSeconds;

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
			if(Time.realtimeSinceStartup >= timeToLoadLevel)
			{
				levelLoaded = true;
				Application.LoadLevel(nextLevel);
			}
			else
			{
				float timeRest = timeToLoadLevel - Time.realtimeSinceStartup;

				if(debugText != null)
				{
					debugText.GetComponent<GUIText>().text = string.Format("Time to the next level: {0:F0} s.", timeRest);
				}
			}
		}
	}
	
}
