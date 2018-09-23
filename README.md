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
```
* 完成模型的销毁
* 完成模型的加载
```c#
void MenuWindow(int windowID)
{
  menuWindowRectangle = new Rect(Screen.width - 160, 40, 150, Screen.height - 60);

  if (modelThumbs != null)
  {
    GUI.skin.button.fixedWidth = 120;
    GUI.skin.button.fixedHeight = 163;

    scroll = GUILayout.BeginScrollView(scroll);
    selected = GUILayout.SelectionGrid(selected, modelThumbs, 1);

    if (selected >= 0 && selected < modelNames.Length && prevSelected != selected)
    {
      prevSelected = selected;
      LoadModel(modelNames[selected]);
    }

    GUILayout.EndScrollView();

    GUI.skin.button.fixedWidth = 0;
    GUI.skin.button.fixedHeight = 0;
  }
}
```
* 试衣镜逻辑判断
