# Unity UI Toolkit IStyle API 参考

## 概述

`IStyle` 接口是 Unity UI Toolkit 的核心样式接口，提供对 `VisualElement` 内联样式数据的访问。通过此接口可以读取和设置元素的样式属性，用于控制元素的布局、外观和交互效果。

**重要概念**：
- **内联样式**：通过 `IStyle` 设置的样式优先级最高，会覆盖 USS 样式表中的值
- **计算样式**：要获取元素最终计算的样式值（包含 USS 和继承值），需使用 `IComputedStyle` 接口
- **USS 样式表**：Unity 样式表文件，定义可重用的样式规则

## 属性分类详解

### 1. 布局属性 (Layout)

#### 弹性布局 (Flexbox)
| 属性 | 类型 | 描述 | 常用值 |
|------|------|------|--------|
| `flexDirection` | `StyleEnum<FlexDirection>` | 主轴方向（子元素排列方向） | `Row`, `Column`, `RowReverse`, `ColumnReverse` |
| `flexWrap` | `StyleEnum<Wrap>` | 子元素换行方式 | `NoWrap`, `Wrap`, `WrapReverse` |
| `justifyContent` | `StyleEnum<Justify>` | 主轴对齐方式 | `FlexStart`, `FlexEnd`, `Center`, `SpaceBetween`, `SpaceAround`, `SpaceEvenly` |
| `alignItems` | `StyleEnum<Align>` | 交叉轴对齐方式（单行） | `Auto`, `FlexStart`, `FlexEnd`, `Center`, `Stretch` |
| `alignContent` | `StyleEnum<Align>` | 交叉轴对齐方式（多行） | 同上 |
| `alignSelf` | `StyleEnum<Align>` | 单个元素在交叉轴的对齐方式 | 同上 |

#### 弹性项目 (Flex Item)
| 属性 | 类型 | 描述 | 示例 |
|------|------|------|------|
| `flexGrow` | `StyleFloat` | 放大比例（当有剩余空间时） | `0` (不放大), `1` (等分剩余空间) |
| `flexShrink` | `StyleFloat` | 缩小比例（当空间不足时） | `1` (默认缩小), `0` (不缩小) |
| `flexBasis` | `StyleLength` | 项目在主轴上的初始大小 | `Auto`, `100px`, `50%` |

#### 尺寸与位置
| 属性 | 类型 | 描述 | 示例 |
|------|------|------|------|
| `width` | `StyleLength` | 固定宽度 | `100px`, `50%`, `Auto` |
| `height` | `StyleLength` | 固定高度 | `100px`, `50%`, `Auto` |
| `minWidth` | `StyleLength` | 最小宽度 | `50px`, `30%` |
| `maxWidth` | `StyleLength` | 最大宽度 | `200px`, `80%` |
| `minHeight` | `StyleLength` | 最小高度 | `50px`, `30%` |
| `maxHeight` | `StyleLength` | 最大高度 | `200px`, `80%` |
| `position` | `StyleEnum<Position>` | 定位方式 | `Relative`, `Absolute` |
| `left` | `StyleLength` | 左侧距离（绝对定位） | `10px`, `20%` |
| `top` | `StyleLength` | 顶部距离（绝对定位） | `10px`, `20%` |
| `right` | `StyleLength` | 右侧距离（绝对定位） | `10px`, `20%` |
| `bottom` | `StyleLength` | 底部距离（绝对定位） | `10px`, `20%` |

#### 边距与内边距
| 属性 | 类型 | 描述 | 示例 |
|------|------|------|------|
| `marginTop` | `StyleLength` | 上外边距 | `10px`, `Auto` |
| `marginBottom` | `StyleLength` | 下外边距 | `10px`, `Auto` |
| `marginLeft` | `StyleLength` | 左外边距 | `10px`, `Auto` |
| `marginRight` | `StyleLength` | 右外边距 | `10px`, `Auto` |
| `paddingTop` | `StyleLength` | 上内边距 | `10px` |
| `paddingBottom` | `StyleLength` | 下内边距 | `10px` |
| `paddingLeft` | `StyleLength` | 左内边距 | `10px` |
| `paddingRight` | `StyleLength` | 右内边距 | `10px` |

