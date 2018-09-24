using UnityEngine;
using System.Collections;

public class LocateAvatarsAndGestureListeners : MonoBehaviour 
{

	void Start () 
	{
		KinectManager manager = KinectManager.Instance;
		
		if(manager)
		{
			manager.avatarControllers.Clear();
			manager.ClearKinectUsers();

			MonoBehaviour[] monoScripts = FindObjectsOfType(typeof(MonoBehaviour)) as MonoBehaviour[];
			
			foreach(MonoBehaviour monoScript in monoScripts)
			{
				if(typeof(AvatarController).IsAssignableFrom(monoScript.GetType()) &&
				   monoScript.enabled)
				{
					AvatarController avatar = (AvatarController)monoScript;
					manager.avatarControllers.Add(avatar);
				}
			}

			manager.gestureManager = null;
			foreach(MonoBehaviour monoScript in monoScripts)
			{
				if(typeof(KinectGestures).IsAssignableFrom(monoScript.GetType()) && 
				   monoScript.enabled)
				{
					manager.gestureManager = (KinectGestures)monoScript;
					break;
				}
			}

			manager.gestureListeners.Clear();

			foreach(MonoBehaviour monoScript in monoScripts)
			{
				if(typeof(KinectGestures.GestureListenerInterface).IsAssignableFrom(monoScript.GetType()) &&
				   monoScript.enabled)
				{
					//KinectGestures.GestureListenerInterface gl = (KinectGestures.GestureListenerInterface)monoScript;
					manager.gestureListeners.Add(monoScript);
				}
			}

		}
	}
	
}
