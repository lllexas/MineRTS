using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System.Text;

[CustomEditor(typeof(UnitAtlasAnimationSetSO))]
public class UnitAtlasAnimationSetSOEditor : Editor
{
    private bool _showIdentity = true;
    private bool _showAtlas = true;
    private bool _showLayout = true;
    private bool _showClips = true;
    private bool _showPreview = true;

    private double _lastPreviewTime;
    private int _previewFrame;
    private bool _isSelectingFrames;
    private int _editingClipIndex = -1;
    private List<AtlasFrameCoord> _pendingFrames = new List<AtlasFrameCoord>();
    private Vector2 _atlasScroll;

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        DrawIdentitySection();
        DrawAtlasSection();
        DrawLayoutSection();
        DrawClipsSection();
        DrawPreviewSection();

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

    private void DrawAtlasSection()
    {
        _showAtlas = EditorGUILayout.BeginFoldoutHeaderGroup(_showAtlas, "Atlas");
        if (_showAtlas)
        {
            DrawProperty("AtlasTexture");
            DrawProperty("FrameTier");
            DrawProperty("AtlasColumns");
            DrawProperty("AtlasRows");

            UnitAtlasAnimationSetSO animationSet = (UnitAtlasAnimationSetSO)target;
            EditorGUILayout.LabelField("Frame Size", $"{animationSet.FrameSizePixels}px");
            EditorGUILayout.LabelField("Frame Capacity", animationSet.TotalFrameCapacity.ToString());
            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("Frame Grid", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                _isSelectingFrames
                    ? "左键按顺序追加 Frame，拖拽可连续扫过。右键移除已选 Frame。"
                    : "先确认 atlas 的 frame 划分与坐标，再在下方 clip 中引用这些 frame。",
                MessageType.None);

            DrawAtlasPreview(animationSet, null, false, false);
        }

        EditorGUILayout.EndFoldoutHeaderGroup();
        EditorGUILayout.Space(4f);
    }

    private void DrawLayoutSection()
    {
        _showLayout = EditorGUILayout.BeginFoldoutHeaderGroup(_showLayout, "Layout");
        if (_showLayout)
        {
            DrawProperty("PivotNormalized");
            DrawProperty("AllowFlipX");
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
            if (clipsProperty == null)
            {
                EditorGUILayout.HelpBox("字段不存在或不可序列化：Clips", MessageType.Warning);
            }
            else
            {
                DrawClipList(clipsProperty, (UnitAtlasAnimationSetSO)target);
                DrawClipSummary((UnitAtlasAnimationSetSO)target);
            }
        }

        EditorGUILayout.EndFoldoutHeaderGroup();
        EditorGUILayout.Space(4f);
    }

    private void DrawClipList(SerializedProperty clipsProperty, UnitAtlasAnimationSetSO animationSet)
    {
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Clip Count", clipsProperty.arraySize.ToString());
        if (GUILayout.Button("Add Clip", GUILayout.Width(90f)))
        {
            int newIndex = clipsProperty.arraySize;
            clipsProperty.InsertArrayElementAtIndex(newIndex);
            SerializedProperty newElement = clipsProperty.GetArrayElementAtIndex(newIndex);
            newElement.FindPropertyRelative("State").enumValueIndex = (int)UnitAnimationStateId.Idle;
            newElement.FindPropertyRelative("Frames").arraySize = 0;
            newElement.FindPropertyRelative("TicksPerFrame").intValue = 1;
            newElement.FindPropertyRelative("Loop").boolValue = true;
            newElement.FindPropertyRelative("LockUntilComplete").boolValue = false;
        }
        EditorGUILayout.EndHorizontal();

        for (int i = 0; i < clipsProperty.arraySize; i++)
        {
            SerializedProperty element = clipsProperty.GetArrayElementAtIndex(i);
            SerializedProperty stateProperty = element.FindPropertyRelative("State");
            SerializedProperty framesProperty = element.FindPropertyRelative("Frames");
            SerializedProperty ticksPerFrameProperty = element.FindPropertyRelative("TicksPerFrame");
            SerializedProperty loopProperty = element.FindPropertyRelative("Loop");
            SerializedProperty lockProperty = element.FindPropertyRelative("LockUntilComplete");

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"Clip {i}", EditorStyles.boldLabel);
            if (GUILayout.Button("Remove", GUILayout.Width(70f)))
            {
                clipsProperty.DeleteArrayElementAtIndex(i);
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
                break;
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.PropertyField(stateProperty);
            EditorGUILayout.PropertyField(ticksPerFrameProperty);
            EditorGUILayout.PropertyField(loopProperty);
            EditorGUILayout.PropertyField(lockProperty);

            DrawFrameSummary(framesProperty);
            DrawClipEditButtons(i, framesProperty, stateProperty);
            if (_isSelectingFrames && _editingClipIndex == i)
            {
                DrawInlineClipEditor(i, animationSet);
            }
            EditorGUILayout.EndVertical();
        }
    }

