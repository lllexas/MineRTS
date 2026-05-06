using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(UnitVASO))]
public sealed class UnitVASOEditor : Editor
{
    private bool _showIdentity = true;
    private bool _showSource = true;
    private bool _showStaticAssets = true;
    private bool _showClips = true;
    private bool _showDiagnostics = true;
    private Vector2 _previewFrameScroll;

    private void OnEnable()
    {
        UnitVAClipPreviewSession.Changed += Repaint;
    }

    private void OnDisable()
    {
        UnitVAClipPreviewSession.Changed -= Repaint;
    }

    public override void OnInspectorGUI()
    {
        UnitVASO unitVA = (UnitVASO)target;
        if (UnitVAClipPreviewSession.IsActive && UnitVAClipPreviewSession.ActiveAsset == unitVA)
        {
            DrawDedicatedPreviewInspector(unitVA);
            return;
        }

        serializedObject.Update();

        DrawIdentitySection();
        DrawSourceSection();
        DrawStaticAssetsSection();
        DrawClipsSection();
        DrawDiagnosticsSection();

        serializedObject.ApplyModifiedProperties();
    }

    private void DrawIdentitySection()
    {
        _showIdentity = EditorGUILayout.BeginFoldoutHeaderGroup(_showIdentity, "Identity");
        if (_showIdentity)
        {
            DrawProperty("UnitTypeId");
        }

        EditorGUILayout.EndFoldoutHeaderGroup();
        EditorGUILayout.Space(4f);
    }

    private void DrawSourceSection()
    {
        _showSource = EditorGUILayout.BeginFoldoutHeaderGroup(_showSource, "Source");
        if (_showSource)
        {
            DrawProperty("SourceJson");
            DrawProperty("SourceSkeletonDataAsset");
            DrawProperty("SourceAssetPath");
            DrawProperty("SourceAssetGuid");
            DrawProperty("SourceSpineVersion");
            DrawProperty("BakeSampleFps");
            DrawProperty("FlipHorizontal");

            UnitVASO unitVA = (UnitVASO)target;
            EditorGUILayout.BeginHorizontal();
            using (new EditorGUI.DisabledScope(unitVA.SourceJson == null))
            {
                if (GUILayout.Button("Refresh From Source JSON"))
                {
                    RefreshFromSourceJson(unitVA);
                }
            }

            using (new EditorGUI.DisabledScope(string.IsNullOrEmpty(unitVA.SourceAssetPath)))
            {
                if (GUILayout.Button("Ping Source"))
                {
                    UnityEngine.Object source = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(unitVA.SourceAssetPath);
                    if (source != null)
                    {
                        EditorGUIUtility.PingObject(source);
                    }
                }
            }
            EditorGUILayout.EndHorizontal();

            using (new EditorGUI.DisabledScope(unitVA.SourceSkeletonDataAsset == null || unitVA.Clips == null || unitVA.Clips.Count == 0))
            {
                if (GUILayout.Button("Bake From Spine"))
                {
                    BakeFromSpine(unitVA);
                }
            }
        }

        EditorGUILayout.EndFoldoutHeaderGroup();
        EditorGUILayout.Space(4f);
    }

    private void DrawStaticAssetsSection()
    {
        _showStaticAssets = EditorGUILayout.BeginFoldoutHeaderGroup(_showStaticAssets, "Static Assets");
        if (_showStaticAssets)
        {
            DrawProperty("Mesh");
            DrawProperty("BaseTexture");
            DrawProperty("DisplayScale");
            DrawProperty("VertexCount");

            UnitVASO unitVA = (UnitVASO)target;
            if (unitVA.Mesh != null && unitVA.VertexCount != unitVA.Mesh.vertexCount)
            {
                EditorGUILayout.HelpBox(
                    $"VertexCount ({unitVA.VertexCount}) does not match Mesh.vertexCount ({unitVA.Mesh.vertexCount}). OnValidate will use the mesh count.",
                    MessageType.Warning);
            }
        }

        EditorGUILayout.EndFoldoutHeaderGroup();
        EditorGUILayout.Space(4f);
    }

    private void DrawClipsSection()
    {
        _showClips = EditorGUILayout.BeginFoldoutHeaderGroup(_showClips, "Clips");
        if (_showClips)
        {
            SerializedProperty clipsProperty = serializedObject.FindProperty("Clips");
            UnitVASO unitVA = (UnitVASO)target;

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Clip Count", clipsProperty.arraySize.ToString());
            if (GUILayout.Button("Add Clip", GUILayout.Width(90f)))
            {
                int index = clipsProperty.arraySize;
                clipsProperty.InsertArrayElementAtIndex(index);
                SerializedProperty clipProperty = clipsProperty.GetArrayElementAtIndex(index);
                clipProperty.FindPropertyRelative("SourceAnimationName").stringValue = string.Empty;
                clipProperty.FindPropertyRelative("State").enumValueIndex = (int)UnitAnimationStateId.None;
                clipProperty.FindPropertyRelative("TicksPerFrame").intValue = 1;
                clipProperty.FindPropertyRelative("Loop").boolValue = true;
                clipProperty.FindPropertyRelative("Frames").arraySize = 0;
            }
            EditorGUILayout.EndHorizontal();

            for (int i = 0; i < clipsProperty.arraySize; i++)
            {
                SerializedProperty clipProperty = clipsProperty.GetArrayElementAtIndex(i);
                SerializedProperty sourceNameProperty = clipProperty.FindPropertyRelative("SourceAnimationName");
                SerializedProperty stateProperty = clipProperty.FindPropertyRelative("State");
                SerializedProperty ticksProperty = clipProperty.FindPropertyRelative("TicksPerFrame");
                SerializedProperty loopProperty = clipProperty.FindPropertyRelative("Loop");
                SerializedProperty lockProperty = clipProperty.FindPropertyRelative("LockUntilComplete");
                SerializedProperty framesProperty = clipProperty.FindPropertyRelative("Frames");

                string title = string.IsNullOrEmpty(sourceNameProperty.stringValue)
                    ? $"Clip {i}"
                    : $"Clip {i}: {sourceNameProperty.stringValue}";

                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.BeginHorizontal();
                DrawClipPreviewButton(unitVA, i);
                EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Remove", GUILayout.Width(70f)))
                {
                    clipsProperty.DeleteArrayElementAtIndex(i);
                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.EndVertical();
                    break;
                }
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.PropertyField(sourceNameProperty);
                EditorGUILayout.PropertyField(stateProperty);
                EditorGUILayout.PropertyField(ticksProperty);
                EditorGUILayout.PropertyField(loopProperty);
                EditorGUILayout.PropertyField(lockProperty);

                EditorGUILayout.LabelField("Frame Count", framesProperty.arraySize.ToString());
                DrawBakeFrameExpectation(
                    unitVA,
                    sourceNameProperty.stringValue,
                    loopProperty.boolValue,
                    framesProperty.arraySize);
                DrawFrameSummary(framesProperty, unitVA.VertexCount);
                EditorGUILayout.EndVertical();
            }
        }

        EditorGUILayout.EndFoldoutHeaderGroup();
        EditorGUILayout.Space(4f);
    }