### 2. 背景与边框 (Background & Border)

#### 背景样式
| 属性 | 类型 | 描述 | 示例 |
|------|------|------|------|
| `backgroundColor` | `StyleColor` | 背景颜色 | `new Color(0.2f, 0.3f, 0.4f, 1.0f)` |
| `backgroundImage` | `StyleBackground` | 背景图片 | `new Background(someTexture2D)` |
| `backgroundPositionX` | `StyleBackgroundPosition` | 背景图片X位置 | `new BackgroundPosition(BackgroundPositionKeyword.Left)` |
| `backgroundPositionY` | `StyleBackgroundPosition` | 背景图片Y位置 | `new BackgroundPosition(BackgroundPositionKeyword.Top)` |
| `backgroundRepeat` | `StyleBackgroundRepeat` | 背景图片重复方式 | `new BackgroundRepeat(Repeat.NoRepeat)` |
| `backgroundSize` | `StyleBackgroundSize` | 背景图片尺寸 | `new BackgroundSize(BackgroundSizeType.Contain)` |
| `unityBackgroundImageTintColor` | `StyleColor` | 背景图片色调颜色 | `new Color(1.0f, 1.0f, 1.0f, 0.5f)` |

#### 边框样式
| 属性 | 类型 | 描述 | 示例 |
|------|------|------|------|
| `borderTopWidth` | `StyleFloat` | 上边框宽度 | `1.0f`, `2.0f` |
| `borderBottomWidth` | `StyleFloat` | 下边框宽度 | `1.0f`, `2.0f` |
| `borderLeftWidth` | `StyleFloat` | 左边框宽度 | `1.0f`, `2.0f` |
| `borderRightWidth` | `StyleFloat` | 右边框宽度 | `1.0f`, `2.0f` |
| `borderTopColor` | `StyleColor` | 上边框颜色 | `Color.red`, `new Color(1,0,0,1)` |
| `borderBottomColor` | `StyleColor` | 下边框颜色 | `Color.green` |
| `borderLeftColor` | `StyleColor` | 左边框颜色 | `Color.blue` |
| `borderRightColor` | `StyleColor` | 右边框颜色 | `Color.yellow` |
| `borderTopLeftRadius` | `StyleLength` | 左上角圆角半径 | `5px`, `50%` (圆形) |
| `borderTopRightRadius` | `StyleLength` | 右上角圆角半径 | `5px`, `50%` |
| `borderBottomLeftRadius` | `StyleLength` | 左下角圆角半径 | `5px`, `50%` |
| `borderBottomRightRadius` | `StyleLength` | 右下角圆角半径 | `5px`, `50%` |

#### 九宫格切片 (9-Slice)
| 属性 | 类型 | 描述 | 示例 |
|------|------|------|------|
| `unitySliceLeft` | `StyleInt` | 九宫格左边缘尺寸 | `10` |
| `unitySliceRight` | `StyleInt` | 九宫格右边缘尺寸 | `10` |
| `unitySliceTop` | `StyleInt` | 九宫格上边缘尺寸 | `10` |
| `unitySliceBottom` | `StyleInt` | 九宫格下边缘尺寸 | `10` |
| `unitySliceScale` | `StyleFloat` | 九宫格切片缩放 | `1.0f` |

### 3. 文本样式 (Text)

#### 字体与颜色
| 属性 | 类型 | 描述 | 示例 |
|------|------|------|------|
| `color` | `StyleColor` | 文本颜色 | `Color.white`, `new Color(1,1,1,1)` |
| `fontSize` | `StyleLength` | 字体大小 | `14px`, `1.2em` |
| `unityFont` | `StyleFont` | 字体对象（传统方式） | `Resources.Load<Font>("Fonts/Arial")` |
| `unityFontDefinition` | `StyleFontDefinition` | 字体定义（推荐方式） | `FontDefinition.FromSDFFont(...)` |
| `unityFontStyleAndWeight` | `StyleEnum<FontStyle>` | 字体样式和粗细 | `FontStyle.Bold`, `FontStyle.Italic`, `FontStyle.BoldAndItalic`, `FontStyle.Normal` |

