using UnityEngine;

using System;
using System.Collections;

public class PointmanController : MonoBehaviour 
{
	public int playerIndex = 0;

	public bool verticalMovement = true;

	public bool mirroredMovement = false;

	public float moveRate = 1f;

	public Vector3 originPosition;

	public bool invertedZMovement = false;
	
	public GameObject bodyJoint;
	public GameObject skeletonLine;

	private GameObject[] bones;
	private GameObject[] lines;

	private Vector3 initialPosition;
	private Quaternion initialRotation;
	private Vector3 initialPosOffset = Vector3.zero;
	private Int64 initialPosUserID = 0;
	private bool initialPosSetY = true;

	public Transform GetBoneTransform(int index)
	{
		if(bones != null && index >= 0 && index < bones.Length && bones[index] != null)
		{
			return bones[index].transform;
		}

		return null;
	}

	
	void Start () 
	{
		bones = new GameObject[KinectInterop.Constants.MaxJointCount];
		
		lines = new GameObject[bones.Length];
		
		initialPosition = transform.position;
		initialRotation = transform.rotation;

	}
	

	void Update () 
	{
		KinectManager manager = KinectManager.Instance;
		
		Int64 userID = manager ? manager.GetUserIdByIndex(playerIndex) : 0;
		
		if(userID <= 0)
		{
			initialPosUserID = 0;
			initialPosOffset = Vector3.zero;
			initialPosSetY = true;

			if(transform.position != initialPosition)
			{
				transform.position = initialPosition;
			}
			
			if(transform.rotation != initialRotation)
			{
				transform.rotation = initialRotation;
			}

			for(int i = 0; i < bones.Length; i++) 
			{
				if(bones[i] != null)
				{
					bones[i].gameObject.SetActive(false);
					
					bones[i].transform.localPosition = Vector3.zero;
					bones[i].transform.localRotation = Quaternion.identity;
				}

				if(lines[i] != null)
				{
					lines[i].gameObject.SetActive(false);
				}
			}

			return;
		}
		
		Vector3 posPointMan = manager.GetUserPosition(userID);

		Vector3 posPointManWorld = new Vector3(posPointMan.x, posPointMan.y, invertedZMovement ? -posPointMan.z : posPointMan.z) + originPosition;
		Vector3 posPointManHips = new Vector3(posPointMan.x, posPointMan.y, !mirroredMovement ? -posPointMan.z : posPointMan.z) + originPosition;

		if(initialPosUserID != userID)
		{
			initialPosUserID = userID;
			initialPosOffset = posPointManWorld;
		}
		
		if(!verticalMovement && !initialPosSetY)
		{
			float fFootPosY = 0f;
			if(manager.IsJointTracked(userID, (int)KinectInterop.JointType.FootLeft))
			{
				fFootPosY = manager.GetJointPosition(userID, (int)KinectInterop.JointType.FootLeft).y;
			}
			else if(manager.IsJointTracked(userID, (int)KinectInterop.JointType.FootRight))
			{
				fFootPosY = manager.GetJointPosition(userID, (int)KinectInterop.JointType.FootRight).y;
			}
			
			initialPosOffset.y = posPointManWorld.y - (fFootPosY + originPosition.y);
			initialPosSetY = true;
		}

		Vector3 relPosUser = (posPointManWorld - initialPosOffset);

		transform.position = initialPosOffset + 
			(verticalMovement ? relPosUser * moveRate : new Vector3(relPosUser.x, 0, relPosUser.z) * moveRate);

		if(manager.IsJointTracked(userID, (int)KinectInterop.JointType.Head))
		{
			float fHeadPosY = manager.GetJointPosition(userID, (int)KinectInterop.JointType.Head).y + originPosition.y;
			float halfHeight = Mathf.Abs(fHeadPosY - posPointManWorld.y);

		}

		for(int i = 0; i < bones.Length; i++) 
		{
			if(bones[i] == null && bodyJoint != null)
			{
				bones[i] = Instantiate(bodyJoint) as GameObject;
				bones[i].transform.parent = transform;
				bones[i].name = ((KinectInterop.JointType)i).ToString();
			}

			if(bones[i] != null)
			{
				int joint = !mirroredMovement ? i : (int)KinectInterop.GetMirrorJoint((KinectInterop.JointType)i);
				if(joint < 0)
					continue;
				
				if(manager.IsJointTracked(userID, joint))
				{
					bones[i].gameObject.SetActive(true);
					
					Vector3 posJoint = manager.GetJointPosition(userID, joint);
					posJoint.z = !mirroredMovement ? -posJoint.z : posJoint.z;

					posJoint += originPosition;
					posJoint -= posPointManHips;
					
					if(mirroredMovement)
					{
						posJoint.x = -posJoint.x;
						posJoint.z = -posJoint.z;
					}

					Quaternion rotJoint = manager.GetJointOrientation(userID, joint, !mirroredMovement);
					rotJoint = initialRotation * rotJoint;
					
					bones[i].transform.localPosition = posJoint;
					bones[i].transform.rotation = rotJoint;
					
					if(lines[i] == null && i > 0 && skeletonLine != null) 
					{
						lines[i] = Instantiate(skeletonLine) as GameObject;
						lines[i].transform.parent = transform;
						lines[i].name = ((KinectInterop.JointType)i).ToString() + "_Line";
					}

					if(lines[i] != null && i > 0)
					{
						lines[i].gameObject.SetActive(true);

						int jParent = (int)manager.GetParentJoint((KinectInterop.JointType)joint);
						Vector3 posJoint2 = manager.GetJointPosition(userID, jParent);
						posJoint2.z = !mirroredMovement ? -posJoint2.z : posJoint2.z;

						posJoint2 += originPosition;
						posJoint2 -= posPointManHips;
						
						if(mirroredMovement)
						{
							posJoint2.x = -posJoint2.x;
							posJoint2.z = -posJoint2.z;
						}
			
						Vector3 dirFromParent = posJoint - posJoint2;

						lines[i].transform.localPosition = posJoint2 + dirFromParent / 2f;
						lines[i].transform.up = transform.rotation * dirFromParent.normalized;

						Vector3 lineScale = lines[i].transform.localScale;
						lines[i].transform.localScale = new Vector3(lineScale.x, dirFromParent.magnitude / 2f, lineScale.z);
					}

				}
				else
				{
					if(bones[i] != null)
					{
						bones[i].gameObject.SetActive(false);
					}
					
					if(lines[i] != null)
					{
						lines[i].gameObject.SetActive(false);
					}
				}
			}	
		}
	}

}