    private static void DrawBakeFrameExpectation(UnitVASO unitVA, string sourceAnimationName, bool loop, int bakedFrameCount)
    {
        if (!TryGetExpectedBakeFrameCount(unitVA, sourceAnimationName, loop, out int expectedFrameCount, out float duration))
        {
            return;
        }

        int fps = UnitVASettings.NormalizeBakeSampleFps(unitVA.BakeSampleFps);
        EditorGUILayout.LabelField("Source", FormatSourceTimeline(duration, fps));
        EditorGUILayout.LabelField("Bake", FormatBakeTimeline(expectedFrameCount, loop));
        if (bakedFrameCount > 0 && bakedFrameCount != expectedFrameCount)
        {
            EditorGUILayout.HelpBox(
                $"Current bake has {bakedFrameCount} poses; expected {expectedFrameCount}. Re-bake this UnitVASO.",
                MessageType.Warning);
        }
    }

    private static string FormatSourceTimeline(float duration, int fps)
    {
        float sourceEndFrame = duration * fps;
        string endFrameText = FormatFrameNumber(sourceEndFrame);
        string segmentText = FormatFrameNumber(sourceEndFrame);
        return $"{segmentText} seg @ {fps}fps = {duration:0.###}s, 0..{endFrameText}f";
    }

    private static string FormatBakeTimeline(int expectedFrameCount, bool loop)
    {
        int endPoseFrame = Mathf.Max(0, expectedFrameCount - 1);
        if (loop)
        {
            return $"{expectedFrameCount} poses, 0..{endPoseFrame}f, seam omitted";
        }

        return $"{expectedFrameCount} poses, 0..{endPoseFrame}f, endpoint kept";
    }

    private static string FormatFrameNumber(float value)
    {
        int rounded = Mathf.RoundToInt(value);
        if (Mathf.Abs(value - rounded) < 0.01f)
        {
            return rounded.ToString();
        }

        return value.ToString("0.##");
    }

    private static bool TryGetExpectedBakeFrameCount(
        UnitVASO unitVA,
        string sourceAnimationName,
        bool loop,
        out int expectedFrameCount,
        out float duration)
    {
        expectedFrameCount = 0;
        duration = 0f;
        if (unitVA == null || unitVA.SourceSkeletonDataAsset == null || string.IsNullOrWhiteSpace(sourceAnimationName))
        {
            return false;
        }

        object skeletonData = ReadSkeletonData(unitVA.SourceSkeletonDataAsset, true);
        MethodInfo findAnimation = skeletonData?.GetType().GetMethod("FindAnimation", new[] { typeof(string) });
        object animation = findAnimation?.Invoke(skeletonData, new object[] { sourceAnimationName });
        if (animation == null)
        {
            return false;
        }

        PropertyInfo durationProperty = animation.GetType().GetProperty("Duration");
        if (durationProperty == null)
        {
            return false;
        }

        duration = Convert.ToSingle(durationProperty.GetValue(animation));
        expectedFrameCount = BuildSampleTimes(duration, unitVA.BakeSampleFps, loop).Count;
        return true;
    }

    private static void DrawClipPreviewButton(UnitVASO unitVA, int clipIndex)
    {
        bool active = UnitVAClipPreviewSession.IsPreviewing(unitVA, clipIndex);
        GUIContent content = GetEyeContent(active);
        GUIStyle style = active ? EditorStyles.toolbarButton : GUI.skin.button;
        Color previousColor = GUI.backgroundColor;
        if (active)
        {
            GUI.backgroundColor = new Color(0.55f, 0.8f, 1f, 1f);
        }

        if (GUILayout.Button(content, style, GUILayout.Width(34f), GUILayout.Height(20f)))
        {
            UnitVAClipPreviewSession.Toggle(unitVA, clipIndex);
        }

        GUI.backgroundColor = previousColor;
    }

    private static GUIContent GetEyeContent(bool active)
    {
        GUIContent content = EditorGUIUtility.IconContent("animationvisibilitytoggleon");
        if (content == null || content.image == null)
        {
            return new GUIContent(active ? "On" : "View", "Toggle clip preview");
        }

        content.tooltip = "Toggle clip preview";
        return content;
    }