    private void DrawPreviewSection()
    {
        _showPreview = EditorGUILayout.BeginFoldoutHeaderGroup(_showPreview, "Preview");
        if (_showPreview)
        {
            UnitAtlasAnimationSetSO animationSet = (UnitAtlasAnimationSetSO)target;
            if (animationSet.Clips == null || animationSet.Clips.Count == 0)
            {
                EditorGUILayout.HelpBox("当前没有 Clip 可预览。", MessageType.Info);
            }
            else
            {
                string[] clipNames = new string[animationSet.Clips.Count];
                for (int i = 0; i < animationSet.Clips.Count; i++)
                {
                    UnitAtlasClipDef clip = animationSet.Clips[i];
                    clipNames[i] = $"{clip.State} [{FormatFrameRange(clip)}]";
                }

                int clipIndex = Mathf.Clamp(SessionState.GetInt(GetPreviewClipKey(animationSet), 0), 0, animationSet.Clips.Count - 1);
                int newClipIndex = EditorGUILayout.Popup("Clip", clipIndex, clipNames);
                if (newClipIndex != clipIndex)
                {
                    clipIndex = newClipIndex;
                    _previewFrame = 0;
                    _lastPreviewTime = EditorApplication.timeSinceStartup;
                    SessionState.SetInt(GetPreviewClipKey(animationSet), clipIndex);
                }

                UnitAtlasClipDef selectedClip = animationSet.Clips[clipIndex];
                EditorGUILayout.LabelField("Ticks / Frame", selectedClip.TicksPerFrame.ToString());
                EditorGUILayout.LabelField("Loop", selectedClip.Loop ? "Yes" : "No");
                EditorGUILayout.LabelField("Lock Until Complete", selectedClip.LockUntilComplete ? "Yes" : "No");

                AdvancePreviewFrame(selectedClip);
                AtlasFrameCoord previewCoord = GetPreviewCoord(selectedClip);
                EditorGUILayout.LabelField("Preview Frame", $"{_previewFrame} -> ({previewCoord.Row}, {previewCoord.Col})");

                DrawAtlasPreview(animationSet, previewCoord, false, true);
                DrawPreviewSequenceLegend(selectedClip);
                Repaint();
            }
        }

        EditorGUILayout.EndFoldoutHeaderGroup();
        EditorGUILayout.Space(4f);
    }

    private void AdvancePreviewFrame(UnitAtlasClipDef clip)
    {
        double now = EditorApplication.timeSinceStartup;
        if (_lastPreviewTime <= 0d)
        {
            _lastPreviewTime = now;
            return;
        }

        double secondsPerFrame = Mathf.Max(1, clip.TicksPerFrame) * TimeTicker.SecondsPerTick;
        if (secondsPerFrame <= 0d || now - _lastPreviewTime < secondsPerFrame)
        {
            return;
        }

        _lastPreviewTime = now;
        if (clip.Loop)
        {
            _previewFrame = (_previewFrame + 1) % Mathf.Max(1, clip.Frames?.Length ?? 1);
        }
        else
        {
            _previewFrame = Mathf.Min(_previewFrame + 1, Mathf.Max(0, (clip.Frames?.Length ?? 1) - 1));
        }
    }

