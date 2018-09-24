using UnityEngine;
using System.Collections;
using System.IO;

public class KinectRecorderPlayer : MonoBehaviour 
{
	public string filePath = "BodyRecording.txt";

	public GUIText infoText;

	public bool playAtStart = false;

	private static KinectRecorderPlayer instance = null;
	
	private bool isRecording = false;
	private bool isPlaying = false;

	private KinectManager manager = null;

	private long liRelTime = 0;
	private float fStartTime = 0f;
	private float fCurrentTime = 0f;
	private int fCurrentFrame = 0;

	private StreamReader fileReader = null;
	private float fPlayTime = 0f;
	private string sPlayLine = string.Empty;

	public static KinectRecorderPlayer Instance
	{
		get
		{
			return instance;
		}
	}

	
//	public void RecordToggleValueChanged(bool bOn)
//	{
//		if(!isRecording)
//		{
//			StartRecording();
//		}
//		else
//		{
//			StopRecordingOrPlaying();
//		}
//	}
//
//	public void PlayToggleValueChanged(bool bOn)
//	{
//		if(!isPlaying)
//		{
//			StartPlaying();
//		}
//		else
//		{
//			StopRecordingOrPlaying();
//		}
//	}

	public bool StartRecording()
	{
		if(isRecording)
			return false;

		isRecording = true;

		if(isPlaying && isRecording)
		{
			CloseFile();
			isPlaying = false;
			
			Debug.Log("Playing stopped.");
		}
		
		if(filePath.Length == 0)
		{
			isRecording = false;

			Debug.LogError("No file to save.");
			if(infoText != null)
			{
				infoText.GetComponent<GUIText>().text = "No file to save.";
			}
		}
		
		if(isRecording)
		{
			Debug.Log("Recording started.");
			if(infoText != null)
			{
				infoText.GetComponent<GUIText>().text = "Recording... Say 'Stop' to stop the recorder.";
			}
			
			if(filePath.Length > 0 && File.Exists(filePath))
			{
				File.Delete(filePath);
			}
			
			fStartTime = fCurrentTime = Time.time;
			fCurrentFrame = 0;
		}

		return isRecording;
	}

	public bool StartPlaying()
	{
		if(isPlaying)
			return false;

		isPlaying = true;

		if(isRecording && isPlaying)
		{
			isRecording = false;
			Debug.Log("Recording stopped.");
		}
		
		if(filePath.Length == 0 || !File.Exists(filePath))
		{
			isPlaying = false;
			Debug.LogError("No file to play.");

			if(infoText != null)
			{
				infoText.GetComponent<GUIText>().text = "No file to play.";
			}
		}
		
		if(isPlaying)
		{
			Debug.Log("Playing started.");
			if(infoText != null)
			{
				infoText.GetComponent<GUIText>().text = "Playing... Say 'Stop' to stop the player.";
			}

			fStartTime = fCurrentTime = Time.time;
			fCurrentFrame = -1;

			fileReader = new StreamReader(filePath);
			ReadLineFromFile();
			
			if(manager)
			{
				manager.EnablePlayMode(true);
			}
		}

		return isPlaying;
	}


	public void StopRecordingOrPlaying()
	{
		if(isRecording)
		{
			isRecording = false;

			Debug.Log("Recording stopped.");
			if(infoText != null)
			{
				infoText.GetComponent<GUIText>().text = "Recording stopped.";
			}
		}

		if(isPlaying)
		{
			CloseFile();
			isPlaying = false;

			Debug.Log("Playing stopped.");
			if(infoText != null)
			{
				infoText.GetComponent<GUIText>().text = "Playing stopped.";
			}
		}

		if(infoText != null)
		{
			infoText.GetComponent<GUIText>().text = "Say: 'Record' to start the recorder, or 'Play' to start the player.";
		}
	}

	public bool IsRecording()
	{
		return isRecording;
	}

	public bool IsPlaying()
	{
		return isPlaying;
	}
	
	
	void Awake()
	{
		instance = this;
	}

	void Start()
	{
		if(infoText != null)
		{
			infoText.GetComponent<GUIText>().text = "Say: 'Record' to start the recorder, or 'Play' to start the player.";
		}

		if(!manager)
		{
			manager = KinectManager.Instance;
		}
		else
		{
			Debug.Log("KinectManager not found, probably not initialized.");

			if(infoText != null)
			{
				infoText.GetComponent<GUIText>().text = "KinectManager not found, probably not initialized.";
			}
		}
		
		if(playAtStart)
		{
			StartPlaying();
		}
	}

	void Update () 
	{
		if(isRecording)
		{
			if(manager && manager.IsInitialized())
			{
				string sBodyFrame = manager.GetBodyFrameData(ref liRelTime, ref fCurrentTime);

				if(sBodyFrame.Length > 0)
				{
					using(StreamWriter writer = File.AppendText(filePath))
					{
						string sRelTime = string.Format("{0:F3}", (fCurrentTime - fStartTime));
						writer.WriteLine(sRelTime + "|" + sBodyFrame);

						if(infoText != null)
						{
							infoText.GetComponent<GUIText>().text = string.Format("Recording @ {0}s., frame {1}. Say 'Stop' to stop the player.", sRelTime, fCurrentFrame);
						}

						fCurrentFrame++;
					}
				}
			}
		}

		if(isPlaying)
		{
			fCurrentTime = Time.time;
			float fRelTime = fCurrentTime - fStartTime;

			if(sPlayLine != null && fRelTime >= fPlayTime)
			{
				if(manager && sPlayLine.Length > 0)
				{
					manager.SetBodyFrameData(sPlayLine);
				}

				ReadLineFromFile();
			}

			if(sPlayLine == null)
			{
				StopRecordingOrPlaying();
			}
		}
	}

	void OnDestroy()
	{
		CloseFile();
		isRecording = isPlaying = false;
	}

	private bool ReadLineFromFile()
	{
		if(fileReader == null)
			return false;

		sPlayLine = fileReader.ReadLine();
		if(sPlayLine == null)
			return false;

		char[] delimiters = { '|' };
		string[] sLineParts = sPlayLine.Split(delimiters);

		if(sLineParts.Length >= 2)
		{
			float.TryParse(sLineParts[0], out fPlayTime);
			sPlayLine = sLineParts[1];
			fCurrentFrame++;

			if(infoText != null)
			{
				infoText.GetComponent<GUIText>().text = string.Format("Playing @ {0:F3}s., frame {1}. Say 'Stop' to stop the player.", fPlayTime, fCurrentFrame);
			}

			return true;
		}

		return false;
	}

	private void CloseFile()
	{
		if(fileReader != null)
		{
			fileReader.Dispose();
			fileReader = null;
		}

		if(manager)
		{
			manager.EnablePlayMode(false);
		}
	}

}