    private void DrawDedicatedPreviewInspector(UnitVASO unitVA)
    {
        UnitVAClip clip = UnitVAClipPreviewSession.GetActiveClip();
        if (clip == null)
        {
            EditorGUILayout.HelpBox("Active preview clip is missing.", MessageType.Warning);
            if (GUILayout.Button("Close Preview"))
            {
                UnitVAClipPreviewSession.Close();
            }
            return;
        }

        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
        GUILayout.Label("Unit VA Clip Preview", EditorStyles.boldLabel);
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("Close", EditorStyles.toolbarButton, GUILayout.Width(60f)))
        {
            UnitVAClipPreviewSession.Close();
            return;
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField("Asset", unitVA.name);
        EditorGUILayout.LabelField("Clip", clip.SourceAnimationName);
        EditorGUILayout.LabelField("State", clip.State.ToString());
        EditorGUILayout.LabelField("Frames", clip.FrameCount.ToString());
        EditorGUILayout.LabelField("Vertices", unitVA.VertexCount.ToString());
        EditorGUILayout.LabelField("Loop", clip.Loop ? "True" : "False");
        EditorGUILayout.LabelField("LockUntilComplete", clip.LockUntilComplete ? "True" : "False");

        EditorGUILayout.Space(8f);
        DrawPreviewPlaybackControls(unitVA, clip);
        EditorGUILayout.Space(8f);
        DrawPreviewFrameOrder(clip);
    }

    private static void DrawPreviewPlaybackControls(UnitVASO unitVA, UnitVAClip clip)
    {
        using (new EditorGUI.DisabledScope(clip.FrameCount == 0))
        {
            EditorGUILayout.BeginHorizontal();
            string playLabel = UnitVAClipPreviewSession.IsPlaying ? "Pause" : "Play";
            if (GUILayout.Button(playLabel, GUILayout.Width(70f)))
            {
                UnitVAClipPreviewSession.IsPlaying = !UnitVAClipPreviewSession.IsPlaying;
                UnitVAClipPreviewSession.NotifyChanged();
            }

            if (GUILayout.Button("First", GUILayout.Width(58f)))
            {
                UnitVAClipPreviewSession.CurrentFrame = 0;
                UnitVAClipPreviewSession.NotifyChanged();
            }

            if (GUILayout.Button("Last", GUILayout.Width(58f)))
            {
                UnitVAClipPreviewSession.CurrentFrame = Mathf.Max(0, clip.FrameCount - 1);
                UnitVAClipPreviewSession.NotifyChanged();
            }
            EditorGUILayout.EndHorizontal();

            int frame = Mathf.Clamp(UnitVAClipPreviewSession.CurrentFrame, 0, Mathf.Max(0, clip.FrameCount - 1));
            int nextFrame = EditorGUILayout.IntSlider("Frame", frame, 0, Mathf.Max(0, clip.FrameCount - 1));
            if (nextFrame != UnitVAClipPreviewSession.CurrentFrame)
            {
                UnitVAClipPreviewSession.CurrentFrame = nextFrame;
                UnitVAClipPreviewSession.NotifyChanged();
            }

            float speed = EditorGUILayout.Slider("Speed", UnitVAClipPreviewSession.PlaybackSpeed, 0.1f, 4f);
            if (!Mathf.Approximately(speed, UnitVAClipPreviewSession.PlaybackSpeed))
            {
                UnitVAClipPreviewSession.PlaybackSpeed = speed;
                UnitVAClipPreviewSession.NotifyChanged();
            }

            EditorGUILayout.LabelField("Sample FPS", unitVA.BakeSampleFps.ToString());
        }
    }

    private void DrawPreviewFrameOrder(UnitVAClip clip)
    {
        EditorGUILayout.LabelField("Frame Order", EditorStyles.boldLabel);
        _previewFrameScroll = EditorGUILayout.BeginScrollView(_previewFrameScroll, GUILayout.MinHeight(120f));

        const int columns = 6;
        for (int i = 0; i < clip.FrameCount; i += columns)
        {
            EditorGUILayout.BeginHorizontal();
            for (int col = 0; col < columns && i + col < clip.FrameCount; col++)
            {
                int frameIndex = i + col;
                bool active = frameIndex == UnitVAClipPreviewSession.CurrentFrame;
                Color previousColor = GUI.backgroundColor;
                if (active)
                {
                    GUI.backgroundColor = new Color(0.55f, 0.8f, 1f, 1f);
                }

                if (GUILayout.Button(frameIndex.ToString(), GUILayout.Width(42f)))
                {
                    UnitVAClipPreviewSession.CurrentFrame = frameIndex;
                    UnitVAClipPreviewSession.IsPlaying = false;
                    UnitVAClipPreviewSession.NotifyChanged();
                }

                GUI.backgroundColor = previousColor;
            }
            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.EndScrollView();
    }

    private static void DrawFrameSummary(SerializedProperty framesProperty, int vertexCount)
    {
        const int maxVisibleFrames = 8;

        if (framesProperty.arraySize == 0)
        {
            EditorGUILayout.HelpBox("No baked VA frames yet. This is expected before the Spine baker is implemented.", MessageType.Info);
            return;
        }

        int visibleCount = Mathf.Min(framesProperty.arraySize, maxVisibleFrames);
        for (int i = 0; i < visibleCount; i++)
        {
            SerializedProperty frameProperty = framesProperty.GetArrayElementAtIndex(i);
            SerializedProperty positionsProperty = frameProperty.FindPropertyRelative("Positions");
            string status = positionsProperty.arraySize == vertexCount ? "OK" : "Mismatch";
            EditorGUILayout.LabelField($"Frame {i}", $"{positionsProperty.arraySize} positions / {vertexCount} vertices ({status})");
        }

        if (framesProperty.arraySize > visibleCount)
        {
            EditorGUILayout.LabelField("...", $"{framesProperty.arraySize - visibleCount} more frames hidden");
        }
    }

    private void DrawDiagnosticsSection()
    {
        _showDiagnostics = EditorGUILayout.BeginFoldoutHeaderGroup(_showDiagnostics, "Diagnostics");
        if (_showDiagnostics)
        {
            UnitVASO unitVA = (UnitVASO)target;
            EditorGUILayout.LabelField("Total Frames", unitVA.TotalFrameCount.ToString());
            EditorGUILayout.LabelField("Expected Positions", unitVA.ExpectedPositionCount.ToString());
            EditorGUILayout.LabelField("Frame Vertex Counts", unitVA.HasExpectedFrameVertexCounts() ? "OK" : "Mismatch");

            int bakedClips = 0;
            if (unitVA.Clips != null)
            {
                for (int i = 0; i < unitVA.Clips.Count; i++)
                {
                    if (unitVA.Clips[i]?.FrameCount > 0)
                    {
                        bakedClips++;
                    }
                }
            }

            EditorGUILayout.LabelField("Baked Clips", bakedClips.ToString());
            if (unitVA.Mesh == null)
            {
                EditorGUILayout.HelpBox("Mesh is empty. Creation from Spine JSON only creates the VA container; mesh baking is the next tool stage.", MessageType.Info);
            }

            if (unitVA.BaseTexture == null)
            {
                EditorGUILayout.HelpBox("BaseTexture is empty. The creator expects a same-directory texture named like the json base name.", MessageType.Warning);
            }
        }

        EditorGUILayout.EndFoldoutHeaderGroup();
        EditorGUILayout.Space(4f);
    }

    private void DrawProperty(string propertyName)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property == null)
        {
            EditorGUILayout.HelpBox($"Missing serialized property: {propertyName}", MessageType.Warning);
            return;
        }