    private void DrawClipSummary(UnitAtlasAnimationSetSO animationSet)
    {
        if (animationSet.Clips == null || animationSet.Clips.Count == 0)
        {
            return;
        }

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("Clip Summary", EditorStyles.boldLabel);

        for (int i = 0; i < animationSet.Clips.Count; i++)
        {
            UnitAtlasClipDef clip = animationSet.Clips[i];
            bool overlap = HasOverlap(animationSet, i);
            bool overflow = HasOutOfBounds(animationSet, clip);
            string status = overflow ? "Overflow" : overlap ? "Overlap" : "OK";
            int frameCount = clip.Frames?.Length ?? 0;
            EditorGUILayout.LabelField($"{clip.State}: {FormatFrameRange(clip)} | {frameCount}f | {clip.TicksPerFrame}t | {status}");
        }

        EditorGUILayout.EndVertical();
    }

    private void DrawAtlasPreview(UnitAtlasAnimationSetSO animationSet, AtlasFrameCoord? highlightFrame, bool showSelectionOrder, bool showPreviewOrder)
    {
        if (animationSet.AtlasTexture == null)
        {
            EditorGUILayout.HelpBox("AtlasTexture 为空，无法预览。", MessageType.Info);
            return;
        }

        float previewSize = Mathf.Min(EditorGUIUtility.currentViewWidth - 40f, 320f);
        _atlasScroll = EditorGUILayout.BeginScrollView(_atlasScroll, GUILayout.Height(previewSize + 18f));
        Rect rect = GUILayoutUtility.GetRect(previewSize, previewSize, GUILayout.ExpandWidth(false));
        GUI.DrawTexture(rect, animationSet.AtlasTexture, ScaleMode.ScaleToFit, true);

        Rect textureRect = FitRectPreserveAspect(rect, animationSet.AtlasTexture.width, animationSet.AtlasTexture.height);
        DrawGrid(textureRect, Mathf.Max(1, animationSet.AtlasColumns), Mathf.Max(1, animationSet.AtlasRows));
        DrawFrameLabels(textureRect, Mathf.Max(1, animationSet.AtlasColumns), Mathf.Max(1, animationSet.AtlasRows));
        HandleSelectionInput(textureRect, animationSet);

        if (highlightFrame.HasValue)
        {
            DrawHighlight(textureRect, animationSet, highlightFrame.Value, new Color(1f, 0.8f, 0.2f, 0.95f), new Color(1f, 0.8f, 0.2f, 0.22f));
        }

        if (_isSelectingFrames)
        {
            for (int i = 0; i < _pendingFrames.Count; i++)
            {
                DrawHighlight(textureRect, animationSet, _pendingFrames[i], new Color(0.2f, 1f, 0.6f, 0.95f), new Color(0.2f, 1f, 0.6f, 0.18f));
            }

            if (showSelectionOrder)
            {
                DrawFrameOrderLabels(textureRect, animationSet, _pendingFrames, new Color(0.2f, 1f, 0.6f, 1f));
            }
        }
        else if (showPreviewOrder)
        {
            IList<AtlasFrameCoord> previewFrames = GetPreviewFrames(animationSet);
            if (previewFrames != null)
            {
                DrawFrameOrderLabels(textureRect, animationSet, previewFrames, new Color(1f, 0.8f, 0.2f, 1f));
            }
        }

        EditorGUILayout.EndScrollView();
    }

    private static Rect FitRectPreserveAspect(Rect rect, float textureWidth, float textureHeight)
    {
        float textureAspect = textureWidth / Mathf.Max(1f, textureHeight);
        float rectAspect = rect.width / Mathf.Max(1f, rect.height);

        if (textureAspect > rectAspect)
        {
            float height = rect.width / textureAspect;
            float y = rect.y + (rect.height - height) * 0.5f;
            return new Rect(rect.x, y, rect.width, height);
        }

        float width = rect.height * textureAspect;
        float x = rect.x + (rect.width - width) * 0.5f;
        return new Rect(x, rect.y, width, rect.height);
    }

