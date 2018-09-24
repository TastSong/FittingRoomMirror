using UnityEngine;
using System;
using System.Collections;
using System.IO;

public class SpeechManager : MonoBehaviour 
{
	
	public string grammarFileName = "SpeechGrammar.grxml";

	public bool dynamicGrammar = false;
	
	public int languageCode = 1033;

	public float requiredConfidence = 0f;
	
	public GUIText debugText;

	private bool isListening;
	
	private bool isPhraseRecognized;
	private string phraseTagRecognized;
	private float phraseConfidence;
	
	private KinectInterop.SensorData sensorData = null;
	
	private bool sapiInitialized = false;
	
	private static SpeechManager instance;
	
    public static SpeechManager Instance
    {
        get
        {
            return instance;
        }
    }
	
	public bool IsSapiInitialized()
	{
		return sapiInitialized;
	}

	public bool AddGrammarPhrase(string fromRule, string toRule, string phrase, bool bClearRulePhrases, bool bCommitGrammar)
	{
		if(sapiInitialized)
		{
			int hr = sensorData.sensorInterface.AddGrammarPhrase(fromRule, toRule, phrase, bClearRulePhrases, bCommitGrammar);
			return (hr == 0);
		}

		return false;
	}
	
	public bool IsListening()
	{
		return isListening;
	}
	
	public bool IsPhraseRecognized()
	{
		return isPhraseRecognized;
	}

	public float GetPhraseConfidence()
	{
		return phraseConfidence;
	}
	
	public string GetPhraseTagRecognized()
	{
		return phraseTagRecognized;
	}
	
	public void ClearPhraseRecognized()
	{
		isPhraseRecognized = false;
		phraseTagRecognized = String.Empty;
		phraseConfidence = 0f;
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
				throw new Exception("Speech recognition cannot be started, because KinectManager is missing or not initialized.");
			}
			
			if(debugText != null)
			{
				debugText.GetComponent<GUIText>().text = "Please, wait...";
			}
			
			bool bNeedRestart = false;
			if(sensorData.sensorInterface.IsSpeechRecognitionAvailable(ref bNeedRestart))
			{
				if(bNeedRestart)
				{
					KinectInterop.RestartLevel(gameObject, "SM");
					return;
				}
			}
			else
			{
				string sInterfaceName = sensorData.sensorInterface.GetType().Name;
				throw new Exception(sInterfaceName + ": Speech recognition is not supported!");
			}
			
			string sCriteria = String.Format("Language={0:X};Kinect=True", languageCode);
			int rc = sensorData.sensorInterface.InitSpeechRecognition(sCriteria, true, false);
	        if (rc < 0)
	        {
				string sErrorMessage = (new SpeechErrorHandler()).GetSapiErrorMessage(rc);
				throw new Exception(String.Format("Error initializing Kinect/SAPI: " + sErrorMessage));
	        }
			
			if(requiredConfidence > 0)
			{
				sensorData.sensorInterface.SetSpeechConfidence(requiredConfidence);
			}
			
			if(grammarFileName != string.Empty)
			{
				// copy the grammar file from Resources, if available
				//if(!File.Exists(grammarFileName))
				{
					TextAsset textRes = Resources.Load(grammarFileName, typeof(TextAsset)) as TextAsset;
					
					if(textRes != null)
					{
						string sResText = textRes.text;
						File.WriteAllText(grammarFileName, sResText);
					}
					else
					{
						throw new Exception("Couldn't find grammar resource: " + grammarFileName + ".txt");
					}
				}

				// load the grammar file
				rc = sensorData.sensorInterface.LoadSpeechGrammar(grammarFileName, (short)languageCode, dynamicGrammar);
		        if (rc < 0)
		        {
					string sErrorMessage = (new SpeechErrorHandler()).GetSapiErrorMessage(rc);
					throw new Exception("Error loading grammar file " + grammarFileName + ": " + sErrorMessage);
		        }

//				// test dynamic grammar phrases
//				AddGrammarPhrase("addressBook", string.Empty, "Nancy Anderson", true, false);
//				AddGrammarPhrase("addressBook", string.Empty, "Cindy White", false, false);
//				AddGrammarPhrase("addressBook", string.Empty, "Oliver Lee", false, false);
//				AddGrammarPhrase("addressBook", string.Empty, "Alan Brewer", false, false);
//				AddGrammarPhrase("addressBook", string.Empty, "April Reagan", false, true);
			}
			
			instance = this;
			sapiInitialized = true;
			
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
				debugText.GetComponent<GUIText>().text = "Please check the Kinect and SAPI installations.";
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
		if(sapiInitialized && sensorData != null && sensorData.sensorInterface != null)
		{
			// finish speech recognition
			sensorData.sensorInterface.FinishSpeechRecognition();
		}
		
		sapiInitialized = false;
		instance = null;
	}
	
	void Update () 
	{
		// start Kinect speech recognizer as needed
//		if(!sapiInitialized)
//		{
//			StartRecognizer();
//			
//			if(!sapiInitialized)
//			{
//				Application.Quit();
//				return;
//			}
//		}
		
		if(sapiInitialized)
		{
			// update the speech recognizer
			int rc = sensorData.sensorInterface.UpdateSpeechRecognition();
			
			if(rc >= 0)
			{
				if(sensorData.sensorInterface.IsSpeechStarted())
				{
					isListening = true;
				}
				else if(sensorData.sensorInterface.IsSpeechEnded())
				{
					isListening = false;
				}

				if(sensorData.sensorInterface.IsPhraseRecognized())
				{
					isPhraseRecognized = true;
					phraseConfidence = sensorData.sensorInterface.GetPhraseConfidence();
					
					phraseTagRecognized = sensorData.sensorInterface.GetRecognizedPhraseTag();
					sensorData.sensorInterface.ClearRecognizedPhrase();
					
					//Debug.Log(phraseTagRecognized);
				}
			}
		}
	}
	
	void OnGUI()
	{
		if(sapiInitialized)
		{
			if(debugText != null)
			{
				if(isPhraseRecognized)
				{
					debugText.GetComponent<GUIText>().text = string.Format("{0}  ({1:F1}%)", phraseTagRecognized, phraseConfidence * 100f);
				}
				else if(isListening)
				{
					debugText.GetComponent<GUIText>().text = "Listening...";
				}
			}
		}
	}
	
	
}
