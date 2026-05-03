using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public enum InStageRenderBand
{
    Conveyor,
    Item,
    Unit
}

public struct SpriteInstanceDrawRequest
{
    public int SpriteId;
    public Matrix4x4 Matrix;
    public InStageRenderBand Band;
}

public class SpriteInstanceRenderService
{
    public static SpriteInstanceRenderService Shared { get; } = new SpriteInstanceRenderService();

    private readonly Dictionary<BatchKey, List<Matrix4x4>> _batches = new Dictionary<BatchKey, List<Matrix4x4>>();
    private readonly Dictionary<MaterialCacheKey, Material> _materialCache = new Dictionary<MaterialCacheKey, Material>();
    private readonly MaterialPropertyBlock _propertyBlock = new MaterialPropertyBlock();

    public void Clear()
    {
        foreach (var batch in _batches.Values)
        {
            batch.Clear();
        }
    }

    public void Enqueue(SpriteInstanceDrawRequest request)
    {
        if (request.SpriteId < 0)
        {
            return;
        }

        BatchKey key = new BatchKey(request.SpriteId, request.Band);
        if (!_batches.TryGetValue(key, out List<Matrix4x4> matrices))
        {
            matrices = new List<Matrix4x4>(1024);
            _batches[key] = matrices;
        }

        matrices.Add(request.Matrix);
    }

    public void Flush(SpriteLib spriteLib)
    {
        if (spriteLib == null)
        {
            return;
        }

        _propertyBlock.Clear();
        _propertyBlock.SetVector("_BaseColor", Color.white);
        _propertyBlock.SetVector("_Color", Color.white);

        foreach (InStageRenderBand band in Enum.GetValues(typeof(InStageRenderBand)))
        {
            RenderBand(spriteLib, band);
        }
    }

    private void RenderBand(SpriteLib spriteLib, InStageRenderBand band)
    {
        (int renderQueue, int rendererPriority) = GetBandRenderSettings(band);

        foreach (var kvp in _batches)
        {
            if (kvp.Key.Band != band || kvp.Value.Count == 0)
            {
                continue;
            }

            Material baseMat = spriteLib.GetMaterial(kvp.Key.SpriteId);
            Mesh mesh = spriteLib.GetMesh(kvp.Key.SpriteId);
            if (baseMat == null || mesh == null)
            {
                continue;
            }

            Material material = GetOrCreateMaterial(kvp.Key.SpriteId, renderQueue, baseMat);
            RenderParams rp = new RenderParams(material)
            {
                worldBounds = new Bounds(Vector3.zero, Vector3.one * 10000),
                shadowCastingMode = ShadowCastingMode.Off,
                receiveShadows = false,
                matProps = _propertyBlock,
                rendererPriority = rendererPriority
            };

            Graphics.RenderMeshInstanced(rp, mesh, 0, kvp.Value);
        }
    }

    private Material GetOrCreateMaterial(int spriteId, int renderQueue, Material baseMat)
    {
        MaterialCacheKey key = new MaterialCacheKey(spriteId, renderQueue);
        if (_materialCache.TryGetValue(key, out Material material))
        {
            return material;
        }

        material = new Material(baseMat);
        material.renderQueue = renderQueue;
        _materialCache[key] = material;
        return material;
    }

    private static (int renderQueue, int rendererPriority) GetBandRenderSettings(InStageRenderBand band)
    {
        switch (band)
        {
            case InStageRenderBand.Conveyor:
                return (3000, 10);
            case InStageRenderBand.Item:
                return (3005, 20);
            case InStageRenderBand.Unit:
            default:
                return (3010, 30);
        }
    }

    private readonly struct BatchKey : IEquatable<BatchKey>
    {
        public BatchKey(int spriteId, InStageRenderBand band)
        {
            SpriteId = spriteId;
            Band = band;
        }

        public int SpriteId { get; }
        public InStageRenderBand Band { get; }

        public bool Equals(BatchKey other)
        {
            return SpriteId == other.SpriteId && Band == other.Band;
        }

        public override bool Equals(object obj)
        {
            return obj is BatchKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(SpriteId, (int)Band);
        }
    }

    private readonly struct MaterialCacheKey : IEquatable<MaterialCacheKey>
    {
        public MaterialCacheKey(int spriteId, int renderQueue)
        {
            SpriteId = spriteId;
            RenderQueue = renderQueue;
        }

        public int SpriteId { get; }
        public int RenderQueue { get; }

        public bool Equals(MaterialCacheKey other)
        {
            return SpriteId == other.SpriteId && RenderQueue == other.RenderQueue;
        }

        public override bool Equals(object obj)
        {
            return obj is MaterialCacheKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(SpriteId, RenderQueue);
        }
    }
}