    private static void DrawGrid(Rect rect, int columns, int rows)
    {
        Handles.BeginGUI();
        Color oldColor = Handles.color;
        Handles.color = new Color(1f, 1f, 1f, 0.35f);

        float cellWidth = rect.width / columns;
        float cellHeight = rect.height / rows;

        for (int x = 1; x < columns; x++)
        {
            float lineX = rect.x + (cellWidth * x);
            Handles.DrawLine(new Vector3(lineX, rect.y), new Vector3(lineX, rect.yMax));
        }

        for (int y = 1; y < rows; y++)
        {
            float lineY = rect.y + (cellHeight * y);
            Handles.DrawLine(new Vector3(rect.x, lineY), new Vector3(rect.xMax, lineY));
        }

        Handles.color = oldColor;
        Handles.EndGUI();
    }

    private static void DrawFrameLabels(Rect rect, int columns, int rows)
    {
        GUIStyle labelStyle = new GUIStyle(EditorStyles.miniBoldLabel)
        {
            alignment = TextAnchor.UpperLeft
        };
        labelStyle.normal.textColor = new Color(1f, 1f, 1f, 0.92f);

        float cellWidth = rect.width / columns;
        float cellHeight = rect.height / rows;
        for (int row = 0; row < rows; row++)
        {
            for (int col = 0; col < columns; col++)
            {
                Rect labelRect = new Rect(
                    rect.x + (col * cellWidth) + 3f,
                    rect.y + (row * cellHeight) + 2f,
                    cellWidth - 6f,
                    14f);
                GUI.Label(labelRect, $"{row},{col}", labelStyle);
            }
        }
    }

    private static void DrawFrameOrderLabels(Rect rect, UnitAtlasAnimationSetSO animationSet, IList<AtlasFrameCoord> frames, Color textColor)
    {
        if (frames == null || frames.Count == 0)
        {
            return;
        }

        GUIStyle labelStyle = new GUIStyle(EditorStyles.whiteMiniLabel)
        {
            alignment = TextAnchor.LowerRight,
            fontStyle = FontStyle.Bold,
            wordWrap = true
        };
        labelStyle.normal.textColor = textColor;

        int columns = Mathf.Max(1, animationSet.AtlasColumns);
        int rows = Mathf.Max(1, animationSet.AtlasRows);
        float cellWidth = rect.width / columns;
        float cellHeight = rect.height / rows;
        Dictionary<(int row, int col), string> labelsByCell = BuildCellOrderLabels(frames, columns, rows);
        foreach (KeyValuePair<(int row, int col), string> pair in labelsByCell)
        {
            int row = pair.Key.row;
            int col = pair.Key.col;
            Rect labelRect = new Rect(
                rect.x + (col * cellWidth) + 2f,
                rect.y + (row * cellHeight) + cellHeight - 34f,
                cellWidth - 4f,
                30f);
            GUI.Label(labelRect, pair.Value, labelStyle);
        }
    }

    private static Dictionary<(int row, int col), string> BuildCellOrderLabels(IList<AtlasFrameCoord> frames, int columns, int rows)
    {
        Dictionary<(int row, int col), StringBuilder> builders = new Dictionary<(int row, int col), StringBuilder>();
        for (int i = 0; i < frames.Count; i++)
        {
            int row = Mathf.Clamp(frames[i].Row, 0, rows - 1);
            int col = Mathf.Clamp(frames[i].Col, 0, columns - 1);
            (int row, int col) key = (row, col);
            if (!builders.TryGetValue(key, out StringBuilder builder))
            {
                builder = new StringBuilder();
                builders[key] = builder;
            }
            else
            {
                int existingCount = CountOrderEntries(builder);
                if (existingCount % 3 == 0)
                {
                    builder.AppendLine();
                }
                else
                {
                    builder.Append(',');
                }
            }

            builder.Append(i);
        }

        Dictionary<(int row, int col), string> result = new Dictionary<(int row, int col), string>();
        foreach (KeyValuePair<(int row, int col), StringBuilder> pair in builders)
        {
            result[pair.Key] = pair.Value.ToString();
        }

        return result;
    }

    private static int CountOrderEntries(StringBuilder builder)
    {
        if (builder.Length == 0)
        {
            return 0;
        }

        int count = 1;
        for (int i = 0; i < builder.Length; i++)
        {
            if (builder[i] == ',' || builder[i] == '\n')
            {
                count++;
            }
        }

        return count;
    }