#### 文本布局
| 属性 | 类型 | 描述 | 示例 |
|------|------|------|------|
| `unityTextAlign` | `StyleEnum<TextAnchor>` | 文本对齐方式 | `TextAnchor.MiddleCenter`, `TextAnchor.UpperLeft` |
| `letterSpacing` | `StyleLength` | 字符间距 | `1px`, `0.5em` |
| `wordSpacing` | `StyleLength` | 单词间距 | `2px`, `1em` |
| `unityParagraphSpacing` | `StyleLength` | 段落间距 | `10px` |
| `whiteSpace` | `StyleEnum<WhiteSpace>` | 空白处理方式 | `WhiteSpace.Normal`, `WhiteSpace.NoWrap` |

#### 文本效果
| 属性 | 类型 | 描述 | 示例 |
|------|------|------|------|
| `textShadow` | `StyleTextShadow` | 文本阴影 | `new TextShadow() { offset = new Vector2(1,1), blurRadius = 2, color = Color.black }` |
| `unityTextOutlineColor` | `StyleColor` | 文本描边颜色 | `Color.black` |
| `unityTextOutlineWidth` | `StyleFloat` | 文本描边宽度 | `1.0f` |
| `textOverflow` | `StyleEnum<TextOverflow>` | 文本溢出处理 | `TextOverflow.Clip`, `TextOverflow.Ellipsis` |
| `unityTextOverflowPosition` | `StyleEnum<TextOverflowPosition>` | 文本溢出位置 | `TextOverflowPosition.End`, `TextOverflowPosition.Start` |

### 4. 变换与过渡 (Transform & Transition)

#### 变换属性
| 属性 | 类型 | 描述 | 示例 |
|------|------|------|------|
| `rotate` | `StyleRotate` | 旋转变换 | `new Rotate(new Angle(45, AngleUnit.Degree))` |
| `scale` | `StyleScale` | 缩放变换 | `new Scale(new Vector3(1.5f, 1.5f, 1))` |
| `translate` | `StyleTranslate` | 平移变换 | `new Translate(new Length(10), new Length(20))` |
| `transformOrigin` | `StyleTransformOrigin` | 变换原点 | `new TransformOrigin(new Length(0, LengthUnit.Percent), new Length(0, LengthUnit.Percent))` |

#### 过渡动画
| 属性 | 类型 | 描述 | 示例 |
|------|------|------|------|
| `transitionProperty` | `StyleList<StylePropertyName>` | 应用过渡效果的属性列表 | `new List<StylePropertyName> { "background-color", "opacity" }` |
| `transitionDuration` | `StyleList<TimeValue>` | 过渡持续时间 | `new List<TimeValue> { new TimeValue(0.3f, TimeUnit.Second) }` |
| `transitionTimingFunction` | `StyleList<EasingFunction>` | 过渡时序函数 | `new List<EasingFunction> { EasingFunction.EaseInOut }` |
| `transitionDelay` | `StyleList<TimeValue>` | 过渡延迟时间 | `new List<TimeValue> { new TimeValue(0.1f, TimeUnit.Second) }` |

### 5. 视觉与交互 (Visual & Interaction)

#### 可见性与显示
| 属性 | 类型 | 描述 | 示例 |
|------|------|------|------|
| `visibility` | `StyleEnum<Visibility>` | 元素可见性 | `Visibility.Visible`, `Visibility.Hidden` |
| `display` | `StyleEnum<DisplayStyle>` | 显示方式 | `DisplayStyle.Flex`, `DisplayStyle.None` |
| `opacity` | `StyleFloat` | 不透明度 | `1.0f` (完全不透明), `0.5f` (半透明) |
| `overflow` | `StyleEnum<Overflow>` | 内容溢出处理 | `Overflow.Visible`, `Overflow.Hidden` |
| `unityOverflowClipBox` | `StyleEnum<OverflowClipBox>` | 溢出裁剪框 | `OverflowClipBox.PaddingBox`, `OverflowClipBox.ContentBox` |

#### 交互样式
| 属性 | 类型 | 描述 | 示例 |
|------|------|------|------|
| `cursor` | `StyleCursor` | 鼠标指针样式 | `new Cursor { texture = someTexture, hotspot = Vector2.zero }` |

