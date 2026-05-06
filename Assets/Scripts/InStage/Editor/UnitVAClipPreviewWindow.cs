using UnityEditor;
using UnityEngine;

public sealed class UnitVAClipPreviewWindow : EditorWindow
{
    private const float MinOrthoSize = 1f;
    private const float PreviewPadding = 1.25f;
    private static bool s_closingFromSession;

    private PreviewRenderUtility _previewUtility;
    private Mesh _previewMesh;
    private Mesh _boundsMesh;
    private Material _previewMaterial;
    private double _lastUpdateTime;
    private double _frameAccumulator;
    private UnitVASO _lastBoundsAsset;
    private UnitVAClip _lastBoundsClip;
    private Bounds _clipBounds;
    private string _lastError;

    public static void ShowWindow()
    {
        UnitVAClipPreviewWindow window = GetWindow<UnitVAClipPreviewWindow>("Unit VA Clip Preview");
        window.minSize = new Vector2(360f, 260f);
        window.MoveNearSceneView();
        window.Show();
        window.Focus();
        window.Repaint();
    }

    public static void CloseIfOpen()
    {
        UnitVAClipPreviewWindow window = HasOpenInstances<UnitVAClipPreviewWindow>()
            ? GetWindow<UnitVAClipPreviewWindow>("Unit VA Clip Preview", false)
            : null;

        if (window != null)
        {
            s_closingFromSession = true;
            window.Close();
            s_closingFromSession = false;
        }
    }

    private void OnEnable()
    {
        UnitVAClipPreviewSession.Changed += Repaint;
        _lastUpdateTime = EditorApplication.timeSinceStartup;
    }

    private void OnDisable()
    {
        UnitVAClipPreviewSession.Changed -= Repaint;
        DisposePreviewResources();

        if (!s_closingFromSession && UnitVAClipPreviewSession.IsActive)
        {
            UnitVAClipPreviewSession.CloseFromWindow();
        }
    }

    private void Update()
    {
        if (!UnitVAClipPreviewSession.IsActive)
        {
            return;
        }

        UnitVAClip clip = UnitVAClipPreviewSession.GetActiveClip();
        if (clip?.Frames == null || clip.Frames.Count == 0)
        {
            return;
        }

        double now = EditorApplication.timeSinceStartup;
        double delta = now - _lastUpdateTime;
        _lastUpdateTime = now;

        if (UnitVAClipPreviewSession.IsPlaying)
        {
            float fps = UnitVASettings.NormalizeBakeSampleFps(UnitVAClipPreviewSession.ActiveAsset.BakeSampleFps);
            _frameAccumulator += delta * fps * Mathf.Max(0.01f, UnitVAClipPreviewSession.PlaybackSpeed);
            int wholeFrames = Mathf.FloorToInt((float)_frameAccumulator);
            if (wholeFrames >= 1)
            {
                _frameAccumulator -= wholeFrames;
                int nextFrame = UnitVAClipPreviewSession.CurrentFrame + wholeFrames;
                if (clip.Loop)
                {
                    UnitVAClipPreviewSession.CurrentFrame = nextFrame % clip.Frames.Count;
                }
                else
                {
                    UnitVAClipPreviewSession.CurrentFrame = Mathf.Min(nextFrame, clip.Frames.Count - 1);
                    if (UnitVAClipPreviewSession.CurrentFrame >= clip.Frames.Count - 1)
                    {
                        UnitVAClipPreviewSession.IsPlaying = false;
                        _frameAccumulator = 0d;
                    }
                }

                UnitVAClipPreviewSession.NotifyChanged();
                Repaint();
            }
        }
        else
        {
            _frameAccumulator = 0d;
        }
    }