    private void HandleSelectionInput(Rect rect, UnitAtlasAnimationSetSO animationSet)
    {
        if (!_isSelectingFrames)
        {
            return;
        }

        Event evt = Event.current;
        if (evt == null)
        {
            return;
        }

        if (evt.type != EventType.MouseDown && evt.type != EventType.MouseDrag)
        {
            return;
        }

        if (!rect.Contains(evt.mousePosition))
        {
            return;
        }

        AtlasFrameCoord coord = GetCoordFromMouse(rect, animationSet, evt.mousePosition);
        if (evt.button == 0)
        {
            AppendPendingFrame(coord);
            evt.Use();
        }
        else if (evt.button == 1)
        {
            RemovePendingFrame(coord);
            evt.Use();
        }
    }

    private static void DrawHighlight(Rect rect, UnitAtlasAnimationSetSO animationSet, AtlasFrameCoord frameCoord, Color lineColor, Color fillColor)
    {
        int columns = Mathf.Max(1, animationSet.AtlasColumns);
        int rows = Mathf.Max(1, animationSet.AtlasRows);
        int col = Mathf.Clamp(frameCoord.Col, 0, columns - 1);
        int row = Mathf.Clamp(frameCoord.Row, 0, rows - 1);

        float cellWidth = rect.width / columns;
        float cellHeight = rect.height / rows;
        Rect cellRect = new Rect(
            rect.x + (col * cellWidth),
            rect.y + (row * cellHeight),
            cellWidth,
            cellHeight);

        EditorGUI.DrawRect(cellRect, fillColor);
        Handles.BeginGUI();
        Color oldColor = Handles.color;
        Handles.color = lineColor;
        Handles.DrawAAPolyLine(2f,
            new Vector3(cellRect.xMin, cellRect.yMin),
            new Vector3(cellRect.xMax, cellRect.yMin),
            new Vector3(cellRect.xMax, cellRect.yMax),
            new Vector3(cellRect.xMin, cellRect.yMax),
            new Vector3(cellRect.xMin, cellRect.yMin));
        Handles.color = oldColor;
        Handles.EndGUI();
    }

    private static bool HasOverlap(UnitAtlasAnimationSetSO animationSet, int index)
    {
        UnitAtlasClipDef current = animationSet.Clips[index];
        for (int i = 0; i < animationSet.Clips.Count; i++)
        {
            if (i == index)
            {
                continue;
            }

            UnitAtlasClipDef other = animationSet.Clips[i];
            if (SharesFrame(current, other))
            {
                return true;
            }
        }

        return false;
    }

