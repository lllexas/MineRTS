using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 资产库：管理 Sprite、生成对应的 UV Mesh 和开启了 Instancing 的材质
/// </summary>
public class SpriteLib : SingletonMono<SpriteLib>
{
    [Header("配置")]
    // 在 Inspector 里拖入所有的单位 Sprite
    public List<Sprite> unitSprites = new List<Sprite>();
    [Header("核心 Shader")]
    public Shader instancingShader; // 👉 在 Inspector 里把 "Custom/SimpleInstancing" 拖进来！
    // 内部缓存
    private Material[] _instancedMaterials;
    private Mesh[] _instancedMeshes;

    protected override void Awake()
    {
        base.Awake();
        Initialize();
    }

    private void Initialize()
    {
        // 1. 获取我们那个特制的 Shader
        Shader targetShader = instancingShader != null ? instancingShader : Shader.Find("Custom/SimpleInstancing");
        if (targetShader == null) targetShader = Shader.Find("Sprites/Default");

        int count = unitSprites.Count;
        _instancedMaterials = new Material[count];
        _instancedMeshes = new Mesh[count];

        for (int i = 0; i < count; i++)
        {
            var sprite = unitSprites[i];
            if (sprite == null) continue;

            // 2. 创建专属材质
            // 每一个不同的 Sprite 拥有一个独立的材质实例（因为贴图不同）
            // 只要是同一个材质实例画出的 1023 个物体，都会被自动合批
            var mat = new Material(targetShader);
            mat.enableInstancing = true;
            mat.renderQueue = 4000;

            // 对应我们 Shader 里的 _MainTex
            mat.SetTexture("_MainTex", sprite.texture);

            _instancedMaterials[i] = mat;

            // 3. 创建专属 Mesh (根据 Sprite 在图集中的位置裁切 UV)
            _instancedMeshes[i] = CreateMeshForSprite(sprite);
        }
    }

    private Mesh CreateMeshForSprite(Sprite sprite)
    {
        Mesh mesh = new Mesh();
        mesh.name = sprite != null ? $"{sprite.name}_Mesh" : "Null_Mesh";

        // --- 【修改点：根据 PPU 计算真实尺寸】 ---
        float width, height;
        if (sprite != null)
        {
            // 核心公式：实际单位尺寸 = 像素宽度 / PPU
            width = sprite.rect.width / sprite.pixelsPerUnit;
            height = sprite.rect.height / sprite.pixelsPerUnit;
        }
        else
        {
            width = 1f;
            height = 1f;
        }

        float halfW = width * 0.5f;
        float halfH = height * 0.5f;

        // A. 顶点 (不再是固定 0.5，而是根据 PPU 计算出的尺寸)
        Vector3[] vertices = new Vector3[]
        {
        new Vector3(-halfW, -halfH, 0), // 左下
        new Vector3( halfW, -halfH, 0), // 右下
        new Vector3(-halfW,  halfH, 0), // 左上
        new Vector3( halfW,  halfH, 0)  // 右上
        };
        // ---------------------------------------

        // B. UV 计算 (保持不变)
        Vector2[] uvs;
        if (sprite != null)
        {
            Rect rect = sprite.textureRect;
            float texWidth = sprite.texture.width;
            float texHeight = sprite.texture.height;

            float xMin = rect.xMin / texWidth;
            float xMax = rect.xMax / texWidth;
            float yMin = rect.yMin / texHeight;
            float yMax = rect.yMax / texHeight;

            uvs = new Vector2[]
            {
            new Vector2(xMin, yMin),
            new Vector2(xMax, yMin),
            new Vector2(xMin, yMax),
            new Vector2(xMax, yMax)
            };
        }
        else
        {
            uvs = new Vector2[] { Vector2.zero, Vector2.right, Vector2.up, Vector2.one };
        }

        mesh.vertices = vertices;
        mesh.uv = uvs;
        mesh.triangles = new int[] { 0, 2, 1, 2, 3, 1 };

        // 重新计算包围盒，防止裁剪错误
        mesh.RecalculateBounds();

        return mesh;
    }

    public Material GetMaterial(int spriteId)
    {
        if (spriteId >= 0 && spriteId < _instancedMaterials.Length) return _instancedMaterials[spriteId];
        return null;
    }

    public Mesh GetMesh(int spriteId)
    {
        if (spriteId >= 0 && spriteId < _instancedMeshes.Length) return _instancedMeshes[spriteId];
        return null;
    }
}