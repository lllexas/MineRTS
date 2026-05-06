using System;
using UnityEngine;

public static class UnitVAPreviewMeshBuilder
{
    public static bool TryBuildFrameMesh(UnitVASO asset, UnitVAClip clip, int frameIndex, Mesh targetMesh, out string error)
    {
        if (asset == null)
        {
            error = "UnitVASO is null.";
            return false;
        }

        if (asset.Mesh == null)
        {
            error = "UnitVASO.Mesh is missing.";
            return false;
        }

        if (clip == null)
        {
            error = "Clip is null.";
            return false;
        }

        if (clip.Frames == null || clip.Frames.Count == 0)
        {
            error = "Clip has no baked frames.";
            return false;
        }

        if (targetMesh == null)
        {
            error = "Target preview mesh is null.";
            return false;
        }

        int safeFrameIndex = Mathf.Clamp(frameIndex, 0, clip.Frames.Count - 1);
        UnitVAFrame frame = clip.Frames[safeFrameIndex];
        Vector2[] positions = frame?.Positions;
        if (positions == null || positions.Length == 0)
        {
            error = $"Frame {safeFrameIndex} has no positions.";
            return false;
        }

        Vector2[] sourceUv = asset.Mesh.uv;
        int[] sourceTriangles = asset.Mesh.triangles;
        if (sourceUv == null || sourceUv.Length != positions.Length)
        {
            error = $"Mesh uv count ({sourceUv?.Length ?? 0}) does not match frame position count ({positions.Length}).";
            return false;
        }

        Vector3[] vertices = new Vector3[positions.Length];
        for (int i = 0; i < positions.Length; i++)
        {
            Vector2 position = positions[i];
            vertices[i] = new Vector3(position.x, position.y, 0f);
        }

        targetMesh.Clear();
        targetMesh.name = $"{asset.name}_{clip.SourceAnimationName}_Preview";
        targetMesh.vertices = vertices;
        targetMesh.uv = sourceUv;
        targetMesh.triangles = sourceTriangles ?? Array.Empty<int>();
        targetMesh.RecalculateBounds();

        error = string.Empty;
        return true;
    }
}
