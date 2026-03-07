# BigMap WorldSpace UI 系统

## 📁 文件结构

```
Assets/Scripts/OutStage/BigMap/UI/
├── WorldSpaceUIManager.cs          # 单例 - UI 逻辑控制器
├── WorldSpaceCanvasController.cs   # 非单例 - Canvas 动画控制器
├── Panels/
│   ├── NodeInfoPanel.cs            # 节点信息面板 (IMenuPanel)
│   ├── StoryPanel.cs               # 剧情面板 (IMenuPanel)
│   ├── RewardPanel.cs              # 奖励面板 (IMenuPanel)
│   └── TipPanel.cs                 # 提示面板
```

## 🔧 Unity 场景设置步骤

### 1. 创建 WorldSpace Canvas

1. 在 Hierarchy 中右键 → UI → Canvas
2. 将 Canvas 的 `Render Mode` 设置为 `World Space`
3. 调整位置：放在 BigMapRuntimeRenderer 上方（Z 轴 -1 左右）
4. 添加组件：
   - `CanvasGroup`（用于淡入淡出动画）
   - `WorldSpaceCanvasController`（控制动画）

### 2. 创建 UI Manager GameObject

1. 在 Hierarchy 中创建空 GameObject，命名为 `BigMapWorldSpaceUI`
2. 添加组件：`WorldSpaceUIManager`
3. 将步骤 1 的 Canvas 拖到 `_canvasController` 字段

### 3. 创建面板 Prefab

#### NodeInfoPanel
1. 在 Canvas 下创建 Panel（Image）
2. 添加组件：`NodeInfoPanel`
3. 添加子元素：
   - `TitleText` (TextMeshProUGUI) - 节点名称
   - `DescText` (TextMeshProUGUI) - 节点描述
   - `StatusText` (TextMeshProUGUI) - 节点状态
   - `EnterButton` (Button) - 进入关卡按钮
   - `CloseButton` (Button) - 关闭按钮
4. 将组件引用拖到 `NodeInfoPanel` 对应字段
5. 保存为 Prefab：`Assets/UIPrefab/BigMap/NodeInfoPanel.prefab`

#### StoryPanel
1. 在 Canvas 下创建 Panel（Image）
2. 添加组件：`StoryPanel`
3. 添加子元素：
   - `TitleText` (TextMeshProUGUI) - 剧情标题
   - `ContentText` (TextMeshProUGUI) - 剧情内容
   - `NextButton` (Button) - 下一句按钮
   - `SkipButton` (Button) - 跳过按钮
4. 将组件引用拖到 `StoryPanel` 对应字段
5. 保存为 Prefab：`Assets/UIPrefab/BigMap/StoryPanel.prefab`

#### RewardPanel
1. 在 Canvas 下创建 Panel（Image）
2. 添加组件：`RewardPanel`
3. 添加子元素：
   - `TitleText` (TextMeshProUGUI) - "任务完成！"
   - `RewardText` (TextMeshProUGUI) - 奖励内容
   - `ConfirmButton` (Button) - 确认按钮
4. 将组件引用拖到 `RewardPanel` 对应字段
5. 保存为 Prefab：`Assets/UIPrefab/BigMap/RewardPanel.prefab`

#### TipPanel
1. 在 Canvas 下创建 Panel（Image）
2. 添加组件：`TipPanel`
3. 添加子元素：
   - `TipText` (TextMeshProUGUI) - 提示内容
4. 将组件引用拖到 `TipPanel` 对应字段
5. 保存为 Prefab：`Assets/UIPrefab/BigMap/TipPanel.prefab`

### 4. 配置 WorldSpaceUIManager

1. 选中 `BigMapWorldSpaceUI` GameObject
2. 在 Inspector 中将 4 个面板 Prefab 拖到对应字段：
   - `_nodeInfoPanel`
   - `_storyPanel`
   - `_rewardPanel`
   - `_tipPanel`

## 📖 使用示例

### 显示节点信息面板
```csharp
// 通过事件自动触发（点击节点时）
// 或者手动调用
var nodeData = BigMapRuntimeRenderer.Instance.GetNode(nodeId).NodeData;
WorldSpaceUIManager.Instance.ShowNodeInfo(nodeData);
```

### 显示剧情
```csharp
// 加载 Resources/Story/Story_001.json
WorldSpaceUIManager.Instance.ShowStory("Story_001");

// 设置完成回调
var panel = WorldSpaceUIManager.Instance.GetStoryPanel();
panel.SetOnCompleteCallback(() => {
    Debug.Log("剧情播放完成，进入关卡");
});
```

### 显示奖励
```csharp
MissionReward reward = new MissionReward {
    Money = 1000,
    TechPoints = 50,
    Blueprints = new List<string> { "Tank_A", "Jet_Fighter" }
};
WorldSpaceUIManager.Instance.ShowReward(reward);
```

### 显示提示
```csharp
WorldSpaceUIManager.Instance.ShowTip("节点已解锁！", 2f);
```

## 🎨 配置建议

### Canvas 设置
- **Position**: (0, 0, -1) - 在底图上方
- **Size**: (100, 100) - 根据实际需求调整
- **Sorting Layer**: UI
- **Sorting Order**: 10

### 面板动画
- **Fade Duration**: 0.3 秒
- **Scale Duration**: 0.2 秒
- **Auto Hide Delay**: 3 秒（TipPanel）

## 🐱 注意事项

1. **事件系统**: 确保 `PostSystem` 已初始化
2. **单例顺序**: `WorldSpaceUIManager` 依赖 `BigMapRuntimeRenderer`
3. **资源路径**: 剧情数据放在 `Resources/Story/` 文件夹下
4. **Z 轴层级**:
   - BigMapRuntimeRenderer: Z = -2
   - EdgeRenderer: Z = -1.5
   - WorldSpace UI Canvas: Z = -1

## 🔗 相关系统

- `BigMapRuntimeRenderer` - 底图渲染器
- `NodeController` - 节点控制器
- `GameFlowController` - 游戏流程控制器
- `MissionManager` - 任务管理器
- `StoryPackData` - 剧情数据结构
