# FittingRoomMirror

## 环境配置
* [unity2017.1.1](https://unity3d.com/cn/get-unity/download/archive)安装
* 安装[Kinect2驱动](https://www.microsoft.com/en-us/download/details.aspx?id=44561)
* VS2013(用其他版本应该没有问题，个人比较喜欢2013而已)

## 主要代码分析
```C#
private void LoadModel(string modelDir)
{
  string modelPath = "Clothing/" + modelDir + "/model";
  UnityEngine.Object modelPrefab = Resources.Load(modelPath, typeof(GameObject));
  if(modelPrefab == null)
    return;

  if(selModel != null) 
  {
    GameObject.Destroy(selModel);
  }

  selModel = (GameObject)GameObject.Instantiate(modelPrefab);
  selModel.name = "Model" + modelDir;
  selModel.transform.position = Vector3.zero;
  selModel.transform.rotation = Quaternion.Euler(0, 180f, 0);

  AvatarController ac = selModel.AddComponent<AvatarController>();
  ac.posRelativeToCamera = modelRelativeToCamera;
  ac.posRelOverlayColor = (foregroundCamera != null);
  ac.mirroredMovement = true;
  ac.verticalMovement = true;
  ac.smoothFactor = 0f;

  KinectManager km = KinectManager.Instance;
  ac.Awake();

  if(km.IsUserDetected())
  {
    ac.SuccessfulCalibration(km.GetPrimaryUserID());
  }

  km.avatarControllers.Clear(); // = new List<AvatarController>();
  km.avatarControllers.Add(ac);

  AvatarScaler scaler = selModel.AddComponent<AvatarScaler>();
  scaler.mirroredAvatar = true;
  scaler.bodyScaleFactor = bodyScaleFactor;
  scaler.continuousScaling = continuousScaling;
  scaler.foregroundCamera = foregroundCamera;

  scaler.Start();
}
