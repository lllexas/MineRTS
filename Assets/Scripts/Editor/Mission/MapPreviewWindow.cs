#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// 地图预览窗口 - 用于可视化选择坐标喵~
/// </summary>
public class MapPreviewWindow : EditorWindow
{
    private string _mapId;
    private Action<Vector2Int> _onCoordinateSelected;
    private Texture2D _mapPreviewTexture;
    private Vector2Int _selectedCoordinate;
    private int _mapWidth = 100;  // 默认地图宽度
    private int _mapHeight = 100; // 默认地图高度
    private int _originX = 0;     // 地图原点偏移 X
    private int _originY = 0;     // 地图原点偏移 Y

    [SerializeField] private VisualElement _root;
    [SerializeField] private Label _coordinateLabel;

    /// <summary>
    /// 打开地图预览窗口喵~
    /// </summary>
    /// <param name="mapId">地图 ID</param>
    /// <param name="onCoordinateSelected">坐标选择回调</param>
    public static void Open(string mapId, Action<Vector2Int> onCoordinateSelected)
    {
        var window = CreateInstance<MapPreviewWindow>();
        window._mapId = mapId;
        window._onCoordinateSelected = onCoordinateSelected;
        window.titleContent = new GUIContent($"地图预览：{mapId}");
        window.minSize = new Vector2(512, 512);
        window.ShowAuxWindow();
    }

    private void CreateGUI()
    {
        _root = rootVisualElement;

        // 创建地图预览区域
        var previewContainer = new VisualElement
        {
            style = {
                flexGrow = 1,
                flexDirection = FlexDirection.Column,
                alignItems = Align.Center,
                justifyContent = Justify.Center
            }
        };

        // 地图图片（使用 Image 元素）
        var mapImage = new Image
        {
            name = "MapPreviewImage",
            style = {
                width = 480,
                height = 480,
                marginBottom = 10
            }
        };
        
        // 加载地图纹理
        LoadMapTexture(mapImage);
        
        mapImage.RegisterCallback<MouseDownEvent>(OnMapClick);
        previewContainer.Add(mapImage);

        // 坐标显示标签
        _coordinateLabel = new Label("点击地图选择坐标")
        {
            style = {
                fontSize = 14,
                marginBottom = 10
            }
        };
        previewContainer.Add(_coordinateLabel);

        // 按钮容器
        var buttonContainer = new VisualElement
        {
            style = {
                flexDirection = FlexDirection.Row,
                marginTop = 10
            }
        };

        // 确认按钮
        var confirmButton = new Button(() =>
        {
            if (_onCoordinateSelected != null)
            {
                _onCoordinateSelected(_selectedCoordinate);
            }
            Close();
        })
        {
            text = "确认选择",
            style = {
                width = 120,
                height = 30,
                marginRight = 10
            }
        };
        buttonContainer.Add(confirmButton);

        // 取消按钮
        var cancelButton = new Button(() => Close())
        {
            text = "取消",
            style = {
                width = 120,
                height = 30
            }
        };
        buttonContainer.Add(cancelButton);
        
        previewContainer.Add(buttonContainer);

        _root.Add(previewContainer);
    }

    private void LoadMapTexture(Image mapImage)
    {
        // 尝试加载 JSON 地图数据
        var jsonAsset = Resources.Load<TextAsset>($"Levels/{_mapId}");
        if (jsonAsset != null)
        {
            // 解析 JSON 并生成可视化纹理
            _mapPreviewTexture = GenerateMapTextureFromJson(jsonAsset.text);
            if (_mapPreviewTexture != null)
            {
                mapImage.style.backgroundImage = _mapPreviewTexture;
                return;
            }
        }

        // 如果 JSON 加载失败，回退到网格纹理
        _mapPreviewTexture = CreateGridTexture(480, 480);
        mapImage.style.backgroundImage = _mapPreviewTexture;
    }

