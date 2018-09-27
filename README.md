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
```C#
public void UserDetected(long userId, int userIndex)

public void GestureInProgress(long userId, int userIndex, KinectGestures.Gestures gesture,
                              float progress, KinectInterop.JointType joint, Vector3 screenPos)
public bool GestureCompleted(long userId, int userIndex, KinectGestures.Gestures gesture,
                              KinectInterop.JointType joint, Vector3 screenPos)
```   
* 实现手势监听
```C#
void Update()
{
    if (!gestureListener)
        return;
    if (gestureListener.IsSwipeLeft())
    {
        selected++;
        selected = (selected % numberOfModels);
        Debug.Log("SwipeLeft.");
    }

    else if (gestureListener.IsSwipeRight())
    {
        selected--;
        selected = (selected % numberOfModels);
        Debug.Log("SwipeRight.");
    }

}
```
* 左右挥手换衣逻辑

## 添加衣服模型方法
1. 对于每个fbx模型，导入模型并在Unity编辑器的Assets-view中选择它。
2. 在“检查器”中选择“钻机”选项卡。将AnimationType设置为'Humanoid'，将AvatarDefinition设置为'从此模型创建'。
3. 按应用按钮。然后按配置按钮检查是否正确分配了所有必需的关节。服装模特通常不使用所有关节，这可能使头像定义无效。在这种情况下，您可以手动分配缺失的关节（以红色显示）。
4. 请记住：模型中的关节位置必须与Kinect关节的结构相匹配。你可以看到它们，例如在KinectOverlayDemo2中。否则，模型可能无法正确覆盖用户的身体。
5. 在FittingRoomDemo / Resources-folder中为您的模型类别（衬衫，裤子，裙子等）创建一个子文件夹。
6. 在模型类别文件夹中为p.1模型中导入的所有导入的后续编号（0000,0001,0002等）创建子文件夹。
7. 将模型移动到这些数字文件夹中，每个文件夹一个模型，以及所需的材料和纹理。将模型的fbx文件重命名为'model.fbx'。
8. 您可以在相应的模型文件夹中以jpeg格式（100 x 143px，24bpp）为每个模型放置预览图像。然后将其重命名为'preview.jpg.bytes'。如果您没有放置预览图像，试衣间演示将在模型选择菜单中显示“无预览”。
9. 打开FittingRoomDemo1场景。
10. 将模型类别的ModelSelector组件添加到KinectController游戏对象。将其“模型类别”设置设置为与上面第5页中创建的子文件夹名称相同。设置'模型数'设置以反映上面第6页中创建的子文件夹数。
11. ModelSelector组件的其他设置必须与演示中的现有ModelSelector类似。即'相对于相机的模型'必须设置为'BackgroundCamera'，'前景相机'必须设置为'MainCamera'，'连续缩放' - 启用。比例因子设置最初可以设置为1，“垂直偏移”设置为0.稍后您可以稍微调整它们以提供最佳的模型到主体叠加。
12.如果希望在模型类别更改后所选模型继续覆盖用户的身体，则启用ModelSelector组件的“保持选定模型”设置。这很有用，如果有几个类别（即ModelSelectors），例如衬衫，裤子，裙子等。在这种情况下，当类别改变并且用户开始选择裤子时，所选衬衫模型仍将覆盖用户的身体，实例。
13. CategorySelector组件为改变模型和类别提供手势控制，并负责为同一用户切换模型类别（例如衬衫，裤子，领带等）。场景中的第一个用户（播放器索引0）已经有一个CategorySelector，因此您无需添加更多内容。
14.如果您计划多用户试衣间，请为每个其他用户添加一个CategorySelector组件。您可能还需要为这些用户将使用的模型类别添加相应的ModelSelector组件。
15.运行场景以确保可以在列表中选择模型并正确覆盖用户的身体。如果需要，可以进行一些实验，以找到提供最佳模型到主体叠加的比例因子和垂直偏移设置的值。
16.如果要关闭场景中的光标交互，请禁用KinectController游戏对象的InteractionManager组件。如果要关闭手势（用于更改模型的滑动和用于更改类别的手动提升），请禁用CategorySelector组件的相应设置。如果要关闭或更改T型姿势校准，请更改KinectManager组件的“播放器校准姿势”设置。
17.您可以使用FittingRoomDemo2场景来使用或试验单个叠加模型。如果需要，调整AvatarScaler的比例因子设置，以微调模型的整个身体，手臂或腿骨的比例。如果希望模型在每个更新上重新缩放，请启用“连续缩放”设置。
18.如果服装/叠加模型使用标准着色器，请将其“渲染模式”设置为“剪切”。
