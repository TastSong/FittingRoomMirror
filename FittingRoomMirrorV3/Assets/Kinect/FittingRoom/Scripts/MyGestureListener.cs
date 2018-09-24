using UnityEngine;
using System.Collections;
using System;
//using Windows.Kinect;

public class MyGestureListener : MonoBehaviour, KinectGestures.GestureListenerInterface 
{
    public GUIText gestureInfo;

    private static MyGestureListener instance = null;

    private bool progressDisplayed;
    private float progressGestureTime;

    private bool swipeLeft;
    private bool swipeRight;

    public static MyGestureListener Instance
    {
        get
        {
            return instance;
        }
    }


    public bool IsSwipeLeft()
    {
        if (swipeLeft)
        {
            swipeLeft = false;
            return true;
        }

        return false;
    }


    public bool IsSwipeRight()
    {
        if (swipeRight)
        {
            swipeRight = false;
            return true;
        }

        return false;
    }


    public void UserDetected(long userId, int userIndex)
    {
        KinectManager manager = KinectManager.Instance;
        if (!manager || (userId != manager.GetPrimaryUserID()))
            return;

        manager.DetectGesture(userId, KinectGestures.Gestures.SwipeLeft);
        manager.DetectGesture(userId, KinectGestures.Gestures.SwipeRight);

        if (gestureInfo != null)
        {
            gestureInfo.GetComponent<GUIText>().text = "Swipe left, right or up to change the slides.";
        }
    }


    public void UserLost(long userId, int userIndex)
    {
        KinectManager manager = KinectManager.Instance;
        if (!manager || (userId != manager.GetPrimaryUserID()))
            return;

        if (gestureInfo != null)
        {
            gestureInfo.GetComponent<GUIText>().text = string.Empty;
        }
    }


    public void GestureInProgress(long userId, int userIndex, KinectGestures.Gestures gesture,
                                  float progress, KinectInterop.JointType joint, Vector3 screenPos)
    {

    }

    public bool GestureCompleted(long userId, int userIndex, KinectGestures.Gestures gesture,
                                  KinectInterop.JointType joint, Vector3 screenPos)
    {
        KinectManager manager = KinectManager.Instance;
        if (!manager || (userId != manager.GetPrimaryUserID()))
            return false;

        if (gestureInfo != null)
        {
            string sGestureText = gesture + " detected";
            gestureInfo.GetComponent<GUIText>().text = sGestureText;
        }

        if (gesture == KinectGestures.Gestures.SwipeLeft)
            swipeLeft = true;
        else if (gesture == KinectGestures.Gestures.SwipeRight)
            swipeRight = true;

        return true;
    }

    public bool GestureCancelled(long userId, int userIndex, KinectGestures.Gestures gesture,
                                  KinectInterop.JointType joint)
    {
        KinectManager manager = KinectManager.Instance;
        if (!manager || (userId != manager.GetPrimaryUserID()))
            return false;

        if (progressDisplayed)
        {
            progressDisplayed = false;

            if (gestureInfo != null)
            {
                gestureInfo.GetComponent<GUIText>().text = String.Empty;
            }
        }

        return true;
    }


    void Awake()
    {
        instance = this;
    }

    void Update()
    {
        if (progressDisplayed && ((Time.realtimeSinceStartup - progressGestureTime) > 2f))
        {
            progressDisplayed = false;
            gestureInfo.GetComponent<GUIText>().text = String.Empty;

            Debug.Log("Forced progress to end.");
        }
    }
}
