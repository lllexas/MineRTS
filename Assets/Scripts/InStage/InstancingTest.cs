using UnityEngine;
using UnityEngine.Rendering;

public class ModernInstancingTest : MonoBehaviour
{
    public Material testMaterial;
    private Mesh _quadMesh;
    private Matrix4x4[] _matrices;
    private RenderParams _rp;

    [Range(1, 1023)]
    public int instanceCount = 100;

    void Start()
    {
        // 1. 创建 Mesh (同前)
        _quadMesh = new Mesh();
        _quadMesh.vertices = new Vector3[]
        {
            new Vector3(-0.5f, -0.5f, 0),
            new Vector3( 0.5f, -0.5f, 0),
            new Vector3(-0.5f,  0.5f, 0),
            new Vector3( 0.5f,  0.5f, 0)
        };
        _quadMesh.uv = new Vector2[] { Vector2.zero, Vector2.right, Vector2.up, Vector2.one };
        _quadMesh.triangles = new int[] { 0, 2, 1, 2, 3, 1 };

        // 2. 准备数据
        _matrices = new Matrix4x4[instanceCount];
        MaterialPropertyBlock block = new MaterialPropertyBlock();
        Vector4[] colors = new Vector4[instanceCount];

        for (int i = 0; i < instanceCount; i++)
        {
            Vector3 pos = new Vector3(Random.Range(-5f, 5f), Random.Range(-5f, 5f), 0);
            // 确保缩放不是 0！
            _matrices[i] = Matrix4x4.TRS(pos, Quaternion.identity, Vector3.one);
            colors[i] = new Color(Random.value, Random.value, Random.value, 1.0f);
        }

        // 把颜色塞进 block
        block.SetVectorArray("_BaseColor", colors); // URP 2D 建议先用 _BaseColor 试试

        // 3. 配置现代渲染参数
        _rp = new RenderParams(testMaterial)
        {
            worldBounds = new Bounds(Vector3.zero, Vector3.one * 1000), // 确保包围盒足够大
            matProps = block,
            receiveShadows = false,
            shadowCastingMode = ShadowCastingMode.Off
        };
    }

    void Update()
    {
        if (testMaterial == null) return;

        // 现代 API 调用方式
        // 直接传入数组，它会自动处理
        Graphics.RenderMeshInstanced(_rp, _quadMesh, 0, _matrices, instanceCount);
    }
}