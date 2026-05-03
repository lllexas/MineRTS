using UnityEngine;

public enum CameraProjectionMode
{
    Orthographic,
    Perspective
}

public enum CameraControlMode
{
    Flat2D,
    Billboard3D
}

[CreateAssetMenu(fileName = "CameraProfileSO", menuName = "MineRTS/Camera/Camera Profile")]
public class CameraProfileSO : ScriptableObject
{
    [Header("Identity")]
    public string ProfileId = "NewCameraProfile";

    [Header("Projection")]
    public CameraProjectionMode ProjectionMode = CameraProjectionMode.Orthographic;
    public CameraControlMode ControlMode = CameraControlMode.Flat2D;
    public Vector3 Position = new Vector3(0f, 0f, -10f);
    public Vector3 RotationEuler = Vector3.zero;
    public float OrthographicSize = 8f;
    public float FieldOfView = 40f;
    public float NearClipPlane = 0.3f;
    public float FarClipPlane = 1000f;

    [Header("Navigation")]
    public float MoveSpeed = 25f;
    public float DragSpeed = 1.0f;
    public float EdgeSize = 10f;
    public bool UseEdgeScrolling = true;

    [Header("Zoom")]
    public float DefaultZoom = 8f;
    public float MinZoom = 2f;
    public float MaxZoom = 18f;
    public float ZoomSensitivity = 10f;

    [Header("Smoothing")]
    public float LerpSpeed = 100f;

    public void ApplyTo(Camera camera, CameraController controller)
    {
        if (camera == null || controller == null)
        {
            return;
        }

        controller.ApplyProfile(this);
    }

    public void CaptureFrom(Camera camera, CameraController controller)
    {
        if (camera == null || controller == null)
        {
            return;
        }

        ProjectionMode = camera.orthographic ? CameraProjectionMode.Orthographic : CameraProjectionMode.Perspective;
        ControlMode = controller.ControlMode;
        Position = camera.transform.position;
        RotationEuler = camera.transform.eulerAngles;
        OrthographicSize = camera.orthographicSize;
        FieldOfView = camera.fieldOfView;
        NearClipPlane = camera.nearClipPlane;
        FarClipPlane = camera.farClipPlane;

        MoveSpeed = controller.moveSpeed;
        DragSpeed = controller.dragSpeed;
        EdgeSize = controller.edgeSize;
        UseEdgeScrolling = controller.useEdgeScrolling;
        DefaultZoom = controller.defaultZoom;
        MinZoom = controller.minZoom;
        MaxZoom = controller.maxZoom;
        ZoomSensitivity = controller.zoomSensitivity;
        LerpSpeed = controller.lerpSpeed;
    }
}
