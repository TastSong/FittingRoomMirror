using UnityEngine;
using System.Collections;
using System.IO;

public class GetJointPositionDemo : MonoBehaviour 
{
	public KinectInterop.JointType joint = KinectInterop.JointType.HandRight;

	public Vector3 jointPosition;

	public bool isSaving = false;

	public string saveFilePath = "joint_pos.csv";
	
	public float secondsToSave = 0f;

	private float saveStartTime = -1f;


	void Start()
	{
		if(isSaving && File.Exists(saveFilePath))
		{
			File.Delete(saveFilePath);
		}
	}


	void Update () 
	{
		if(isSaving)
		{
			if(!File.Exists(saveFilePath))
			{
				using(StreamWriter writer = File.CreateText(saveFilePath))
				{
					// csv file header
					string sLine = "time,joint,pos_x,pos_y,poz_z";
					writer.WriteLine(sLine);
				}
			}

			// check the start time
			if(saveStartTime < 0f)
			{
				saveStartTime = Time.time;
			}
		}

		// get the joint position
		KinectManager manager = KinectManager.Instance;

		if(manager && manager.IsInitialized())
		{
			if(manager.IsUserDetected())
			{
				long userId = manager.GetPrimaryUserID();

				if(manager.IsJointTracked(userId, (int)joint))
				{
					Vector3 jointPos = manager.GetJointPosition(userId, (int)joint);
					jointPosition = jointPos;

					if(isSaving)
					{
						if((secondsToSave == 0f) || ((Time.time - saveStartTime) <= secondsToSave))
						{
							using(StreamWriter writer = File.AppendText(saveFilePath))
							{
								string sLine = string.Format("{0:F3},{1},{2:F3},{3:F3},{4:F3}", Time.time, ((KinectInterop.JointType)joint).ToString(), jointPos.x, jointPos.y, jointPos.z);
								writer.WriteLine(sLine);
							}
						}
					}
				}
			}
		}

	}

}
