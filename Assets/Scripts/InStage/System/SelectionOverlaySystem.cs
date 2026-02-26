using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class SelectionOverlaySystem : SingletonMono<SelectionOverlaySystem>
{
    [Header("虚线材质设置")]
    public Material dashedLineMaterial; // 拖入 Assets/Shaders/PowerNet/Custom_Effects_DashedLineInstanced.mat
    [Header("实线材质设置 (可选)")]
    public Material solidLineMaterial; // 拖入 Assets/Shaders/PowerNet/Custom_Effects_SolidLineInstanced.mat (新建)
    public Color selectionColor = new Color(0f, 1f, 1f, 0.8f); // 选择框颜色 (青色半透明)
    public float edgeMargin = 0.05f; // 选择框边距 (略大于单位尺寸)
    public float lineWidth = 0.02f; // 线框宽度

    // GPU实例化渲染数据
    private List<Matrix4x4> _selectionMatrices = new List<Matrix4x4>();
    private Mesh _selectionBoxMesh;
    private MaterialPropertyBlock _propertyBlock;

    // 材质缓存 (避免每帧创建新材质)
    private Material _instancedMaterial;

    protected override void Awake()
    {
        base.Awake();

        // 创建线框Mesh
        _selectionBoxMesh = CreateSelectionBoxMesh();

        // 初始化材质属性块
        _propertyBlock = new MaterialPropertyBlock();

        // 如果提供了材质，创建支持实例化的副本
        // 优先使用实线材质，如果未提供则使用虚线材质
        Material baseMaterial = solidLineMaterial != null ? solidLineMaterial : dashedLineMaterial;
        if (baseMaterial != null)
        {
            _instancedMaterial = new Material(baseMaterial);
            _instancedMaterial.enableInstancing = true;
            _instancedMaterial.renderQueue = 3030; // 高于单位(3010)和血条(3020)，低于UI
        }
    }

    /// <summary>
    /// 创建选择框线框Mesh (单位大小：1x1，中心在原点，使用三角形渲染线框)
    /// </summary>
    private Mesh CreateSelectionBoxMesh()
    {
        Mesh mesh = new Mesh();
        mesh.name = "SelectionBoxWireframe";

        // 线宽的一半 (向内/向外扩展)
        float halfWidth = lineWidth * 0.5f;

        // 矩形内边界和外边界
        float innerLeft = -0.5f + halfWidth;
        float innerRight = 0.5f - halfWidth;
        float innerBottom = -0.5f + halfWidth;
        float innerTop = 0.5f - halfWidth;
        float outerLeft = -0.5f - halfWidth;
        float outerRight = 0.5f + halfWidth;
        float outerBottom = -0.5f - halfWidth;
        float outerTop = 0.5f + halfWidth;

        // 4条边，每条边6个顶点 (2个三角形)，共24个顶点
        Vector3[] vertices = new Vector3[24];
        Vector2[] uvs = new Vector2[24];

        // 下边 (顶点索引 0-5)
        vertices[0] = new Vector3(outerLeft, outerBottom, 0);   // 左下外
        vertices[1] = new Vector3(outerRight, outerBottom, 0);  // 右下外
        vertices[2] = new Vector3(innerLeft, innerBottom, 0);   // 左下内
        vertices[3] = new Vector3(innerRight, innerBottom, 0);  // 右下内
        vertices[4] = new Vector3(innerLeft, innerBottom, 0);   // 左下内 (重复，用于第二个三角形)
        vertices[5] = new Vector3(innerRight, innerBottom, 0);  // 右下内 (重复)

        // 右边 (顶点索引 6-11)
        vertices[6] = new Vector3(outerRight, outerBottom, 0);  // 右下外
        vertices[7] = new Vector3(outerRight, outerTop, 0);     // 右上外
        vertices[8] = new Vector3(innerRight, innerBottom, 0);  // 右下内
        vertices[9] = new Vector3(innerRight, innerTop, 0);     // 右上内
        vertices[10] = new Vector3(innerRight, innerBottom, 0); // 右下内 (重复)
        vertices[11] = new Vector3(innerRight, innerTop, 0);    // 右上内 (重复)

        // 上边 (顶点索引 12-17)
        vertices[12] = new Vector3(outerRight, outerTop, 0);    // 右上外
        vertices[13] = new Vector3(outerLeft, outerTop, 0);     // 左上外
        vertices[14] = new Vector3(innerRight, innerTop, 0);    // 右上内
        vertices[15] = new Vector3(innerLeft, innerTop, 0);     // 左上内
        vertices[16] = new Vector3(innerRight, innerTop, 0);    // 右上内 (重复)
        vertices[17] = new Vector3(innerLeft, innerTop, 0);     // 左上内 (重复)

        // 左边 (顶点索引 18-23)
        vertices[18] = new Vector3(outerLeft, outerTop, 0);     // 左上外
        vertices[19] = new Vector3(outerLeft, outerBottom, 0);  // 左下外
        vertices[20] = new Vector3(innerLeft, innerTop, 0);     // 左上内
        vertices[21] = new Vector3(innerLeft, innerBottom, 0);  // 左下内
        vertices[22] = new Vector3(innerLeft, innerTop, 0);     // 左上内 (重复)
        vertices[23] = new Vector3(innerLeft, innerBottom, 0);  // 左下内 (重复)

        // 设置UV：每个边的UV沿着边方向，v坐标表示内外 (0=外, 1=内)
        for (int i = 0; i < 24; i += 6)
        {
            // 每个边的6个顶点
            uvs[i] = new Vector2(0, 0);     // 外起点
            uvs[i + 1] = new Vector2(1, 0); // 外终点
            uvs[i + 2] = new Vector2(0, 1); // 内起点
            uvs[i + 3] = new Vector2(1, 1); // 内终点
            uvs[i + 4] = new Vector2(0, 1); // 内起点 (重复)
            uvs[i + 5] = new Vector2(1, 1); // 内终点 (重复)
        }

        // 三角形索引：每个边2个三角形，共8个三角形
        int[] triangles = new int[8 * 3]; // 24个索引

        int triIndex = 0;
        for (int edge = 0; edge < 4; edge++)
        {
            int baseIndex = edge * 6;

            // 第一个三角形：外起点 -> 内起点 -> 外终点
            triangles[triIndex++] = baseIndex + 0;
            triangles[triIndex++] = baseIndex + 2;
            triangles[triIndex++] = baseIndex + 1;

            // 第二个三角形：内起点 -> 内终点 -> 外终点
            triangles[triIndex++] = baseIndex + 2;
            triangles[triIndex++] = baseIndex + 3;
            triangles[triIndex++] = baseIndex + 1;
        }

        mesh.vertices = vertices;
        mesh.uv = uvs;
        mesh.triangles = triangles;

        mesh.RecalculateBounds();
        mesh.RecalculateNormals();

        return mesh;
    }

    /// <summary>
    /// 更新选择框渲染 (由EntitySystem.UpdateSystem调用)
    /// </summary>
    public void UpdateRender()
    {
        if (EntitySystem.Instance == null || _instancedMaterial == null || _selectionBoxMesh == null)
            return;

        var whole = EntitySystem.Instance.wholeComponent;
        if (whole == null) return;

        // 清空上一帧的矩阵数据
        _selectionMatrices.Clear();

        // 收集所有被选中单位的变换矩阵
        for (int i = 0; i < whole.entityCount; i++)
        {
            ref var core = ref whole.coreComponent[i];
            if (!core.Active) continue;

            ref var draw = ref whole.drawComponent[i];
            if (!draw.IsSelected) continue;

            // 计算选择框尺寸 (单位尺寸 + 边距)
            Vector2Int size = core.LogicSize;
            float width = size.x + edgeMargin * 2;
            float height = size.y + edgeMargin * 2;

            // 位置：使用单位的插值位置，Z轴设为-1.1f (在单位之上但在UI之下)
            Vector3 position = new Vector3(core.Position.x, core.Position.y, -1.1f);

            // 缩放：根据单位尺寸调整
            Vector3 scale = new Vector3(width, height, 1f);

            // 创建变换矩阵
            Matrix4x4 matrix = Matrix4x4.TRS(position, Quaternion.identity, scale);
            _selectionMatrices.Add(matrix);
        }

        // 如果有被选中的单位，进行实例化绘制
        if (_selectionMatrices.Count > 0)
        {
            // 设置颜色属性
            _propertyBlock.Clear();
            _propertyBlock.SetColor("_BaseColor", selectionColor);

            // 设置RenderParams
            RenderParams rp = new RenderParams(_instancedMaterial)
            {
                worldBounds = new Bounds(Vector3.zero, Vector3.one * 10000),
                shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off,
                receiveShadows = false,
                matProps = _propertyBlock,
                rendererPriority = 100 // 高优先级，确保在最前面
            };

            // 单批次绘制所有选择框
            Graphics.RenderMeshInstanced(rp, _selectionBoxMesh, 0, _selectionMatrices);
        }
    }

    /// <summary>
    /// 隐藏所有选择框 (供clear命令调用)
    /// </summary>
    public void HideAllSelectionBoxes()
    {
        // GPU实例化方案下，只需清空矩阵列表即可
        _selectionMatrices.Clear();
    }

    // 保留旧接口以供兼容性
    public void RefreshSelectionDisplay()
    {
        // 现在UpdateRender已包含刷新逻辑
    }
}