## 使用示例

### 基础用法
```csharp
using UnityEngine;
using UnityEngine.UIElements;

public class MyUIElement : VisualElement
{
    public MyUIElement()
    {
        // 设置布局属性
        style.flexDirection = FlexDirection.Row;
        style.justifyContent = Justify.SpaceBetween;
        style.alignItems = Align.Center;

        // 设置尺寸
        style.width = 300;
        style.height = 200;
        style.minWidth = 100;

        // 设置边距和内边距
        style.marginTop = 10;
        style.marginBottom = 10;
        style.paddingLeft = 20;
        style.paddingRight = 20;

        // 设置背景和边框
        style.backgroundColor = new Color(0.1f, 0.1f, 0.1f, 1.0f);
        style.borderTopWidth = 2;
        style.borderTopColor = Color.blue;
        style.borderRadius = 10; // 设置四个角相同的圆角

        // 设置文本样式
        style.color = Color.white;
        style.fontSize = 16;
        style.unityTextAlign = TextAnchor.MiddleCenter;
    }
}
```

### 创建圆形元素
```csharp
public static VisualElement CreateCircle(float size, Color color)
{
    var circle = new VisualElement();

    // 设置尺寸为正方形
    circle.style.width = size;
    circle.style.height = size;

    // 设置圆角为50%以创建圆形
    circle.style.borderTopLeftRadius = size / 2;
    circle.style.borderTopRightRadius = size / 2;
    circle.style.borderBottomLeftRadius = size / 2;
    circle.style.borderBottomRightRadius = size / 2;

    // 设置背景颜色
    circle.style.backgroundColor = color;

    // 设置边框（可选）
    circle.style.borderWidth = 1;
    circle.style.borderColor = new Color(1, 1, 1, 0.5f);

    return circle;
}
```

### 弹性布局示例
```csharp
public VisualElement CreateFlexContainer()
{
    var container = new VisualElement();

    // 容器样式
    container.style.flexDirection = FlexDirection.Row;
    container.style.justifyContent = Justify.SpaceAround;
    container.style.alignItems = Align.Center;
    container.style.flexWrap = Wrap.Wrap;
    container.style.width = 500;
    container.style.height = 300;
    container.style.backgroundColor = new Color(0.2f, 0.2f, 0.2f, 1.0f);
    container.style.padding = 20;

    // 创建弹性项目
    for (int i = 0; i < 5; i++)
    {
        var item = new VisualElement();
        item.style.width = 80;
        item.style.height = 80;
        item.style.margin = 5;
        item.style.backgroundColor = Color.HSVToRGB(i * 0.2f, 0.8f, 0.8f);

        // 设置不同的弹性属性
        if (i == 0) item.style.flexGrow = 2;  // 第一个项目放大2倍
        else item.style.flexGrow = 1;          // 其他项目放大1倍

        container.Add(item);
    }

    return container;
}
```

### 响应式布局
```csharp
public VisualElement CreateResponsiveLayout()
{
    var responsiveContainer = new VisualElement();

    // 基础样式
    responsiveContainer.style.flexDirection = FlexDirection.Column;
    responsiveContainer.style.width = new StyleLength(new Length(80, LengthUnit.Percent));
    responsiveContainer.style.maxWidth = 1200;
    responsiveContainer.style.marginLeft = StyleKeyword.Auto;
    responsiveContainer.style.marginRight = StyleKeyword.Auto;

    // 媒体查询模拟（通过代码判断）
    void UpdateLayout(bool isMobile)
    {
        if (isMobile)
        {
            responsiveContainer.style.flexDirection = FlexDirection.Column;
            responsiveContainer.style.paddingLeft = 10;
            responsiveContainer.style.paddingRight = 10;
        }
        else
        {
            responsiveContainer.style.flexDirection = FlexDirection.Row;
            responsiveContainer.style.paddingLeft = 50;
            responsiveContainer.style.paddingRight = 50;
        }
    }

    return responsiveContainer;
}
```

## 最佳实践

