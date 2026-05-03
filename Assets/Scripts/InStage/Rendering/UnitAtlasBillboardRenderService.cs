using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public struct UnitAtlasBillboardDrawRequest
{
    public Texture2D AtlasTexture;
    public Rect UvRect;
    public Matrix4x4 Matrix;
}

public sealed class UnitAtlasBillboardRenderService
{
    public static UnitAtlasBillboardRenderService Shared { get; } = new UnitAtlasBillboardRenderService();

    private const int MaxInstancesPerDraw = 1023;
    private const int UnitRenderQueue = 3010;
    private const int UnitRendererPriority = 30;

    private readonly Dictionary<Texture2D, BatchData> _batches = new Dictionary<Texture2D, BatchData>();
    private readonly Dictionary<Texture2D, Material> _materialCache = new Dictionary<Texture2D, Material>();
    private readonly MaterialPropertyBlock _propertyBlock = new MaterialPropertyBlock();

    private Shader _shader;
    private Mesh _unitBottomPivotQuadMesh;

    public void Clear()
    {
        foreach (BatchData batch in _batches.Values)
        {
            batch.Matrices.Clear();
            batch.UvRects.Clear();
        }
    }

    public void Enqueue(UnitAtlasBillboardDrawRequest request)
    {
        if (request.AtlasTexture == null)
        {
            return;
        }

        if (!_batches.TryGetValue(request.AtlasTexture, out BatchData batch))
        {
            batch = new BatchData();
            _batches.Add(request.AtlasTexture, batch);
        }

        batch.Matrices.Add(request.Matrix);
        batch.UvRects.Add(new Vector4(request.UvRect.x, request.UvRect.y, request.UvRect.width, request.UvRect.height));
    }

    public void Flush()
    {
        Mesh mesh = GetOrCreateUnitBottomPivotQuadMesh();
        Shader shader = GetShader();
        if (mesh == null || shader == null)
        {
            return;
        }

        foreach ((Texture2D atlasTexture, BatchData batch) in _batches)
        {
            if (atlasTexture == null || batch.Matrices.Count == 0)
            {
                continue;
            }

            Material material = GetOrCreateMaterial(atlasTexture, shader);
            if (material == null)
            {
                continue;
            }

            RenderParams renderParams = new RenderParams(material)
            {
                worldBounds = new Bounds(Vector3.zero, Vector3.one * 10000f),
                shadowCastingMode = ShadowCastingMode.Off,
                receiveShadows = false,
                matProps = _propertyBlock,
                rendererPriority = UnitRendererPriority
            };

            for (int start = 0; start < batch.Matrices.Count; start += MaxInstancesPerDraw)
            {
                int count = Mathf.Min(MaxInstancesPerDraw, batch.Matrices.Count - start);
                List<Matrix4x4> matricesSlice = batch.Matrices.GetRange(start, count);
                List<Vector4> uvRectsSlice = batch.UvRects.GetRange(start, count);

                _propertyBlock.Clear();
                _propertyBlock.SetVectorArray("_UvRect", uvRectsSlice);
                _propertyBlock.SetVector("_BaseColor", Color.white);
                _propertyBlock.SetVector("_Color", Color.white);

                Graphics.RenderMeshInstanced(renderParams, mesh, 0, matricesSlice);
            }
        }
    }

    private Shader GetShader()
    {
        if (_shader == null)
        {
            _shader = Shader.Find("Custom/AtlasBillboardInstancing");
        }

        return _shader;
    }

    private Material GetOrCreateMaterial(Texture2D atlasTexture, Shader shader)
    {
        if (_materialCache.TryGetValue(atlasTexture, out Material material) && material != null)
        {
            return material;
        }

        material = new Material(shader);
        material.enableInstancing = true;
        material.renderQueue = UnitRenderQueue;
        material.SetTexture("_MainTex", atlasTexture);

        _materialCache[atlasTexture] = material;
        return material;
    }

    private Mesh GetOrCreateUnitBottomPivotQuadMesh()
    {
        if (_unitBottomPivotQuadMesh != null)
        {
            return _unitBottomPivotQuadMesh;
        }

        _unitBottomPivotQuadMesh = new Mesh
        {
            name = "UnitBottomPivotQuad"
        };

        _unitBottomPivotQuadMesh.vertices = new[]
        {
            new Vector3(-0.5f, 0f, 0f),
            new Vector3(0.5f, 0f, 0f),
            new Vector3(-0.5f, 1f, 0f),
            new Vector3(0.5f, 1f, 0f)
        };

        _unitBottomPivotQuadMesh.uv = new[]
        {
            new Vector2(0f, 0f),
            new Vector2(1f, 0f),
            new Vector2(0f, 1f),
            new Vector2(1f, 1f)
        };

        _unitBottomPivotQuadMesh.triangles = new[] { 0, 2, 1, 2, 3, 1 };
        _unitBottomPivotQuadMesh.RecalculateBounds();

        return _unitBottomPivotQuadMesh;
    }

    private sealed class BatchData
    {
        public List<Matrix4x4> Matrices { get; } = new List<Matrix4x4>(1024);
        public List<Vector4> UvRects { get; } = new List<Vector4>(1024);
    }
}