    private static bool SharesFrame(UnitAtlasClipDef a, UnitAtlasClipDef b)
    {
        if (a.Frames == null || b.Frames == null)
        {
            return false;
        }

        for (int i = 0; i < a.Frames.Length; i++)
        {
            for (int j = 0; j < b.Frames.Length; j++)
            {
                if (a.Frames[i].Row == b.Frames[j].Row && a.Frames[i].Col == b.Frames[j].Col)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool HasOutOfBounds(UnitAtlasAnimationSetSO animationSet, UnitAtlasClipDef clip)
    {
        if (clip.Frames == null)
        {
            return false;
        }

        for (int i = 0; i < clip.Frames.Length; i++)
        {
            AtlasFrameCoord frame = clip.Frames[i];
            if (frame.Row < 0 || frame.Col < 0 || frame.Row >= animationSet.AtlasRows || frame.Col >= animationSet.AtlasColumns)
            {
                return true;
            }
        }

        return false;
    }

    private void DrawFrameSummary(SerializedProperty framesProperty)
    {
        int frameCount = framesProperty?.arraySize ?? 0;
        EditorGUILayout.LabelField("Frame Count", frameCount.ToString());
        if (frameCount == 0)
        {
            EditorGUILayout.HelpBox("当前 Clip 还没有选中任何 Frame。", MessageType.Info);
            return;
        }

        string summary = BuildFrameSummary(framesProperty);
        EditorGUILayout.LabelField("Frames");
        float height = Mathf.Max(38f, EditorStyles.textArea.CalcHeight(new GUIContent(summary), EditorGUIUtility.currentViewWidth - 80f));
        EditorGUILayout.SelectableLabel(summary, EditorStyles.textArea, GUILayout.MinHeight(height));
    }

    private void DrawClipEditButtons(int clipIndex, SerializedProperty framesProperty, SerializedProperty stateProperty)
    {
        EditorGUILayout.BeginHorizontal();
        if (!_isSelectingFrames || _editingClipIndex != clipIndex)
        {
            if (GUILayout.Button("Edit Frames"))
            {
                BeginFrameSelection(clipIndex, framesProperty);
            }
        }
        else
        {
            EditorGUILayout.LabelField("当前在上方 Atlas 编辑", EditorStyles.miniBoldLabel);
        }

        if (GUILayout.Button("Clear"))
        {
            if (_isSelectingFrames && _editingClipIndex == clipIndex)
            {
                _pendingFrames.Clear();
            }
            else
            {
                framesProperty.arraySize = 0;
            }
        }

        if (GUILayout.Button("Auto Fill Row"))
        {
            if (_isSelectingFrames && _editingClipIndex == clipIndex)
            {
                AutoFillPendingRow(stateProperty.enumValueIndex);
            }
            else
            {
                AutoFillRow(framesProperty, stateProperty.enumValueIndex);
            }
        }
        EditorGUILayout.EndHorizontal();
    }

    private void BeginFrameSelection(int clipIndex, SerializedProperty framesProperty)
    {
        _pendingFrames.Clear();
        for (int i = 0; i < framesProperty.arraySize; i++)
        {
            SerializedProperty frameProperty = framesProperty.GetArrayElementAtIndex(i);
            _pendingFrames.Add(new AtlasFrameCoord(
                frameProperty.FindPropertyRelative("Row").intValue,
                frameProperty.FindPropertyRelative("Col").intValue));
        }

        _editingClipIndex = clipIndex;
        _isSelectingFrames = true;
        GUI.FocusControl(null);
        Repaint();
    }

    private void ApplyPendingFrames(SerializedProperty framesProperty)
    {
        framesProperty.arraySize = _pendingFrames.Count;
        for (int i = 0; i < _pendingFrames.Count; i++)
        {
            SerializedProperty frameProperty = framesProperty.GetArrayElementAtIndex(i);
            frameProperty.FindPropertyRelative("Row").intValue = _pendingFrames[i].Row;
            frameProperty.FindPropertyRelative("Col").intValue = _pendingFrames[i].Col;
        }

        _pendingFrames.Clear();
    }

    private void AutoFillRow(SerializedProperty framesProperty, int stateEnumIndex)
    {
        UnitAtlasAnimationSetSO animationSet = (UnitAtlasAnimationSetSO)target;
        int row = Mathf.Clamp(stateEnumIndex - 1, 0, Mathf.Max(0, animationSet.AtlasRows - 1));
        int columns = Mathf.Max(1, animationSet.AtlasColumns);
        framesProperty.arraySize = columns;
        for (int col = 0; col < columns; col++)
        {
            SerializedProperty frameProperty = framesProperty.GetArrayElementAtIndex(col);
            frameProperty.FindPropertyRelative("Row").intValue = row;
            frameProperty.FindPropertyRelative("Col").intValue = col;
        }
    }

    private void AutoFillPendingRow(int stateEnumIndex)
    {
        UnitAtlasAnimationSetSO animationSet = (UnitAtlasAnimationSetSO)target;
        int row = Mathf.Clamp(stateEnumIndex - 1, 0, Mathf.Max(0, animationSet.AtlasRows - 1));
        int columns = Mathf.Max(1, animationSet.AtlasColumns);
        _pendingFrames.Clear();
        for (int col = 0; col < columns; col++)
        {
            _pendingFrames.Add(new AtlasFrameCoord(row, col));
        }
    }

    private void AppendPendingFrame(AtlasFrameCoord coord)
    {
        _pendingFrames.Add(coord);
    }

    private void RemovePendingFrame(AtlasFrameCoord coord)
    {
        for (int i = _pendingFrames.Count - 1; i >= 0; i--)
        {
            if (_pendingFrames[i].Row == coord.Row && _pendingFrames[i].Col == coord.Col)
            {
                _pendingFrames.RemoveAt(i);
                return;
            }
        }
    }

    private static AtlasFrameCoord GetCoordFromMouse(Rect rect, UnitAtlasAnimationSetSO animationSet, Vector2 mousePosition)
    {
        float normalizedX = Mathf.Clamp01((mousePosition.x - rect.x) / rect.width);
        float normalizedY = Mathf.Clamp01((mousePosition.y - rect.y) / rect.height);

        int col = Mathf.Min(animationSet.AtlasColumns - 1, Mathf.FloorToInt(normalizedX * animationSet.AtlasColumns));
        int row = Mathf.Min(animationSet.AtlasRows - 1, Mathf.FloorToInt(normalizedY * animationSet.AtlasRows));
        return new AtlasFrameCoord(row, col);
    }

    private AtlasFrameCoord? GetPreviewHighlightFrame(UnitAtlasAnimationSetSO animationSet)
    {
        if (_isSelectingFrames)
        {
            return null;
        }

        if (!_showPreview || animationSet.Clips == null || animationSet.Clips.Count == 0)
        {
            return null;
        }

        int clipIndex = Mathf.Clamp(SessionState.GetInt(GetPreviewClipKey(animationSet), 0), 0, animationSet.Clips.Count - 1);
        return GetPreviewCoord(animationSet.Clips[clipIndex]);
    }

    private AtlasFrameCoord GetPreviewCoord(UnitAtlasClipDef clip)
    {
        if (clip.Frames == null || clip.Frames.Length == 0)
        {
            return default;
        }

        int frameIndex = Mathf.Clamp(_previewFrame, 0, clip.Frames.Length - 1);
        return clip.Frames[frameIndex];
    }

    private static string FormatFrameRange(UnitAtlasClipDef clip)
    {
        if (clip.Frames == null || clip.Frames.Length == 0)
        {
            return "<empty>";
        }

        if (clip.Frames.Length == 1)
        {
            AtlasFrameCoord frame = clip.Frames[0];
            return $"({frame.Row},{frame.Col})";
        }

        AtlasFrameCoord first = clip.Frames[0];
        AtlasFrameCoord last = clip.Frames[clip.Frames.Length - 1];
        return $"({first.Row},{first.Col}) -> ({last.Row},{last.Col})";
    }

    private static string GetPreviewClipKey(UnitAtlasAnimationSetSO animationSet)
    {
        return $"UnitAtlasAnimationSetSOEditor.PreviewClip.{animationSet.GetInstanceID()}";
    }

    private IList<AtlasFrameCoord> GetPreviewFrames(UnitAtlasAnimationSetSO animationSet)
    {
        if (_showPreview && animationSet.Clips != null && animationSet.Clips.Count > 0)
        {
            int clipIndex = Mathf.Clamp(SessionState.GetInt(GetPreviewClipKey(animationSet), 0), 0, animationSet.Clips.Count - 1);
            return animationSet.Clips[clipIndex].Frames;
        }

        return null;
    }

    private void DrawInlineClipEditor(int clipIndex, UnitAtlasAnimationSetSO animationSet)
    {
        if (!_isSelectingFrames || _editingClipIndex != clipIndex)
        {
            return;
        }

        SerializedProperty clipsProperty = serializedObject.FindProperty("Clips");
        if (clipsProperty == null || clipIndex >= clipsProperty.arraySize)
        {
            return;
        }

        SerializedProperty clipProperty = clipsProperty.GetArrayElementAtIndex(clipIndex);
        SerializedProperty stateProperty = clipProperty.FindPropertyRelative("State");
        SerializedProperty ticksPerFrameProperty = clipProperty.FindPropertyRelative("TicksPerFrame");

        EditorGUILayout.Space(6f);
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("Clip Frame Editor", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("Clip", $"{clipIndex} / {(UnitAnimationStateId)stateProperty.enumValueIndex}");
        EditorGUILayout.LabelField("Ticks / Frame", ticksPerFrameProperty.intValue.ToString());
        EditorGUILayout.LabelField("Pending Frame Count", _pendingFrames.Count.ToString());
        EditorGUILayout.HelpBox("左键按顺序追加 Frame，拖拽可连续扫过。右键移除已选 Frame。", MessageType.None);

        DrawAtlasPreview(animationSet, null, true, false);

        string summary = BuildFrameSummary(_pendingFrames);
        if (string.IsNullOrEmpty(summary))
        {
            EditorGUILayout.HelpBox("当前还没有选中任何 Frame。", MessageType.Info);
        }
        else
        {
            EditorGUILayout.LabelField("Pending Frames");
            float height = Mathf.Max(38f, EditorStyles.textArea.CalcHeight(new GUIContent(summary), EditorGUIUtility.currentViewWidth - 80f));
            EditorGUILayout.SelectableLabel(summary, EditorStyles.textArea, GUILayout.MinHeight(height));
        }

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Confirm"))
        {
            SerializedProperty framesProperty = clipProperty.FindPropertyRelative("Frames");
            ApplyPendingFrames(framesProperty);
            _isSelectingFrames = false;
            _editingClipIndex = -1;
            GUI.FocusControl(null);
        }

        if (GUILayout.Button("Cancel"))
        {
            _pendingFrames.Clear();
            _isSelectingFrames = false;
            _editingClipIndex = -1;
            GUI.FocusControl(null);
        }

        if (GUILayout.Button("Clear"))
        {
            _pendingFrames.Clear();
        }

        if (GUILayout.Button("Auto Fill Row"))
        {
            AutoFillPendingRow(stateProperty.enumValueIndex);
        }
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.EndVertical();
    }

    private static string BuildFrameSummary(SerializedProperty framesProperty)
    {
        if (framesProperty == null || framesProperty.arraySize == 0)
        {
            return string.Empty;
        }

        StringBuilder builder = new StringBuilder();
        for (int i = 0; i < framesProperty.arraySize; i++)
        {
            SerializedProperty frameProperty = framesProperty.GetArrayElementAtIndex(i);
            int row = frameProperty.FindPropertyRelative("Row").intValue;
            int col = frameProperty.FindPropertyRelative("Col").intValue;
            if (i > 0)
            {
                builder.Append(" -> ");
                if (i % 6 == 0)
                {
                    builder.AppendLine();
                }
            }

            builder.Append('(').Append(row).Append(',').Append(col).Append(')');
        }

        return builder.ToString();
    }

    private static string BuildFrameSummary(List<AtlasFrameCoord> frames)
    {
        if (frames == null || frames.Count == 0)
        {
            return string.Empty;
        }

        StringBuilder builder = new StringBuilder();
        for (int i = 0; i < frames.Count; i++)
        {
            if (i > 0)
            {
                builder.Append(" -> ");
                if (i % 6 == 0)
                {
                    builder.AppendLine();
                }
            }

            builder.Append('(').Append(frames[i].Row).Append(',').Append(frames[i].Col).Append(')');
        }

        return builder.ToString();
    }

    private static string BuildFrameSummary(AtlasFrameCoord[] frames)
    {
        if (frames == null || frames.Length == 0)
        {
            return string.Empty;
        }

        StringBuilder builder = new StringBuilder();
        for (int i = 0; i < frames.Length; i++)
        {
            if (i > 0)
            {
                builder.Append(" -> ");
                if (i % 6 == 0)
                {
                    builder.AppendLine();
                }
            }

            builder.Append(i)
                .Append(':')
                .Append('(').Append(frames[i].Row).Append(',').Append(frames[i].Col).Append(')');
        }

        return builder.ToString();
    }

    private void DrawPreviewSequenceLegend(UnitAtlasClipDef clip)
    {
        if (clip.Frames == null || clip.Frames.Length == 0)
        {
            return;
        }

        string summary = BuildFrameSummary(clip.Frames);
        EditorGUILayout.LabelField("Clip Frame Order");
        float height = Mathf.Max(38f, EditorStyles.textArea.CalcHeight(new GUIContent(summary), EditorGUIUtility.currentViewWidth - 80f));
        EditorGUILayout.SelectableLabel(summary, EditorStyles.textArea, GUILayout.MinHeight(height));
    }

    private void DrawProperty(string propertyName)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property == null)
        {
            EditorGUILayout.HelpBox($"字段不存在或不可序列化：{propertyName}", MessageType.Warning);
            return;
        }

        EditorGUILayout.PropertyField(property, true);
    }
}