### 1. 样式设置顺序
```csharp
// 推荐顺序
element.style.display = DisplayStyle.Flex;      // 1. 显示方式
element.style.flexDirection = FlexDirection.Row; // 2. 布局方向
element.style.width = 100;                      // 3. 尺寸
element.style.margin = 10;                      // 4. 外边距
element.style.padding = 5;                      // 5. 内边距
element.style.backgroundColor = Color.blue;     // 6. 背景
element.style.borderWidth = 1;                  // 7. 边框
element.style.borderRadius = 5;                 // 8. 圆角
```

### 2. 性能优化建议
- **批量设置**：尽量减少单独的样式属性设置
- **重用样式**：创建可重用的样式类或扩展方法
- **避免频繁更新**：在布局稳定后再设置复杂样式
- **使用USS**：对于静态样式，优先使用 USS 样式表

### 3. 常见陷阱
```csharp
// ❌ 错误：直接设置 borderRadius 不会生效
element.style.borderRadius = 10;

// ✅ 正确：需要设置四个角的圆角
element.style.borderTopLeftRadius = 10;
element.style.borderTopRightRadius = 10;
element.style.borderBottomLeftRadius = 10;
element.style.borderBottomRightRadius = 10;

// 或者使用扩展方法
public static void SetBorderRadius(this IStyle style, float radius)
{
    style.borderTopLeftRadius = radius;
    style.borderTopRightRadius = radius;
    style.borderBottomLeftRadius = radius;
    style.borderBottomRightRadius = radius;
}

// 使用
element.style.SetBorderRadius(10);
```

### 4. 兼容性注意事项
- **单位转换**：注意 `px`、`%`、`em` 等单位的正确使用
- **平台差异**：某些样式在编辑器窗口和运行时可能有差异
- **版本兼容**：检查 Unity 版本对 UI Toolkit 的支持情况

## 扩展方法示例

```csharp
public static class StyleExtensions
{
    // 快速设置边框
    public static void SetBorder(this IStyle style, float width, Color color)
    {
        style.borderTopWidth = width;
        style.borderBottomWidth = width;
        style.borderLeftWidth = width;
        style.borderRightWidth = width;

        style.borderTopColor = color;
        style.borderBottomColor = color;
        style.borderLeftColor = color;
        style.borderRightColor = color;
    }

    // 快速设置圆角
    public static void SetBorderRadius(this IStyle style, float radius)
    {
        style.borderTopLeftRadius = radius;
        style.borderTopRightRadius = radius;
        style.borderBottomLeftRadius = radius;
        style.borderBottomRightRadius = radius;
    }

    // 快速设置边距（所有方向相同）
    public static void SetMargin(this IStyle style, float margin)
    {
        style.marginTop = margin;
        style.marginBottom = margin;
        style.marginLeft = margin;
        style.marginRight = margin;
    }

    // 快速设置内边距（所有方向相同）
    public static void SetPadding(this IStyle style, float padding)
    {
        style.paddingTop = padding;
        style.paddingBottom = padding;
        style.paddingLeft = padding;
        style.paddingRight = padding;
    }

    // 设置背景渐变
    public static void SetGradientBackground(this IStyle style, Color from, Color to, float angle = 90)
    {
        // 创建渐变纹理（简化示例）
        var gradientTex = new Texture2D(2, 2);
        // ... 设置渐变纹理逻辑

        style.backgroundImage = new Background(gradientTex);
    }
}
```

## 调试技巧

### 1. 样式检查
```csharp
// 打印元素的所有样式值
public static void DebugStyle(VisualElement element)
{
    Debug.Log($"Element: {element.name}");
    Debug.Log($"  Width: {element.style.width}");
    Debug.Log($"  Height: {element.style.height}");
    Debug.Log($"  Display: {element.style.display}");
    Debug.Log($"  Position: {element.style.position}");
    // ... 添加更多需要检查的属性
}
```

### 2. 布局调试
```csharp
// 添加调试边框
public static void AddDebugBorder(VisualElement element, Color color)
{
    element.style.borderWidth = 1;
    element.style.borderColor = color;
}

// 添加调试背景
public static void AddDebugBackground(VisualElement element, Color color)
{
    element.style.backgroundColor = new Color(color.r, color.g, color.b, 0.3f);
}
```

