using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System;

public class K2SensorChecker : MonoBehaviour 
{

	public GUIText infoText;

	private DepthSensorInterface sensorInterface = null;

	private bool bSensorAvailable = false;

	public bool IsSensorAvailable()
	{
		return bSensorAvailable;
	}


	void Awake()
	{
		try
		{
//			bool bOnceRestarted = false;
//			if(System.IO.File.Exists("SCrestart.txt"))
//			{
//				bOnceRestarted = true;
//				
//				try 
//				{
//					System.IO.File.Delete("SCrestart.txt");
//				} 
//				catch(Exception ex)
//				{
//					Debug.LogError("Error deleting SCrestart.txt");
//					Debug.LogError(ex.ToString());
//				}
//			}

			sensorInterface = new Kinect2Interface();

			bool bNeedRestart = false;
			if(sensorInterface.InitSensorInterface(true, ref bNeedRestart))
			{
				if(bNeedRestart)
				{
					System.IO.File.WriteAllText("SCrestart.txt", "Restarting level...");
					KinectInterop.RestartLevel(gameObject, "SC");
					return;
				}
				else
				{
					bSensorAvailable = sensorInterface.GetSensorsCount() > 0;
					
					if(infoText != null)
					{
						infoText.GetComponent<GUIText>().text = bSensorAvailable ? "Sensor is connected." : "No sensor is connected.";
					}
				}
			}
			else
			{
				sensorInterface.FreeSensorInterface(true);
				sensorInterface = null;
			}

		}
		catch (Exception ex) 
		{
			Debug.LogError(ex.ToString());
			
			if(infoText != null)
			{
				infoText.GetComponent<GUIText>().text = ex.Message;
			}
		}
		
	}
	
}
