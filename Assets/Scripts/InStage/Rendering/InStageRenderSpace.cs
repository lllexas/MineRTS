using UnityEngine;

public enum InStageRenderLayoutMode
{
    Flat2D,
    Billboard3D
}

public static class InStageRenderSpace
{
    public static InStageRenderLayoutMode LayoutMode { get; set; } = InStageRenderLayoutMode.Flat2D;

    public static Vector3 LogicToWorld(Vector2 logicPos, float layerOffset = 0f)
    {
        return new Vector3(logicPos.x, logicPos.y, layerOffset);
    }

    public static Quaternion GetBillboardRotation(Camera camera)
    {
        if (camera == null)
        {
            return Quaternion.identity;
        }

        Vector3 forward = camera.transform.forward;
        Vector3 up = camera.transform.up;
        return Quaternion.LookRotation(forward, up);
    }

    public static Matrix4x4 MakeSpriteMatrix(Vector2 logicPos, Quaternion rotation, Vector3 scale, float layerOffset = 0f)
    {
        Vector3 worldPos = LogicToWorld(logicPos, layerOffset);
        return Matrix4x4.TRS(worldPos, rotation, scale);
    }

    public static Matrix4x4 MakeBillboardMatrix(
        Vector2 logicPos,
        Vector3 scale,
        Camera camera,
        float heightOffset = 0f,
        float verticalPivotOffset = 0f,
        Quaternion? extraRotation = null)
    {
        Vector3 worldPos = LogicToWorld(logicPos, heightOffset);
        worldPos.y += verticalPivotOffset;

        Quaternion billboardRotation = GetBillboardRotation(camera);
        Quaternion finalRotation = billboardRotation * (extraRotation ?? Quaternion.identity);
        return Matrix4x4.TRS(worldPos, finalRotation, scale);
    }

    public static bool TryScreenToGround(Camera camera, Vector2 screenPos, out Vector2 logicPos)
    {
        logicPos = default;
        if (camera == null)
        {
            return false;
        }

        if (!camera.orthographic)
        {
            Ray ray = camera.ScreenPointToRay(screenPos);
            Plane groundPlane = new Plane(Vector3.forward, Vector3.zero);
            if (!groundPlane.Raycast(ray, out float enter))
            {
                return false;
            }

            Vector3 worldPos = ray.GetPoint(enter);
            logicPos = new Vector2(worldPos.x, worldPos.y);
            return true;
        }

        Vector3 mousePos = new Vector3(screenPos.x, screenPos.y, -camera.transform.position.z);
        Vector3 world = camera.ScreenToWorldPoint(mousePos);
        logicPos = new Vector2(world.x, world.y);
        return true;
    }
}