        EditorGUILayout.PropertyField(property);
    }

    private static void RefreshFromSourceJson(UnitVASO unitVA)
    {
        Undo.RecordObject(unitVA, "Refresh UnitVASO From Source JSON");
        ApplySourceJson(unitVA, unitVA.SourceJson, true);
        EditorUtility.SetDirty(unitVA);
        AssetDatabase.SaveAssets();
    }

    private static void BakeFromSpine(UnitVASO unitVA)
    {
        Type skeletonType = FindType("Spine.Skeleton");
        Type meshGeneratorType = FindType("Spine.Unity.MeshGenerator");
        Type instructionType = FindType("Spine.Unity.SkeletonRendererInstruction");
        if (skeletonType == null || meshGeneratorType == null || instructionType == null)
        {
            EditorUtility.DisplayDialog(
                "UnitVASO Bake Failed",
                "spine-unity runtime types were not found. Install spine-unity before baking.",
                "OK");
            return;
        }

        object skeletonData = ReadSkeletonData(unitVA.SourceSkeletonDataAsset);
        if (skeletonData == null)
        {
            EditorUtility.DisplayDialog(
                "UnitVASO Bake Failed",
                "SourceSkeletonDataAsset did not return valid SkeletonData.",
                "OK");
            return;
        }

        object skeleton = Activator.CreateInstance(skeletonType, skeletonData);
        object meshGenerator = Activator.CreateInstance(meshGeneratorType);
        object instruction = Activator.CreateInstance(instructionType);
        MethodInfo setToSetupPose = skeletonType.GetMethod("SetToSetupPose", Type.EmptyTypes);
        MethodInfo updateWorldTransform = skeletonType.GetMethod("UpdateWorldTransform", Type.EmptyTypes);
        MethodInfo findAnimation = skeletonData.GetType().GetMethod("FindAnimation", new[] { typeof(string) });

        if (setToSetupPose == null || updateWorldTransform == null || findAnimation == null)
        {
            EditorUtility.DisplayDialog(
                "UnitVASO Bake Failed",
                "Required Spine SkeletonData/Skeleton methods were not found.",
                "OK");
            return;
        }

        List<UnitVAClip> bakedClips = new List<UnitVAClip>();
        Mesh baseMesh = null;
        int[] baseTriangles = null;
        Vector2[] baseUvs = null;
        int vertexCount = 0;
        int bakedFrameCount = 0;

        try
        {
            for (int clipIndex = 0; clipIndex < unitVA.Clips.Count; clipIndex++)
            {
                UnitVAClip sourceClip = unitVA.Clips[clipIndex];
                if (sourceClip == null || string.IsNullOrWhiteSpace(sourceClip.SourceAnimationName))
                {
                    continue;
                }

                object animation = findAnimation.Invoke(skeletonData, new object[] { sourceClip.SourceAnimationName });
                if (animation == null)
                {
                    Debug.LogWarning($"UnitVASO bake skipped missing Spine animation: {sourceClip.SourceAnimationName}", unitVA);
                    continue;
                }

                float duration = Convert.ToSingle(animation.GetType().GetProperty("Duration")?.GetValue(animation));
                List<float> sampleTimes = BuildSampleTimes(duration, unitVA.BakeSampleFps, sourceClip.Loop);
                UnitVAClip bakedClip = new UnitVAClip
                {
                    SourceAnimationName = sourceClip.SourceAnimationName,
                    State = sourceClip.State,
                    TicksPerFrame = sourceClip.TicksPerFrame,
                    Loop = sourceClip.Loop,
                    LockUntilComplete = sourceClip.LockUntilComplete,
                    Frames = new List<UnitVAFrame>(sampleTimes.Count)
                };

                for (int frameIndex = 0; frameIndex < sampleTimes.Count; frameIndex++)
                {
                    GeneratedMeshFrame generatedFrame = GenerateMeshAtTime(
                        skeleton,
                        animation,
                        sampleTimes[frameIndex],
                        sourceClip.Loop,
                        setToSetupPose,
                        updateWorldTransform,
                        meshGenerator,
                        instruction,
                        meshGeneratorType,
                        instructionType);

                    Mesh generatedMesh = generatedFrame.Mesh;
                    int validVertexCount = generatedFrame.VertexCount;

                    if (unitVA.FlipHorizontal)
                    {
                        FlipMeshHorizontally(generatedMesh);
                    }

                    Vector3[] vertices = generatedMesh.vertices;
                    Vector2[] uvs = generatedMesh.uv;
                    int[] triangles = generatedMesh.triangles;

                    if (baseMesh == null)
                    {
                        baseMesh = UnityEngine.Object.Instantiate(generatedMesh);
                        baseMesh.name = MakeGeneratedMeshName(unitVA);
                        baseTriangles = triangles;
                        baseUvs = uvs;
                        vertexCount = validVertexCount;
                    }
                    else if (!HasStableGeometry(validVertexCount, triangles, uvs, vertexCount, baseTriangles, baseUvs))
                    {
                        UnityEngine.Object.DestroyImmediate(generatedMesh);
                        throw new InvalidOperationException(
                            $"Unstable Spine geometry at clip '{sourceClip.SourceAnimationName}', frame {frameIndex}. " +
                            "Vertex count, triangles, or UVs changed during sampling.");
                    }

                    UnitVAFrame bakedFrame = new UnitVAFrame
                    {
                        Positions = new Vector2[validVertexCount]
                    };

                    for (int vertexIndex = 0; vertexIndex < validVertexCount; vertexIndex++)
                    {
                        Vector3 vertex = vertices[vertexIndex];
                        bakedFrame.Positions[vertexIndex] = new Vector2(vertex.x, vertex.y);
                    }

                    bakedClip.Frames.Add(bakedFrame);
                    bakedFrameCount++;
                    UnityEngine.Object.DestroyImmediate(generatedMesh);
                }

                bakedClips.Add(bakedClip);
            }

            if (baseMesh == null || bakedFrameCount == 0)
            {
                EditorUtility.DisplayDialog("UnitVASO Bake Failed", "No frames were baked.", "OK");
                return;
            }

            string meshPath = SaveGeneratedMesh(unitVA, baseMesh);

            Undo.RecordObject(unitVA, "Bake UnitVASO From Spine");
            unitVA.Mesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
            unitVA.VertexCount = vertexCount;
            unitVA.Clips = bakedClips;
            EditorUtility.SetDirty(unitVA);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log(
                $"UnitVASO bake complete: {bakedClips.Count} clips, {bakedFrameCount} frames, {vertexCount} vertices. Mesh: {meshPath}",
                unitVA);
        }
        catch (Exception ex)
        {
            Debug.LogError($"UnitVASO bake failed: {ex.Message}", unitVA);
            EditorUtility.DisplayDialog("UnitVASO Bake Failed", ex.Message, "OK");
        }
        finally
        {
            if (baseMesh != null)
            {
                UnityEngine.Object.DestroyImmediate(baseMesh);
            }
        }
    }

    private static object ReadSkeletonData(UnityEngine.Object skeletonDataAsset, bool quiet = false)
    {
        if (skeletonDataAsset == null)
        {
            return null;
        }

        MethodInfo getSkeletonData = skeletonDataAsset.GetType().GetMethod("GetSkeletonData", new[] { typeof(bool) });
        return getSkeletonData?.Invoke(skeletonDataAsset, new object[] { quiet });
    }

    private sealed class GeneratedMeshFrame
    {
        public Mesh Mesh;
        public int VertexCount;
    }

    private static GeneratedMeshFrame GenerateMeshAtTime(
        object skeleton,
        object animation,
        float time,
        bool loop,
        MethodInfo setToSetupPose,
        MethodInfo updateWorldTransform,
        object meshGenerator,
        object instruction,
        Type meshGeneratorType,
        Type instructionType)
    {
        setToSetupPose.Invoke(skeleton, null);
        ApplyAnimation(animation, skeleton, time, loop);
        updateWorldTransform.Invoke(skeleton, null);

        MethodInfo generateInstruction = meshGeneratorType.GetMethod(
            "GenerateSingleSubmeshInstruction",
            BindingFlags.Public | BindingFlags.Static,
            null,
            new[] { instructionType, skeleton.GetType(), typeof(Material) },
            null);
        MethodInfo begin = meshGeneratorType.GetMethod("Begin", Type.EmptyTypes);
        MethodInfo buildMesh = meshGeneratorType.GetMethod("BuildMesh", new[] { instructionType, typeof(bool) });
        MethodInfo fillVertexData = meshGeneratorType.GetMethod("FillVertexData", new[] { typeof(Mesh) });
        MethodInfo fillTriangles = meshGeneratorType.GetMethod("FillTriangles", new[] { typeof(Mesh) });
        MethodInfo fillLateVertexData = meshGeneratorType.GetMethod("FillLateVertexData", new[] { typeof(Mesh) });
        PropertyInfo buffersProperty = meshGeneratorType.GetProperty("Buffers");

        if (generateInstruction == null || begin == null || buildMesh == null || fillVertexData == null || fillTriangles == null || fillLateVertexData == null || buffersProperty == null)
        {
            throw new MissingMethodException("Required spine-unity MeshGenerator methods were not found.");
        }

        generateInstruction.Invoke(null, new[] { instruction, skeleton, null });
        begin.Invoke(meshGenerator, null);
        buildMesh.Invoke(meshGenerator, new object[] { instruction, true });

        Mesh mesh = new Mesh();
        fillVertexData.Invoke(meshGenerator, new object[] { mesh });
        fillTriangles.Invoke(meshGenerator, new object[] { mesh });
        fillLateVertexData.Invoke(meshGenerator, new object[] { mesh });

        int validVertexCount = ReadMeshGeneratorVertexCount(buffersProperty.GetValue(meshGenerator));
        TrimMeshToVertexCount(mesh, validVertexCount);
        return new GeneratedMeshFrame
        {
            Mesh = mesh,
            VertexCount = validVertexCount
        };
    }

    private static int ReadMeshGeneratorVertexCount(object buffers)
    {
        if (buffers == null)
        {
            return 0;
        }

        FieldInfo field = buffers.GetType().GetField("vertexCount");
        return field != null ? Convert.ToInt32(field.GetValue(buffers)) : 0;
    }

    private static void TrimMeshToVertexCount(Mesh mesh, int vertexCount)
    {
        if (mesh == null || vertexCount < 0)
        {
            return;
        }

        Vector3[] vertices = mesh.vertices;
        if (vertices == null || vertices.Length == vertexCount)
        {
            return;
        }

        Vector3[] trimmedVertices = new Vector3[vertexCount];
        Array.Copy(vertices, trimmedVertices, Mathf.Min(vertexCount, vertices.Length));

        Vector2[] uvs = mesh.uv;
        Vector2[] trimmedUvs = new Vector2[vertexCount];
        if (uvs != null)
        {
            Array.Copy(uvs, trimmedUvs, Mathf.Min(vertexCount, uvs.Length));
        }

        Color32[] colors = mesh.colors32;
        Color32[] trimmedColors = new Color32[vertexCount];
        if (colors != null)
        {
            Array.Copy(colors, trimmedColors, Mathf.Min(vertexCount, colors.Length));
        }

        int[] triangles = mesh.triangles;
        mesh.Clear();
        mesh.vertices = trimmedVertices;
        mesh.uv = trimmedUvs;
        if (colors != null && colors.Length > 0)
        {
            mesh.colors32 = trimmedColors;
        }

        mesh.triangles = triangles;
        mesh.RecalculateBounds();
    }

    private static void ApplyAnimation(object animation, object skeleton, float time, bool loop)
    {
        Type mixBlendType = FindType("Spine.MixBlend");
        Type mixDirectionType = FindType("Spine.MixDirection");
        if (mixBlendType == null || mixDirectionType == null)
        {
            throw new MissingMemberException("Spine MixBlend or MixDirection enum was not found.");
        }

        MethodInfo apply = null;
        MethodInfo[] methods = animation.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance);
        for (int i = 0; i < methods.Length; i++)
        {
            MethodInfo method = methods[i];
            if (method.Name == "Apply" && method.GetParameters().Length == 8)
            {
                apply = method;
                break;
            }
        }

        if (apply == null)
        {
            throw new MissingMethodException("Spine.Animation.Apply was not found.");
        }

        apply.Invoke(animation, new[]
        {
            skeleton,
            0f,
            time,
            loop,
            null,
            1f,
            Enum.Parse(mixBlendType, "Setup"),
            Enum.Parse(mixDirectionType, "In")
        });
    }

    private static List<float> BuildSampleTimes(float duration, int sampleFps, bool loop)
    {
        List<float> sampleTimes = new List<float>();
        int fps = UnitVASettings.NormalizeBakeSampleFps(sampleFps);
        if (duration <= 0f)
        {
            sampleTimes.Add(0f);
            return sampleTimes;
        }

        float step = 1f / fps;
        if (loop)
        {
            int frameCount = Mathf.Max(1, Mathf.CeilToInt(duration * fps));
            for (int i = 0; i < frameCount; i++)
            {
                sampleTimes.Add(Mathf.Min(i * step, Mathf.Max(0f, duration - 0.0001f)));
            }
        }
        else
        {
            for (float time = 0f; time < duration; time += step)
            {
                sampleTimes.Add(time);
            }

            if (sampleTimes.Count == 0 || !Mathf.Approximately(sampleTimes[sampleTimes.Count - 1], duration))
            {
                sampleTimes.Add(duration);
            }
        }

        return sampleTimes;
    }

    private static void FlipMeshHorizontally(Mesh mesh)
    {
        Vector3[] verts = mesh.vertices;
        for (int i = 0; i < verts.Length; i++)
        {
            verts[i].x = -verts[i].x;
        }

        mesh.vertices = verts;

        int[] tris = mesh.triangles;
        for (int i = 0; i + 2 < tris.Length; i += 3)
        {
            int tmp = tris[i + 1];
            tris[i + 1] = tris[i + 2];
            tris[i + 2] = tmp;
        }

        mesh.triangles = tris;
    }

    private static bool HasStableGeometry(
        int vertexCount,
        int[] triangles,
        Vector2[] uvs,
        int baseVertexCount,
        int[] baseTriangles,
        Vector2[] baseUvs)
    {
        if (vertexCount != baseVertexCount ||
            triangles == null ||
            uvs == null ||
            baseTriangles == null ||
            baseUvs == null ||
            triangles.Length != baseTriangles.Length ||
            uvs.Length != baseUvs.Length)
        {
            return false;
        }

        for (int i = 0; i < triangles.Length; i++)
        {
            if (triangles[i] != baseTriangles[i])
            {
                return false;
            }
        }

        for (int i = 0; i < uvs.Length; i++)
        {
            if ((uvs[i] - baseUvs[i]).sqrMagnitude > 0.00000001f)
            {
                return false;
            }
        }

        return true;
    }

    private static string SaveGeneratedMesh(UnitVASO unitVA, Mesh generatedMesh)
    {
        string assetPath = AssetDatabase.GetAssetPath(unitVA);
        string directory = Path.GetDirectoryName(assetPath)?.Replace('\\', '/');
        string baseName = Path.GetFileNameWithoutExtension(assetPath);
        if (string.IsNullOrEmpty(directory) || string.IsNullOrEmpty(baseName))
        {
            throw new InvalidOperationException("UnitVASO asset path is invalid.");
        }

        string meshPath = $"{directory}/{baseName}_Mesh.asset";
        Mesh existingMesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
        if (existingMesh == null)
        {
            Mesh meshAsset = UnityEngine.Object.Instantiate(generatedMesh);
            meshAsset.name = MakeGeneratedMeshName(unitVA);
            AssetDatabase.CreateAsset(meshAsset, meshPath);
        }
        else
        {
            Undo.RecordObject(existingMesh, "Update UnitVASO Generated Mesh");
            EditorUtility.CopySerialized(generatedMesh, existingMesh);
            existingMesh.name = MakeGeneratedMeshName(unitVA);
            EditorUtility.SetDirty(existingMesh);
        }

        return meshPath;
    }

    private static string MakeGeneratedMeshName(UnitVASO unitVA)
    {
        return $"{unitVA.name}_Mesh";
    }

    private static Type FindType(string fullName)
    {
        Type type = Type.GetType(fullName);
        if (type != null)
        {
            return type;
        }

        Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
        for (int i = 0; i < assemblies.Length; i++)
        {
            type = assemblies[i].GetType(fullName);
            if (type != null)
            {
                return type;
            }
        }

        return null;
    }

    [MenuItem("Assets/Create/MineRTS/Animation/Unit VASO From Spine JSON", false, 2200)]
    private static void CreateFromSelectedSpineJson()
    {
        UnityEngine.Object[] selectedObjects = Selection.objects;
        UnitVASO lastCreated = null;

        for (int i = 0; i < selectedObjects.Length; i++)
        {
            TextAsset jsonAsset = selectedObjects[i] as TextAsset;
            if (!IsSpineJsonAsset(jsonAsset))
            {
                continue;
            }

            UnitVASO created = CreateOrUpdateFromSpineJson(jsonAsset);
            if (created != null)
            {
                lastCreated = created;
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        if (lastCreated != null)
        {
            Selection.activeObject = lastCreated;
            EditorGUIUtility.PingObject(lastCreated);
        }
    }

    [MenuItem("Assets/Create/MineRTS/Animation/Unit VASO From Spine JSON", true)]
    private static bool ValidateCreateFromSelectedSpineJson()
    {
        UnityEngine.Object[] selectedObjects = Selection.objects;
        for (int i = 0; i < selectedObjects.Length; i++)
        {
            if (IsSpineJsonAsset(selectedObjects[i] as TextAsset))
            {
                return true;
            }
        }

        return false;
    }

    private static UnitVASO CreateOrUpdateFromSpineJson(TextAsset jsonAsset)
    {
        string jsonPath = AssetDatabase.GetAssetPath(jsonAsset);
        string directory = Path.GetDirectoryName(jsonPath)?.Replace('\\', '/');
        string baseName = Path.GetFileNameWithoutExtension(jsonPath);
        if (string.IsNullOrEmpty(directory) || string.IsNullOrEmpty(baseName))
        {
            return null;
        }

        string assetPath = $"{directory}/{baseName}_UnitVA.asset";
        UnitVASO unitVA = AssetDatabase.LoadAssetAtPath<UnitVASO>(assetPath);
        if (unitVA == null)
        {
            unitVA = CreateInstance<UnitVASO>();
            AssetDatabase.CreateAsset(unitVA, assetPath);
        }

        Undo.RecordObject(unitVA, "Create UnitVASO From Spine JSON");
        ApplySourceJson(unitVA, jsonAsset, true);
        EditorUtility.SetDirty(unitVA);
        Debug.Log($"Created/updated UnitVASO: {assetPath}", unitVA);
        return unitVA;
    }

    private static void ApplySourceJson(UnitVASO unitVA, TextAsset jsonAsset, bool createMissingClipEntries)
    {
        string jsonPath = AssetDatabase.GetAssetPath(jsonAsset);
        string directory = Path.GetDirectoryName(jsonPath)?.Replace('\\', '/');
        string baseName = Path.GetFileNameWithoutExtension(jsonPath);

        unitVA.UnitTypeId = string.IsNullOrWhiteSpace(unitVA.UnitTypeId) ? baseName : unitVA.UnitTypeId;
        unitVA.SourceJson = jsonAsset;
        unitVA.SourceAssetPath = jsonPath;
        unitVA.SourceAssetGuid = AssetDatabase.AssetPathToGUID(jsonPath);
        unitVA.SourceSpineVersion = ExtractSpineVersion(jsonAsset.text);
        unitVA.BakeSampleFps = UnitVASettings.NormalizeBakeSampleFps(unitVA.BakeSampleFps);

        if (!string.IsNullOrEmpty(directory) && !string.IsNullOrEmpty(baseName))
        {
            unitVA.SourceSkeletonDataAsset = FindSkeletonDataAsset(directory, baseName);
            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>($"{directory}/{baseName}.png");
            if (texture != null)
            {
                unitVA.BaseTexture = texture;
            }
        }

        unitVA.Clips ??= new List<UnitVAClip>();
        if (createMissingClipEntries)
        {
            AddMissingClipEntries(unitVA, ExtractAnimationNames(jsonAsset.text));
        }
    }

    private static UnityEngine.Object FindSkeletonDataAsset(string directory, string baseName)
    {
        string expectedPath = $"{directory}/{baseName}_SkeletonData.asset";
        UnityEngine.Object expectedAsset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(expectedPath);
        if (expectedAsset != null)
        {
            return expectedAsset;
        }

        string[] guids = AssetDatabase.FindAssets($"{baseName}_SkeletonData t:ScriptableObject", new[] { directory });
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            UnityEngine.Object asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path);
            if (asset != null)
            {
                return asset;
            }
        }

        return null;
    }

    private static void AddMissingClipEntries(UnitVASO unitVA, IReadOnlyList<string> animationNames)
    {
        HashSet<string> existingNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < unitVA.Clips.Count; i++)
        {
            UnitVAClip clip = unitVA.Clips[i];
            string sourceName = clip?.SourceAnimationName;
            if (!string.IsNullOrEmpty(sourceName))
            {
                existingNames.Add(sourceName);
                if (clip.State == UnitAnimationStateId.None)
                {
                    clip.State = GuessStateFromAnimationName(sourceName);
                    clip.Loop = GuessLoopFromAnimationName(sourceName);
                }
            }
        }

        for (int i = 0; i < animationNames.Count; i++)
        {
            string animationName = animationNames[i];
            if (string.IsNullOrEmpty(animationName) || existingNames.Contains(animationName))
            {
                continue;
            }

            unitVA.Clips.Add(new UnitVAClip
            {
                SourceAnimationName = animationName,
                State = GuessStateFromAnimationName(animationName),
                TicksPerFrame = 1,
                Loop = GuessLoopFromAnimationName(animationName),
                Frames = new List<UnitVAFrame>()
            });
        }
    }

    private static UnitAnimationStateId GuessStateFromAnimationName(string animationName)
    {
        string key = NormalizeName(animationName);
        if (key.Contains("idle") || key.Contains("stand") || key.Contains("待机"))
        {
            return UnitAnimationStateId.Idle;
        }

        if (key.Contains("move") || key.Contains("walk") || key.Contains("run") || key.Contains("移动") || key.Contains("走") || key.Contains("跑"))
        {
            return UnitAnimationStateId.Move;
        }

        if (key.Contains("work") || key.Contains("mine") || key.Contains("gather") || key.Contains("工作") || key.Contains("采集"))
        {
            return UnitAnimationStateId.Work;
        }

        if (key.Contains("attack") || key.Contains("atk") || key.Contains("攻击"))
        {
            return UnitAnimationStateId.Attack;
        }

        if (key.Contains("death") || key.Contains("die") || key.Contains("dead") || key.Contains("死亡"))
        {
            return UnitAnimationStateId.Death;
        }

        if (key.Contains("stun") || key.Contains("眩晕") || key.Contains("硬直"))
        {
            return UnitAnimationStateId.Stun;
        }

        return UnitAnimationStateId.None;
    }

    private static bool GuessLoopFromAnimationName(string animationName)
    {
        UnitAnimationStateId state = GuessStateFromAnimationName(animationName);
        return state == UnitAnimationStateId.Idle ||
               state == UnitAnimationStateId.Move ||
               state == UnitAnimationStateId.Work;
    }

    private static string NormalizeName(string name)
    {
        return Regex.Replace(name ?? string.Empty, @"[\s_\-\.]", string.Empty).ToLowerInvariant();
    }

    private static bool IsSpineJsonAsset(TextAsset jsonAsset)
    {
        if (jsonAsset == null)
        {
            return false;
        }

        string path = AssetDatabase.GetAssetPath(jsonAsset);
        return path.EndsWith(".json", StringComparison.OrdinalIgnoreCase) &&
               jsonAsset.text.Contains("\"skeleton\"") &&
               jsonAsset.text.Contains("\"animations\"");
    }

    private static string ExtractSpineVersion(string json)
    {
        Match match = Regex.Match(json ?? string.Empty, "\"spine\"\\s*:\\s*\"([^\"]+)\"");
        return match.Success ? match.Groups[1].Value : string.Empty;
    }

    private static List<string> ExtractAnimationNames(string json)
    {
        List<string> names = new List<string>();
        if (string.IsNullOrEmpty(json))
        {
            return names;
        }

        int keyIndex = json.IndexOf("\"animations\"", StringComparison.Ordinal);
        if (keyIndex < 0)
        {
            return names;
        }

        int colonIndex = json.IndexOf(':', keyIndex);
        int objectStart = colonIndex >= 0 ? json.IndexOf('{', colonIndex) : -1;
        if (objectStart < 0)
        {
            return names;
        }

        int depth = 0;
        for (int i = objectStart; i < json.Length; i++)
        {
            char c = json[i];
            if (c == '{')
            {
                depth++;
                continue;
            }

            if (c == '}')
            {
                depth--;
                if (depth == 0)
                {
                    break;
                }

                continue;
            }

            if (depth == 1 && c == '"')
            {
                int stringEnd = FindStringEnd(json, i + 1);
                if (stringEnd < 0)
                {
                    break;
                }

                string candidate = UnescapeJsonString(json.Substring(i + 1, stringEnd - i - 1));
                int next = SkipWhitespace(json, stringEnd + 1);
                if (next < json.Length && json[next] == ':')
                {
                    names.Add(candidate);
                }

                i = stringEnd;
            }
        }

        return names;
    }

    private static int FindStringEnd(string text, int start)
    {
        bool escaped = false;
        for (int i = start; i < text.Length; i++)
        {
            char c = text[i];
            if (escaped)
            {
                escaped = false;
                continue;
            }

            if (c == '\\')
            {
                escaped = true;
                continue;
            }

            if (c == '"')
            {
                return i;
            }
        }

        return -1;
    }

    private static int SkipWhitespace(string text, int start)
    {
        int i = start;
        while (i < text.Length && char.IsWhiteSpace(text[i]))
        {
            i++;
        }

        return i;
    }

    private static string UnescapeJsonString(string value)
    {
        return value.Replace("\\\"", "\"").Replace("\\\\", "\\");
    }
}