    /// <summary>
    /// 从 JSON 数据生成地图可视化纹理喵~
    /// </summary>
    private Texture2D GenerateMapTextureFromJson(string jsonText)
    {
        try
        {
            var levelData = JsonUtility.FromJson<LevelMapData>(jsonText);
            if (levelData == null || levelData.groundMap == null)
            {
                Debug.LogWarning($"[MapPreview] 无法解析地图数据：{_mapId}");
                return null;
            }

            // 保存地图尺寸和偏移，用于坐标计算
            _mapWidth = levelData.width;
            _mapHeight = levelData.height;
            _originX = levelData.originX;
            _originY = levelData.originY;

            return GenerateMapTexture(levelData.groundMap, levelData.width, levelData.height);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[MapPreview] 解析 JSON 失败：{e.Message}");
            return null;
        }
    }

    /// <summary>
    /// 根据 groundMap 数组生成可视化纹理喵~
    /// </summary>
    private Texture2D GenerateMapTexture(int[] groundMap, int width, int height)
    {
        // 创建纹理，每个地图格素对应 1 个像素
        var texture = new Texture2D(width, height);
        var colors = new Color[width * height];

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int index = y * width + x;
                int tileId = (index < groundMap.Length) ? groundMap[index] : 0;

                // 根据 tile ID 生成颜色
                colors[index] = GetTileColor(tileId);
            }
        }

        texture.SetPixels(colors);
        texture.Apply();
        return texture;
    }

    /// <summary>
    /// 根据 tile ID 生成颜色喵~
    /// </summary>
    private Color GetTileColor(int tileId)
    {
        // 0 = 空地/海洋（深色）
        if (tileId == 0)
        {
            return new Color(0.1f, 0.1f, 0.15f); // 深蓝灰色
        }

        // 使用哈希生成不同的颜色，让不同 tile ID 可区分
        float hue = (tileId * 0.618033988749895f) % 1.0f; // 黄金比例哈希
        float saturation = 0.6f;
        float value = 0.8f;

        return Color.HSVToRGB(hue, saturation, value);
    }

    private Texture2D CreateGridTexture(int width, int height)
    {
        var texture = new Texture2D(width, height);
        var colors = new Color[width * height];

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                // 创建网格效果
                bool isGridLine = (x % 48 == 0) || (y % 48 == 0);
                colors[y * width + x] = isGridLine ? new Color(0.3f, 0.3f, 0.3f) : new Color(0.15f, 0.15f, 0.15f);
            }
        }

        texture.SetPixels(colors);
        texture.Apply();
        return texture;
    }

    private void OnMapClick(MouseDownEvent evt)
    {
        var image = evt.currentTarget as Image;
        if (image == null) return;

        // 获取点击位置（相对于 image 的本地坐标）
        var clickPos = evt.mousePosition;
        var imageRect = image.worldBound;

        // 转换为 image 本地坐标
        float localX = clickPos.x - imageRect.x;
        float localY = clickPos.y - imageRect.y;

        // 转换为地图数组索引（0 ~ width-1, 0 ~ height-1）
        int arrayX = Mathf.FloorToInt((localX / imageRect.width) * _mapWidth);
        int arrayY = Mathf.FloorToInt(((imageRect.height - localY) / imageRect.height) * _mapHeight);

        // 限制在合法范围内
        arrayX = Mathf.Clamp(arrayX, 0, _mapWidth - 1);
        arrayY = Mathf.Clamp(arrayY, 0, _mapHeight - 1);

        // 加上偏移量，转换为世界坐标
        int worldX = arrayX + _originX;
        int worldY = arrayY + _originY;

        _selectedCoordinate = new Vector2Int(worldX, worldY);

        // 更新坐标显示
        if (_coordinateLabel != null)
        {
            _coordinateLabel.text = $"选中坐标：({worldX}, {worldY})";
        }

        evt.StopPropagation();
    }

    private void OnDisable()
    {
        if (_mapPreviewTexture != null && !EditorApplication.isPlaying)
        {
            // 不销毁动态创建的纹理
        }
    }
}
#endif