    private void OnGUI()
    {
        UnitVASO asset = UnitVAClipPreviewSession.ActiveAsset;
        UnitVAClip clip = UnitVAClipPreviewSession.GetActiveClip();
        if (asset == null || clip == null)
        {
            EditorGUILayout.HelpBox("No active UnitVA clip preview.", MessageType.Info);
            if (GUILayout.Button("Close"))
            {
                Close();
            }
            return;
        }

        DrawToolbar(asset, clip);

        Rect previewRect = GUILayoutUtility.GetRect(1f, 1f, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
        DrawPreview(previewRect, asset, clip);
    }

    private void DrawToolbar(UnitVASO asset, UnitVAClip clip)
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
        GUILayout.Label($"{asset.name} / {clip.SourceAnimationName}", EditorStyles.boldLabel);
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("Close Preview", EditorStyles.toolbarButton, GUILayout.Width(100f)))
        {
            UnitVAClipPreviewSession.Close();
        }
        EditorGUILayout.EndHorizontal();
    }

    private void DrawPreview(Rect rect, UnitVASO asset, UnitVAClip clip)
    {
        EnsurePreviewResources(asset);

        if (!UnitVAPreviewMeshBuilder.TryBuildFrameMesh(
                asset,
                clip,
                UnitVAClipPreviewSession.CurrentFrame,
                _previewMesh,
                out _lastError))
        {
            EditorGUI.HelpBox(rect, _lastError, MessageType.Warning);
            return;
        }

        _previewUtility.BeginPreview(rect, GUIStyle.none);
        _previewUtility.camera.clearFlags = CameraClearFlags.Color;
        _previewUtility.camera.backgroundColor = new Color(0.12f, 0.12f, 0.12f, 1f);
        _previewUtility.camera.orthographic = true;
        if (!TryGetClipBounds(asset, clip, out Bounds clipBounds))
        {
            EditorGUI.HelpBox(rect, _lastError, MessageType.Warning);
            return;
        }

        ConfigureCamera(_previewUtility.camera, clipBounds, rect);

        _previewMaterial.mainTexture = asset.BaseTexture;
        _previewUtility.DrawMesh(_previewMesh, Matrix4x4.identity, _previewMaterial, 0);
        _previewUtility.camera.Render();

        Texture texture = _previewUtility.EndPreview();
        GUI.DrawTexture(rect, texture, ScaleMode.StretchToFill, false);

        Rect labelRect = new Rect(rect.x + 8f, rect.y + 8f, rect.width - 16f, 20f);
        EditorGUI.DropShadowLabel(labelRect, $"Frame {UnitVAClipPreviewSession.CurrentFrame + 1} / {clip.Frames.Count}");
    }

    private void EnsurePreviewResources(UnitVASO asset)
    {
        _previewUtility ??= new PreviewRenderUtility(true);
        _previewMesh ??= new Mesh { hideFlags = HideFlags.HideAndDontSave };
        _boundsMesh ??= new Mesh { hideFlags = HideFlags.HideAndDontSave };

        if (_previewMaterial == null)
        {
            Shader shader = Shader.Find("Unlit/Transparent");
            if (shader == null)
            {
                shader = Shader.Find("Sprites/Default");
            }

            _previewMaterial = new Material(shader)
            {
                hideFlags = HideFlags.HideAndDontSave,
                mainTexture = asset.BaseTexture
            };
        }
        else
        {
            _previewMaterial.mainTexture = asset.BaseTexture;
        }
    }

    private static void ConfigureCamera(Camera camera, Bounds bounds, Rect rect)
    {
        Vector3 center = bounds.center;
        float aspect = Mathf.Max(0.1f, rect.width / Mathf.Max(1f, rect.height));
        float halfHeight = Mathf.Max(bounds.extents.y, bounds.extents.x / aspect) * PreviewPadding;
        camera.orthographicSize = Mathf.Max(MinOrthoSize, halfHeight);
        camera.transform.position = new Vector3(center.x, center.y, -10f);
        camera.transform.rotation = Quaternion.identity;
        camera.nearClipPlane = 0.01f;
        camera.farClipPlane = 100f;
    }

    private bool TryGetClipBounds(UnitVASO asset, UnitVAClip clip, out Bounds bounds)
    {
        if (_lastBoundsAsset == asset && _lastBoundsClip == clip)
        {
            bounds = _clipBounds;
            return true;
        }

        _lastBoundsAsset = null;
        _lastBoundsClip = null;
        _clipBounds = default;

        if (clip?.Frames == null || clip.Frames.Count == 0)
        {
            _lastError = "Clip has no frames.";
            bounds = default;
            return false;
        }

        bool hasBounds = false;
        Bounds combinedBounds = default;
        for (int i = 0; i < clip.Frames.Count; i++)
        {
            if (!UnitVAPreviewMeshBuilder.TryBuildFrameMesh(asset, clip, i, _boundsMesh, out _lastError))
            {
                bounds = default;
                return false;
            }

            if (!hasBounds)
            {
                combinedBounds = _boundsMesh.bounds;
                hasBounds = true;
            }
            else
            {
                combinedBounds.Encapsulate(_boundsMesh.bounds);
            }
        }

        _lastBoundsAsset = asset;
        _lastBoundsClip = clip;
        _clipBounds = combinedBounds;
        bounds = _clipBounds;
        return true;
    }

    private void MoveNearSceneView()
    {
        SceneView sceneView = SceneView.lastActiveSceneView;
        if (sceneView == null)
        {
            return;
        }

        Rect sceneRect = sceneView.position;
        position = new Rect(
            sceneRect.x + 32f,
            sceneRect.y + 32f,
            Mathf.Max(420f, sceneRect.width * 0.45f),
            Mathf.Max(320f, sceneRect.height * 0.45f));
    }

    private void DisposePreviewResources()
    {
        _previewUtility?.Cleanup();
        _previewUtility = null;

        if (_previewMesh != null)
        {
            DestroyImmediate(_previewMesh);
            _previewMesh = null;
        }

        if (_boundsMesh != null)
        {
            DestroyImmediate(_boundsMesh);
            _boundsMesh = null;
        }

        if (_previewMaterial != null)
        {
            DestroyImmediate(_previewMaterial);
            _previewMaterial = null;
        }
    }
}