## 资源链接

- [Unity 官方文档：UI Toolkit](https://docs.unity3d.com/Manual/UIElements.html)
- [UI Toolkit 样式参考](https://docs.unity3d.com/Manual/UIE-Style-Reference.html)
- [UI Toolkit 布局系统](https://docs.unity3d.com/Manual/UIE-Layout-Engine.html)
- [USS 选择器参考](https://docs.unity3d.com/Manual/UIE-USS-Selector-Reference.html)

---

## 非法API禁令与兼容性警告

### Unity UI Toolkit Runtime 不支持的功能
以下API在Unity UI Toolkit Runtime中**不存在**或**不推荐使用**，会导致编译错误：

#### 1. 事件与输入相关
| 非法API | 正确替代方案 | 说明 |
|---------|-------------|------|
| `evt.localPosition` (返回Vector3) | `(Vector2)evt.localPosition` | 必须显式转换为Vector2 |
| `evt.localPosition` (WheelEvent) | `evt.localMousePosition` | WheelEvent使用不同的属性名 |
| `MouseCursor` 枚举 | `StyleCursor` 类型 + USS cursor样式 | Unity Runtime不支持MouseCursor枚举 |

#### 2. 样式属性相关
| 非法API | 正确替代方案 | 说明 |
|---------|-------------|------|
| `style.boxShadow` | 使用 `backgroundImage` + 纹理 | Unity Runtime不支持CSS box-shadow |
| `style.zIndex` | 使用元素添加顺序控制层级 | zIndex在Runtime中不可用 |
| `style.textShadow` | 使用 `IMGUIContainer` 或自定义绘制 | 文本阴影需要额外处理 |

#### 3. 类型定义缺失
| 缺失类型 | 解决方案 | 说明 |
|----------|----------|------|
| `BoxShadow` | 不使用阴影或使用纹理替代 | Unity Runtime中不存在此类型 |
| `TextShadow` | 使用 `IMGUIContainer` 绘制文本 | 文本阴影需要自定义实现 |

### 最佳实践总结
1. **向量类型转换**：所有事件位置属性都需要显式转换
   ```csharp
   // ❌ 错误：类型不匹配
   Vector2 pos = evt.localPosition;

   // ✅ 正确：显式转换
   Vector2 pos = (Vector2)evt.localPosition;
   ```

2. **阴影效果替代方案**：
   ```csharp
   // ❌ 错误：不支持boxShadow
   style.boxShadow = new BoxShadow { ... };

   // ✅ 正确：使用背景图片或IMGUI
   style.backgroundImage = new Background(shadowTexture);
   // 或使用 IMGUIContainer 绘制阴影
   ```

3. **层级控制**：
   ```csharp
   // ❌ 错误：不支持zIndex
   style.zIndex = 1;

   // ✅ 正确：通过添加顺序控制
   parent.Add(backgroundElement); // 先添加的在下层
   parent.Add(foregroundElement); // 后添加的在上层
   ```

4. **鼠标指针样式**：
   ```csharp
   // ❌ 错误：MouseCursor不存在
   style.cursor = MouseCursor.PointingHand;

   // ✅ 正确：使用USS样式或避免设置
   // 在USS文件中定义: .my-element { cursor: pointer; }
   // 或运行时：style.cursor = new StyleCursor(); // 但需要有效资源
   ```

### 已验证可用的核心API
以下API在Unity 2022.3 LTS的Runtime中**确定可用**：
- ✅ 基本布局：`width`, `height`, `position`, `left`, `top`
- ✅ 弹性布局：`flexDirection`, `justifyContent`, `alignItems`
- ✅ 边距内边距：`marginTop`, `paddingLeft` 等
- ✅ 背景边框：`backgroundColor`, `borderTopWidth`, `borderTopColor`
- ✅ 圆角：`borderTopLeftRadius` (四个角分别设置)
- ✅ 变换：`translate`, `scale`, `rotate` (但注意性能)
- ✅ 显示控制：`display`, `visibility`, `opacity`

### 调试建议
遇到编译错误时：
1. 检查是否有错误的 `using` 语句
2. 验证API是否在Unity官方文档中列出
3. 参考项目现有代码（如BigMapEditorWindow.cs）
4. 优先使用最基本、最常用的API

---

## 大地图运行时渲染系统开发经验

基于《MineRTS》项目的大地图拓扑渲染系统开发实践，总结以下核心经验：

### 1. 正交摄像机投影管线设计

**核心思想**：摒弃传统UI布局思维，采用图形学正交摄像机投影管线。

**关键公式**：
- **PPU计算**：`PPU = Screen.height / (OrthographicSize * 2f)`
- **局部空间坐标**：`Local_X = World.x * PPU`, `Local_Y = -World.y * PPU` (抵消UI Y轴向下的差异)
- **容器变换**：`Translate_X = (Screen.width / 2f) - (CameraPos.x * PPU * Zoom)`
- **容器变换**：`Translate_Y = (Screen.height / 2f) + (CameraPos.y * PPU * Zoom)`

**架构优势**：
- 当 `CameraPos = (0,0)` 且 `Zoom = 1` 时，世界坐标 `(0,0)` 的节点位于屏幕正中心
- 无论屏幕分辨率如何变化，只要 `OrthographicSize` 不变，屏幕纵向显示的世界高度范围保持恒定
- 实现真正的"所见即所得"坐标映射

### 2. 容器级变换优化

**优化原则**：严禁遍历修改子节点的 `style.left/top`，只修改 `MapContainer` 的 `translate` 和 `scale`。

**实现方式**：
```csharp
// 正确：只修改容器变换
_mapContainer.style.translate = new Translate(translateX, translateY);
_mapContainer.style.scale = new Scale(new Vector3(_zoomLevel, _zoomLevel, 1));

// 错误：遍历修改子节点
foreach (var node in nodes)
{
    node.style.left = CalculatePositionX();
    node.style.top = CalculatePositionY();
}
```

**性能优势**：
- GPU硬件加速变换，避免CPU遍历开销
- 支持平滑动画和动态缩放
- 减少布局计算次数

### 3. 动态PPU计算

**设计目标**：适应屏幕分辨率变化，保持视觉一致性。

**实现机制**：
```csharp
private void UpdatePPU()
{
    float newPPU = Screen.height / (_orthographicSize * 2f);

    if (Mathf.Abs(_currentPPU - newPPU) > 0.001f)
    {
        _currentPPU = newPPU;
        _mapContainer?.UpdatePPU(_currentPPU); // 通知容器更新
    }
}
```

**使用场景**：
- 窗口大小变化时
- 设备旋转时（移动端）
- 多显示器不同分辨率时

### 4. Painter2D绘制技术

**连线绘制**：使用 `Painter2D` 在 `OnGenerateVisualContent` 中绘制连线和箭头。

**关键代码**：
```csharp
private void OnGenerateVisualContent(MeshGenerationContext ctx)
{
    var painter = ctx.painter2D;
    if (painter == null) return;

    // 绘制连线
    painter.strokeColor = edgeColor;
    painter.lineWidth = edgeWidth;
    painter.BeginPath();
    painter.MoveTo(fromPos);
    painter.LineTo(toPos);
    painter.Stroke();

    // 绘制箭头（单向连线）
    if (edge.Direction == EdgeDirection.Unidirectional)
    {
        DrawArrow(painter, fromPos, toPos, edgeWidth);
    }
}
```

**性能优势**：
- 批量绘制，减少Draw Call
- GPU加速的向量图形
- 支持动态线宽和样式

### 5. 事件委托通信模式

**分层架构**：
```
RuntimeNodeElement (点击) → RuntimeMapContainer (转发) → BigMapRuntimeRenderer (业务处理)
```

**实现模式**：
```csharp
// 容器层：注册回调
_mapContainer.SetNodeClickCallback((nodeId, displayName) =>
{
    Debug.Log($"节点被点击: {displayName} (ID: {nodeId})");
    // 业务逻辑处理
});

// 节点层：触发事件
private void OnClick(ClickEvent evt)
{
    Debug.Log($"节点 '{_nodeData.DisplayName}' 被点击");
    evt.StopPropagation();
}
```

**架构优势**：
- 职责分离，便于测试和维护
- 支持多级事件转发和过滤
- 避免紧耦合

### 6. 硬几何平面风格实现

**视觉设计**：
- 节点尺寸：20×20像素
- 颜色方案：科技蓝背景，白色边框
- 交互反馈：悬停高亮，点击缩放动画

**代码实现**：
```csharp
private void SetupBaseStyle()
{
    // 背景颜色（只使用合法API）
    style.backgroundColor = NORMAL_BACKGROUND_COLOR;

    // 边框样式：2px白色实线边框
    style.borderTopWidth = BORDER_WIDTH;
    style.borderBottomWidth = BORDER_WIDTH;
    style.borderLeftWidth = BORDER_WIDTH;
    style.borderRightWidth = BORDER_WIDTH;

    style.borderTopColor = BORDER_COLOR;
    style.borderBottomColor = BORDER_COLOR;
    style.borderLeftColor = BORDER_COLOR;
    style.borderRightColor = BORDER_COLOR;

    // 圆角：轻微圆角
    style.borderTopLeftRadius = 2f;
    style.borderTopRightRadius = 2f;
    style.borderBottomLeftRadius = 2f;
    style.borderBottomRightRadius = 2f;
}
```

### 7. 从错误中学到的教训

#### 错误：API兼容性问题
- **症状**：编译错误提示"未包含定义"、"找不到类型或命名空间名"
- **根本原因**：混淆了Unity UI Toolkit Runtime API与Editor API
- **解决方案**：参考项目现有代码（BigMapEditorWindow.cs），使用已验证可用的核心API

#### 错误：类型转换问题
```csharp
// ❌ 错误：类型不匹配
Vector2 pos = evt.localPosition;

// ✅ 正确：显式转换
Vector2 pos = (Vector2)evt.localPosition;
```

#### 错误：坐标系统混乱
- **症状**：节点位置不准确，缩放时偏移
- **根本原因**：使用了写死的比例常数（如100f）
- **解决方案**：实现严格的数学公式，动态计算PPU

### 8. 性能优化建议

#### 渲染优化
1. **批量处理**：节点创建、位置更新等操作尽量批量处理
2. **脏标记**：使用 `MarkDirtyRepaint()` 仅在必要时触发重绘
3. **延迟加载**：大规模地图数据分块加载

#### 内存优化
1. **对象池**：节点元素和连线数据使用对象池复用
2. **数据压缩**：JSON数据压缩存储，运行时解压
3. **缓存策略**：频繁访问的数据缓存计算结果

#### 交互优化
1. **防抖处理**：滚轮缩放和拖拽操作添加防抖逻辑
2. **分层渲染**：背景、连线、节点分层独立渲染
3. **异步处理**：耗时操作异步执行，避免阻塞UI线程

### 9. 测试验证方案

#### 坐标系统验证
```csharp
// 验证公式正确性
void ValidateCoordinateSystem()
{
    // 条件1：CameraPos = (0,0), Zoom = 1时，世界坐标(0,0)应在屏幕中心
    Vector2 screenPos = WorldToScreen(Vector2.zero);
    Vector2 screenCenter = new Vector2(Screen.width / 2f, Screen.height / 2f);
    Assert.IsTrue(Vector2.Distance(screenPos, screenCenter) < 0.1f);

    // 条件2：OrthographicSize不变时，屏幕纵向显示的世界高度范围恒定
    float worldHeightInView = _orthographicSize * 2f; // 恒定
    // 无论Screen.height如何变化，此值应保持不变
}
```

#### 性能测试
1. **节点数量**：测试不同节点数量（100, 1000, 10000）下的渲染性能
2. **交互响应**：测试拖拽、缩放操作的帧率稳定性
3. **内存占用**：监控长时间运行的内存增长情况

---

**文档版本**：1.2.0
**更新日期**：2026-03-01
**适用版本**：Unity 2022.3 LTS 及以上
**维护者**：猫娘助手开发团队
**更新说明**：添加大地图运行时渲染系统开发经验，总结正交摄像机投影管线设计、容器级变换优化、动态PPU计算等核心实践