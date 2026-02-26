using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using System;
using System.IO;

public class MapPreviewWindow : EditorWindow
{
    private LevelMapData _mapData;
    private Action<Vector2Int> _onSelectedCallback;
    private Texture2D _mapTexture;

    private Image _mapImage;
    private Label _infoLabel;

    public static void Open(string mapId, Action<Vector2Int> onSelected)
    {
        var window = GetWindow<MapPreviewWindow>("地图坐标选取");
        window.minSize = new Vector2(600, 600);
        window.LoadMapAndRender(mapId, onSelected);
    }

    private void LoadMapAndRender(string mapId, Action<Vector2Int> onSelected)
    {
        _onSelectedCallback = onSelected;

        // 1. 加载数据 (假设数据在 Resources/Levels/ 下)
        TextAsset jsonAsset = Resources.Load<TextAsset>($"Levels/{mapId}");
        if (jsonAsset == null)
        {
            EditorUtility.DisplayDialog("错误", $"找不到地图数据: {mapId}", "确定");
            Close();
            return;
        }

        _mapData = JsonUtility.FromJson<LevelMapData>(jsonAsset.text);

        // 2. 生成 1像素=1Grid 的极简贴图
        GenerateTexture();

        // 3. 构建 UI
        BuildUI();
    }

    private void GenerateTexture()
    {
        if (_mapTexture != null) DestroyImmediate(_mapTexture);

        int w = _mapData.width;
        int h = _mapData.height;
        _mapTexture = new Texture2D(w, h, TextureFormat.RGBA32, false);
        _mapTexture.filterMode = FilterMode.Point; // 完美方块像素风
        _mapTexture.wrapMode = TextureWrapMode.Clamp;

        Color32[] colors = new Color32[w * h];

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                int tileId = _mapData.groundMap[y * w + x];
                colors[y * w + x] = GetColorForTile(tileId);
            }
        }

        _mapTexture.SetPixels32(colors);
        _mapTexture.Apply();
    }

    private Color32 GetColorForTile(int tileId)
    {
        if (tileId == 0) return new Color32(30, 30, 30, 255); // 空地底色
        // 伪随机生成稳定颜色
        UnityEngine.Random.InitState(tileId * 12345);
        return new Color32((byte)UnityEngine.Random.Range(50, 255), (byte)UnityEngine.Random.Range(50, 255), (byte)UnityEngine.Random.Range(50, 255), 255);
    }

    private void BuildUI()
    {
        rootVisualElement.Clear();
        rootVisualElement.style.flexDirection = FlexDirection.Column;
        rootVisualElement.style.paddingLeft = 10;
        rootVisualElement.style.paddingRight = 10;
        rootVisualElement.style.paddingTop = 10;
        rootVisualElement.style.paddingBottom = 10;

        // 顶部信息 - 修正：使用 levelId 而不是 mapName
        string mapName = !string.IsNullOrEmpty(_mapData.levelId) ? _mapData.levelId : "未知地图";
        _infoLabel = new Label($"正在选取地图: {mapName} | 尺寸: {_mapData.width}x{_mapData.height}");
        _infoLabel.style.fontSize = 14;
        _infoLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
        _infoLabel.style.marginBottom = 10;
        rootVisualElement.Add(_infoLabel);

        // 图片显示区 (占满剩余空间)
        _mapImage = new Image
        {
            image = _mapTexture,
            scaleMode = ScaleMode.ScaleToFit
        };
        _mapImage.style.flexGrow = 1;
        _mapImage.style.backgroundColor = Color.black;

        // 注册点击事件
        _mapImage.RegisterCallback<PointerDownEvent>(OnMapClicked);

        rootVisualElement.Add(_mapImage);

        // 提示文本
        var tip = new Label("提示: 直接点击上方地图区域选取坐标。");
        tip.style.marginTop = 5;
        tip.style.color = Color.gray;
        rootVisualElement.Add(tip);
    }

    private void OnMapClicked(PointerDownEvent evt)
    {
        if (_mapData == null || _mapImage.image == null) return;

        // 【精准计算核心】：由于 ScaleToFit 会产生黑边，我们必须利用图片控件的实际内容区域进行换算
        Rect contentRect = _mapImage.contentRect;

        // 获取实际贴图的渲染尺寸（去掉黑边后）
        float aspect = (float)_mapData.width / _mapData.height;
        float rectAspect = contentRect.width / contentRect.height;

        float renderWidth = contentRect.width;
        float renderHeight = contentRect.height;
        float offsetX = 0f;
        float offsetY = 0f;

        if (aspect > rectAspect)
        {
            // 上下有黑边
            renderHeight = contentRect.width / aspect;
            offsetY = (contentRect.height - renderHeight) / 2f;
        }
        else
        {
            // 左右有黑边
            renderWidth = contentRect.height * aspect;
            offsetX = (contentRect.width - renderWidth) / 2f;
        }

        // 剔除点击在黑边上的情况
        Vector2 localPos = evt.localPosition;
        if (localPos.x < offsetX || localPos.x > offsetX + renderWidth ||
            localPos.y < offsetY || localPos.y > offsetY + renderHeight)
        {
            return;
        }

        // 计算比例 0.0 ~ 1.0
        float percentX = (localPos.x - offsetX) / renderWidth;
        float percentY = 1.0f - ((localPos.y - offsetY) / renderHeight); // UI坐标Y轴向下，地图坐标Y向上

        // 映射到网格
        int gridX = Mathf.FloorToInt(percentX * _mapData.width) + _mapData.originX;
        int gridY = Mathf.FloorToInt(percentY * _mapData.height) + _mapData.originY;

        Vector2Int result = new Vector2Int(gridX, gridY);

        // 执行回调并关闭
        _onSelectedCallback?.Invoke(result);
        Close();
    }

    private void OnDisable()
    {
        // 防治内存泄漏
        if (_mapTexture != null)
        {
            DestroyImmediate(_mapTexture);
            _mapTexture = null;
        }
    }
}