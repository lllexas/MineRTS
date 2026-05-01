
#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace GAL
{
[CustomEditor(typeof(DialogSequenceSO))]
public class DialogSequenceSOEditor : UnityEditor.Editor
{
    private const string AnchorMetaRoot = "Assets/GAL/Editor/AnchorMeta";
    private const float ListHeight = 320f;

    private DialogSequenceSO _target;
    private DialogSequenceAnchorMeta _anchorMeta;
    private string _sequenceMetaId;
    private string _search = string.Empty;
    private bool _mergeDialogs;
    private bool _compactEffects;
    private int _selectedIndex = -1;
    private Vector2 _scroll;
    private readonly List<Row> _rows = new();
    private readonly Dictionary<EffectEntry, DialogSequenceEffectAnchorRecord> _anchors = new();

    private string PrefsKey => $"DialogSequenceSOEditor_{_target.GetInstanceID()}";

    private void OnEnable()
    {
        _target = (DialogSequenceSO)target;
        _search = EditorPrefs.GetString(PrefsKey + "_Search", string.Empty);
        _mergeDialogs = EditorPrefs.GetBool(PrefsKey + "_Merge", false);
        _compactEffects = EditorPrefs.GetBool(PrefsKey + "_Compact", false);
        _selectedIndex = Mathf.Clamp(_selectedIndex, -1, _target.Entries.Count - 1);
        _sequenceMetaId = EnsureSequenceMetaId();
        _anchorMeta = LoadOrCreateAnchorMeta();
        RebuildRows();
    }

    private void OnDisable()
    {
        if (_target == null) return;
        EditorPrefs.SetString(PrefsKey + "_Search", _search ?? string.Empty);
        EditorPrefs.SetBool(PrefsKey + "_Merge", _mergeDialogs);
        EditorPrefs.SetBool(PrefsKey + "_Compact", _compactEffects);
        SaveAnchorMeta();
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        DrawSource();
        EditorGUILayout.Space(8f);
        DrawBuild();
        EditorGUILayout.Space(8f);
        DrawToolbar();
        EditorGUILayout.Space(6f);
        DrawSearch();
        EditorGUILayout.Space(6f);
        DrawList();
        EditorGUILayout.Space(10f);
        DrawDetails();
        serializedObject.ApplyModifiedProperties();
    }

    private void DrawSource()
    {
        EditorGUILayout.LabelField("CSV 源", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(DialogSequenceSO.CsvSource)));
        EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(DialogSequenceSO.SequenceId)));
        EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(DialogSequenceSO.Description)));
        if (!string.IsNullOrWhiteSpace(_sequenceMetaId))
            EditorGUILayout.LabelField($"MetaLib ID: {_sequenceMetaId}", EditorStyles.miniLabel);
    }

    private void DrawBuild()
    {
        using (new EditorGUI.DisabledScope(_target.CsvSource == null))
        {
            if (GUILayout.Button("从 CSV 构建序列", GUILayout.Height(28f)))
            {
                BuildFromCsv();
                GUIUtility.ExitGUI();
            }
        }

        int dialogs = _target.Entries.OfType<DialogEntry>().Count();
        int effects = _target.Entries.OfType<EffectEntry>().Count();
        EditorGUILayout.LabelField($"统计: {dialogs} 对话 / {effects} 演出 / {_target.Entries.Count} 总条目", EditorStyles.miniLabel);
    }

    private void DrawToolbar()
    {
        EditorGUI.BeginChangeCheck();
        EditorGUILayout.BeginHorizontal();
        _mergeDialogs = GUILayout.Toggle(_mergeDialogs, "合并对话", EditorStyles.miniButtonLeft);
        _compactEffects = GUILayout.Toggle(_compactEffects, "紧凑特效", EditorStyles.miniButtonMid);
        if (GUILayout.Button("刷新", EditorStyles.miniButtonRight, GUILayout.Width(60f))) RebuildRows();
        EditorGUILayout.EndHorizontal();
        if (EditorGUI.EndChangeCheck()) RebuildRows();

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("新增对话")) AddEntry<DialogEntry>();
        if (GUILayout.Button("新增头像")) AddEntry<AvatarEffectEntry>();
        if (GUILayout.Button("新增背景")) AddEntry<BackgroundEffectEntry>();
        if (GUILayout.Button("新增语音")) AddEntry<VoiceEffectEntry>();
        if (GUILayout.Button("新增镜头")) AddEntry<CameraEffectEntry>();
        if (GUILayout.Button("新增闪屏")) AddEntry<ScreenFlashEntry>();
        EditorGUILayout.EndHorizontal();

        using (new EditorGUI.DisabledScope(_selectedIndex < 0 || _selectedIndex >= _target.Entries.Count))
        {
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("上移")) MoveSelected(-1);
            if (GUILayout.Button("下移")) MoveSelected(1);
            if (GUILayout.Button("删除")) RemoveSelected();
            EditorGUILayout.EndHorizontal();
        }
    }

    private void DrawSearch()
    {
        EditorGUILayout.BeginVertical(GUI.skin.box);
        string next = EditorGUILayout.TextField("搜索", _search);
        if (next != _search)
        {
            _search = next;
            _scroll = Vector2.zero;
            RebuildRows();
        }

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("支持: ID / Speaker / Content / 效果 / 锚点", EditorStyles.miniLabel);
        if (GUILayout.Button("下一个", GUILayout.Width(70f))) SelectNextResult();
        if (GUILayout.Button("清空", GUILayout.Width(60f)))
        {
            _search = string.Empty;
            _scroll = Vector2.zero;
            RebuildRows();
        }
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.EndVertical();
    }

    private void DrawList()
    {
        EditorGUILayout.BeginVertical(GUI.skin.box, GUILayout.Height(ListHeight));
        _scroll = EditorGUILayout.BeginScrollView(_scroll, GUILayout.Height(ListHeight - 8f));
        if (_rows.Count == 0)
        {
            EditorGUILayout.HelpBox(string.IsNullOrWhiteSpace(_search) ? "当前没有条目。" : "没有匹配结果。", MessageType.Info);
        }
        else
        {
            foreach (Row row in _rows)
            {
                Rect rect = EditorGUILayout.GetControlRect(false, row.Height);
                if (row.Index == _selectedIndex && row.Index >= 0)
                    EditorGUI.DrawRect(rect, new Color(0.2f, 0.4f, 0.6f, 0.2f));

                if (row.Kind == RowKind.Collapsed)
                {
                    EditorGUI.LabelField(rect, $"    ... {row.CollapsedCount} 条连续对话已折叠 ...", EditorStyles.miniLabel);
                }
                else
                {
                    Rect left = new Rect(rect.x + 8f, rect.y + 2f, 110f, EditorGUIUtility.singleLineHeight);
                    Rect right = new Rect(rect.x + 120f, rect.y + 2f, rect.width - 128f, EditorGUIUtility.singleLineHeight);
                    EditorGUI.LabelField(left, $"[{row.Index}] {row.Title}", row.Kind == RowKind.CompactEffect ? EditorStyles.miniBoldLabel : EditorStyles.boldLabel);
                    EditorGUI.LabelField(right, row.Preview, EditorStyles.miniLabel);
                }

                if (row.Index >= 0 && Event.current.type == EventType.MouseDown && rect.Contains(Event.current.mousePosition))
                {
                    _selectedIndex = row.Index;
                    Event.current.Use();
                    Repaint();
                }
            }
        }
        EditorGUILayout.EndScrollView();
        EditorGUILayout.EndVertical();
    }
    private void DrawDetails()
    {
        if (_selectedIndex < 0 || _selectedIndex >= _target.Entries.Count) return;
        ISequenceEntry entry = _target.Entries[_selectedIndex];
        if (entry == null) return;

        EditorGUILayout.LabelField($"编辑条目 [{_selectedIndex}] {GetEntryTitle(entry)}", EditorStyles.boldLabel);
        EditorGUILayout.BeginVertical(GUI.skin.box);
        switch (entry)
        {
            case DialogEntry dialog: EditDialog(dialog); break;
            case AvatarEffectEntry avatar: EditAvatar(avatar); break;
            case BackgroundEffectEntry background: EditBackground(background); break;
            case VoiceEffectEntry voice: EditVoice(voice); break;
            case CameraEffectEntry camera: EditCamera(camera); break;
            case ScreenFlashEntry flash: EditFlash(flash); break;
        }
        EditorGUILayout.EndVertical();
    }

    private void EditDialog(DialogEntry entry)
    {
        string id = EditorGUILayout.TextField("ID", entry.Id);
        string speaker = EditorGUILayout.TextField("说话者", entry.Speaker);
        EditorGUILayout.LabelField("内容");
        string content = EditorGUILayout.TextArea(entry.Content, GUILayout.MinHeight(72f));
        if (id == entry.Id && speaker == entry.Speaker && content == entry.Content) return;
        Undo.RecordObject(_target, "Edit Dialog Entry");
        entry.Id = id;
        entry.Speaker = speaker;
        entry.Content = content;
        MarkDirty();
    }

    private void EditAvatar(AvatarEffectEntry entry)
    {
        int slotIndex = EditorGUILayout.IntSlider("槽位", entry.SlotIndex, 0, 4);
        AvatarAction action = (AvatarAction)EditorGUILayout.EnumPopup("动作", entry.Action);
        CharacterRenderProfileSO profile = (CharacterRenderProfileSO)EditorGUILayout.ObjectField("角色预设", entry.Profile, typeof(CharacterRenderProfileSO), false);
        string emotionKey = DrawAvatarEmotionKeyField(profile, entry.EmotionKey);
        bool fromLeft = entry.FromLeft;
        if (action == AvatarAction.Show)
            fromLeft = EditorGUILayout.Toggle("从左侧入场", entry.FromLeft);

        if (slotIndex != entry.SlotIndex
            || action != entry.Action
            || profile != entry.Profile
            || emotionKey != entry.EmotionKey
            || fromLeft != entry.FromLeft)
        {
            Undo.RecordObject(_target, "Edit Avatar Effect");
            entry.SlotIndex = slotIndex;
            entry.Action = action;
            entry.Profile = profile;
            entry.EmotionKey = emotionKey;
            entry.FromLeft = fromLeft;
            MarkDirty();
        }
        DrawAnchorInfo(entry);
    }

    private string DrawAvatarEmotionKeyField(CharacterRenderProfileSO profile, string currentKey)
    {
        if (profile == null)
        {
            EditorGUILayout.HelpBox("请先选择角色预设，再选择情绪 Key。", MessageType.Info);
            return string.Empty;
        }

        List<string> keys = GetAvatarEmotionKeys(profile);
        if (keys.Count <= 1)
        {
            EditorGUILayout.HelpBox("该角色预设未配置可用的 EmotionKey。", MessageType.Warning);
            return string.Empty;
        }

        int index = keys.IndexOf(currentKey);
        if (index < 0) index = 0;
        int nextIndex = EditorGUILayout.Popup("情绪 Key", index, keys.ToArray());
        return nextIndex == 0 ? string.Empty : keys[nextIndex];
    }

    private List<string> GetAvatarEmotionKeys(CharacterRenderProfileSO profile)
    {
        var keys = new List<string> { "(未选择)" };
        if (profile?.Avatars == null) return keys;

        foreach (CharacterAvatarPreset preset in profile.Avatars)
        {
            if (preset == null || string.IsNullOrWhiteSpace(preset.EmotionKey))
                continue;

            string key = preset.EmotionKey.Trim();
            if (!keys.Contains(key))
                keys.Add(key);
        }

        return keys;
    }

    private void EditBackground(BackgroundEffectEntry entry)
    {
        BackgroundPresetSO preset = (BackgroundPresetSO)EditorGUILayout.ObjectField("背景预设", entry.Preset, typeof(BackgroundPresetSO), false);
        if (preset != entry.Preset)
        {
            Undo.RecordObject(_target, "Edit Background Effect");
            entry.Preset = preset;
            MarkDirty();
        }
        DrawAnchorInfo(entry);
    }

    private void EditVoice(VoiceEffectEntry entry)
    {
        string targetId = EditorGUILayout.TextField("目标对话 ID", entry.TargetDialogId);
        AudioClip clip = (AudioClip)EditorGUILayout.ObjectField("语音", entry.VoiceClip, typeof(AudioClip), false);
        if (targetId != entry.TargetDialogId || clip != entry.VoiceClip)
        {
            Undo.RecordObject(_target, "Edit Voice Effect");
            entry.TargetDialogId = targetId;
            entry.VoiceClip = clip;
            MarkDirty();
        }
        DrawAnchorInfo(entry);
    }

    private void EditCamera(CameraEffectEntry entry)
    {
        List<string> cameraKeys = GetAllCameraEffectKeys();
        string effectKey = DrawCameraEffectKeyField(entry.EffectKey, cameraKeys);
        bool useDurationOverride = EditorGUILayout.Toggle("覆盖时长", entry.UseDurationOverride);
        float durationOverride = entry.DurationOverride;
        if (useDurationOverride)
            durationOverride = Mathf.Max(0f, EditorGUILayout.FloatField("时长", entry.DurationOverride));

        bool useIntensityOverride = EditorGUILayout.Toggle("覆盖强度", entry.UseIntensityOverride);
        float intensityOverride = entry.IntensityOverride;
        if (useIntensityOverride)
            intensityOverride = EditorGUILayout.FloatField("强度", entry.IntensityOverride);

        if (effectKey != entry.EffectKey
            || useDurationOverride != entry.UseDurationOverride
            || !Mathf.Approximately(durationOverride, entry.DurationOverride)
            || useIntensityOverride != entry.UseIntensityOverride
            || !Mathf.Approximately(intensityOverride, entry.IntensityOverride))
        {
            Undo.RecordObject(_target, "Edit Camera Effect");
            entry.EffectKey = effectKey;
            entry.UseDurationOverride = useDurationOverride;
            entry.DurationOverride = durationOverride;
            entry.UseIntensityOverride = useIntensityOverride;
            entry.IntensityOverride = intensityOverride;
            MarkDirty();
        }
        DrawAnchorInfo(entry);
    }

    private string DrawCameraEffectKeyField(string currentKey, List<string> cameraKeys)
    {
        if (cameraKeys.Count == 0)
            return EditorGUILayout.TextField("效果 Key", currentKey);

        bool isCustom = !string.IsNullOrWhiteSpace(currentKey) && !cameraKeys.Contains(currentKey);
        int customIndex = cameraKeys.Count - 1;
        int selectedIndex = string.IsNullOrWhiteSpace(currentKey)
            ? 0
            : isCustom ? customIndex : cameraKeys.IndexOf(currentKey);
        if (selectedIndex < 0) selectedIndex = 0;

        EditorGUILayout.BeginHorizontal();
        int nextIndex = EditorGUILayout.Popup("效果 Key", selectedIndex, cameraKeys.ToArray());
        bool customSelected = nextIndex == customIndex;
        EditorGUILayout.EndHorizontal();

        if (nextIndex == 0)
            return string.Empty;

        if (!customSelected)
            return cameraKeys[nextIndex];

        return EditorGUILayout.TextField("自定义 Key", currentKey);
    }

    private List<string> GetAllCameraEffectKeys()
    {
        var keys = new List<string>();
        string[] guids = AssetDatabase.FindAssets("t:CameraEffectLibrarySO");
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            CameraEffectLibrarySO library = AssetDatabase.LoadAssetAtPath<CameraEffectLibrarySO>(path);
            if (library == null) continue;

            foreach (string key in library.GetAllKeys())
            {
                if (string.IsNullOrWhiteSpace(key) || keys.Contains(key)) continue;
                keys.Add(key);
            }
        }

        keys.Sort(StringComparer.Ordinal);
        keys.Insert(0, "(未选择)");
        keys.Add("(自定义...)");
        return keys;
    }

    private void EditFlash(ScreenFlashEntry entry)
    {
        ScreenFlashType flashType = (ScreenFlashType)EditorGUILayout.EnumPopup("闪屏颜色", entry.FlashType);
        float duration = Mathf.Max(0f, EditorGUILayout.FloatField("持续时间", entry.Duration));
        if (flashType != entry.FlashType || !Mathf.Approximately(duration, entry.Duration))
        {
            Undo.RecordObject(_target, "Edit Screen Flash");
            entry.FlashType = flashType;
            entry.Duration = duration;
            MarkDirty();
        }
        DrawAnchorInfo(entry);
    }

    private void DrawAnchorInfo(EffectEntry effect)
    {
        _anchors.TryGetValue(effect, out DialogSequenceEffectAnchorRecord record);
        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField("当前锚点", EditorStyles.miniBoldLabel);
        EditorGUI.BeginDisabledGroup(true);
        EditorGUILayout.TextField("Speaker", record?.AnchorSpeaker ?? string.Empty);
        EditorGUILayout.TextField("Content", record?.AnchorContentPreview ?? string.Empty);
        EditorGUILayout.TextField("Signature", record?.AnchorSignature ?? string.Empty);
        EditorGUILayout.IntField("Occurrence", record?.AnchorOccurrence ?? 0);
        EditorGUILayout.IntField("Fallback Bucket", record?.FallbackBucket ?? 0);
        EditorGUI.EndDisabledGroup();
    }

    private void AddEntry<T>() where T : class, ISequenceEntry, new()
    {
        Undo.RecordObject(_target, $"Add {typeof(T).Name}");
        int insert = _selectedIndex >= 0 ? _selectedIndex + 1 : _target.Entries.Count;
        insert = Mathf.Clamp(insert, 0, _target.Entries.Count);
        _target.Entries.Insert(insert, CreateDefaultEntry<T>());
        _selectedIndex = insert;
        MarkDirty();
    }

    private T CreateDefaultEntry<T>() where T : class, ISequenceEntry, new()
    {
        if (typeof(T) != typeof(DialogEntry)) return new T();
        int number = _target.Entries.OfType<DialogEntry>().Count() + 1;
        string prefix = string.IsNullOrWhiteSpace(_target.SequenceId) ? "dialog" : _target.SequenceId.Trim();
        return new DialogEntry { Id = $"{prefix}_{number:D3}", Speaker = string.Empty, Content = string.Empty } as T;
    }

    private void RemoveSelected()
    {
        if (_selectedIndex < 0 || _selectedIndex >= _target.Entries.Count) return;
        Undo.RecordObject(_target, "Remove Entry");
        _target.Entries.RemoveAt(_selectedIndex);
        _selectedIndex = Mathf.Clamp(_selectedIndex, -1, _target.Entries.Count - 1);
        MarkDirty();
    }

    private void MoveSelected(int delta)
    {
        if (_selectedIndex < 0 || _selectedIndex >= _target.Entries.Count) return;
        int target = Mathf.Clamp(_selectedIndex + delta, 0, _target.Entries.Count - 1);
        if (target == _selectedIndex) return;
        Undo.RecordObject(_target, "Move Entry");
        (_target.Entries[_selectedIndex], _target.Entries[target]) = (_target.Entries[target], _target.Entries[_selectedIndex]);
        _selectedIndex = target;
        MarkDirty();
    }

    private void BuildFromCsv()
    {
        List<DialogEntry> dialogs = ParseCsv(_target.CsvSource.text);
        if (dialogs.Count == 0)
        {
            EditorUtility.DisplayDialog("构建失败", "CSV 解析结果为空。", "确定");
            return;
        }

        SaveAnchorMeta();
        Undo.RecordObject(_target, "Build Dialog Sequence");

        var oldDialogs = _target.Entries.OfType<DialogEntry>().Where(x => !string.IsNullOrWhiteSpace(x.Id)).GroupBy(x => x.Id).ToDictionary(x => x.Key, x => x.First());
        List<EffectPlacement> placements = BuildPlacements();
        var lookup = BuildDialogOccurrenceLookup(dialogs);
        var placed = new HashSet<EffectEntry>();
        var rebuilt = new List<ISequenceEntry>();

        AddFallbackBucket(rebuilt, placements, placed, 0);
        for (int i = 0; i < dialogs.Count; i++)
        {
            DialogEntry parsed = dialogs[i];
            DialogEntry effective = oldDialogs.TryGetValue(parsed.Id, out DialogEntry existing) ? existing : parsed;
            effective.Speaker = parsed.Speaker;
            effective.Content = parsed.Content;
            rebuilt.Add(effective);

            foreach (EffectPlacement placement in placements)
            {
                if (placed.Contains(placement.Effect) || !placement.HasAnchor) continue;
                if (!MatchesAnchor(placement.Record, effective, lookup, i)) continue;
                rebuilt.Add(placement.Effect);
                placed.Add(placement.Effect);
            }

            AddFallbackBucket(rebuilt, placements, placed, i + 1);
        }

        foreach (EffectPlacement placement in placements.Where(x => !placed.Contains(x.Effect)).OrderBy(x => x.FallbackBucket).ThenBy(x => x.Order))
        {
            rebuilt.Add(placement.Effect);
            placed.Add(placement.Effect);
        }

        _target.Entries = rebuilt;
        _selectedIndex = Mathf.Clamp(_selectedIndex, -1, _target.Entries.Count - 1);
        MarkDirty();
        EditorUtility.DisplayDialog("构建完成", $"生成了 {dialogs.Count} 条对话，保留了 {rebuilt.OfType<EffectEntry>().Count()} 条演出。", "确定");
    }

    private void AddFallbackBucket(List<ISequenceEntry> rebuilt, List<EffectPlacement> placements, HashSet<EffectEntry> placed, int bucket)
    {
        foreach (EffectPlacement placement in placements)
        {
            if (placed.Contains(placement.Effect) || placement.HasAnchor || placement.FallbackBucket != bucket) continue;
            rebuilt.Add(placement.Effect);
            placed.Add(placement.Effect);
        }
    }
    private List<EffectPlacement> BuildPlacements()
    {
        var sidecar = (_anchorMeta?.Records ?? new List<DialogSequenceEffectAnchorRecord>())
            .ToDictionary(x => $"{x.EffectType}|{x.EffectFingerprint}|{x.FingerprintOccurrence}", x => x, StringComparer.Ordinal);

        var placements = new List<EffectPlacement>();
        var counters = new Dictionary<string, int>(StringComparer.Ordinal);
        int dialogCount = 0;
        int order = 0;

        foreach (ISequenceEntry entry in _target.Entries)
        {
            if (entry is DialogEntry)
            {
                dialogCount++;
                continue;
            }
            if (entry is not EffectEntry effect) continue;

            string type = effect.GetType().Name;
            string fingerprint = BuildEffectFingerprint(effect);
            string counterKey = type + "|" + fingerprint;
            counters.TryGetValue(counterKey, out int count);
            int occurrence = count + 1;
            counters[counterKey] = occurrence;
            sidecar.TryGetValue($"{type}|{fingerprint}|{occurrence}", out DialogSequenceEffectAnchorRecord record);
            placements.Add(new EffectPlacement { Effect = effect, Record = record, FallbackBucket = record?.FallbackBucket ?? dialogCount, Order = record?.OriginalOrder ?? order });
            order++;
        }

        return placements;
    }

    private List<DialogEntry> ParseCsv(string csv)
    {
        var rows = ParseRows(csv);
        var result = new List<DialogEntry>();
        if (rows.Count < 2) return result;

        List<string> headers = rows[0];
        int idIndex = FindIndex(headers, "id", 0);
        int speakerIndex = FindIndex(headers, "speaker", 1);
        int contentIndex = FindIndex(headers, "content", 2);
        int generated = 1;

        foreach (List<string> row in rows.Skip(1))
        {
            if (row.All(string.IsNullOrWhiteSpace)) continue;
            string id = GetCell(row, idIndex);
            if (string.IsNullOrWhiteSpace(id)) id = GenerateDialogId(generated);
            else if (int.TryParse(id, out int number)) id = GenerateDialogId(number);
            result.Add(new DialogEntry { Id = id.Trim(), Speaker = GetCell(row, speakerIndex), Content = GetCell(row, contentIndex) });
            generated++;
        }

        return result;
    }

    private List<List<string>> ParseRows(string csv)
    {
        var rows = new List<List<string>>();
        var row = new List<string>();
        var cell = new StringBuilder();
        bool inQuotes = false;
        for (int i = 0; i < csv.Length; i++)
        {
            char c = csv[i];
            if (c == '"')
            {
                if (inQuotes && i + 1 < csv.Length && csv[i + 1] == '"') { cell.Append('"'); i++; }
                else inQuotes = !inQuotes;
            }
            else if (c == ',' && !inQuotes)
            {
                row.Add(cell.ToString().Trim());
                cell.Clear();
            }
            else if ((c == '\n' || c == '\r') && !inQuotes)
            {
                if (c == '\r' && i + 1 < csv.Length && csv[i + 1] == '\n') i++;
                row.Add(cell.ToString().Trim());
                cell.Clear();
                rows.Add(row);
                row = new List<string>();
            }
            else cell.Append(c);
        }
        if (cell.Length > 0 || row.Count > 0)
        {
            row.Add(cell.ToString().Trim());
            rows.Add(row);
        }
        return rows;
    }

    private int FindIndex(List<string> headers, string name, int fallback)
    {
        int index = headers.FindIndex(x => string.Equals(x?.Trim(), name, StringComparison.OrdinalIgnoreCase));
        return index >= 0 ? index : fallback;
    }

    private string GetCell(List<string> row, int index) => index >= 0 && index < row.Count ? row[index] : string.Empty;

    private string GenerateDialogId(int number)
    {
        string prefix = string.IsNullOrWhiteSpace(_target.SequenceId) ? "dialog" : _target.SequenceId.Trim();
        return $"{prefix}_{number:D3}";
    }

    private void RebuildRows()
    {
        RebuildAnchors();
        _rows.Clear();
        int index = 0;
        while (index < _target.Entries.Count)
        {
            ISequenceEntry entry = _target.Entries[index];
            if (_mergeDialogs && string.IsNullOrWhiteSpace(_search) && entry is DialogEntry)
            {
                int start = index;
                int end = index;
                while (end + 1 < _target.Entries.Count && _target.Entries[end + 1] is DialogEntry) end++;
                int size = end - start + 1;
                if (size >= 3)
                {
                    AddRow(new Row(start, RowKind.Normal, "起 对话", GetDialogPreview((DialogEntry)_target.Entries[start]), 20f));
                    _rows.Add(new Row(-1, RowKind.Collapsed, string.Empty, string.Empty, 18f) { CollapsedCount = size - 2 });
                    AddRow(new Row(end, RowKind.Normal, "止 对话", GetDialogPreview((DialogEntry)_target.Entries[end]), 20f));
                }
                else
                {
                    for (int i = start; i <= end; i++) AddRow(MakeNormalRow(i, _target.Entries[i]));
                }
                index = end + 1;
                continue;
            }

            AddRow((_compactEffects && string.IsNullOrWhiteSpace(_search) && entry is EffectEntry) ? MakeCompactRow(index, entry) : MakeNormalRow(index, entry));
            index++;
        }
    }

    private void AddRow(Row row)
    {
        if (row.Index >= 0 && !MatchesSearch(_target.Entries[row.Index])) return;
        _rows.Add(row);
    }

    private Row MakeNormalRow(int index, ISequenceEntry entry) => new(index, RowKind.Normal, GetEntryTitle(entry), GetEntryPreview(entry), 20f);
    private Row MakeCompactRow(int index, ISequenceEntry entry) => new(index, RowKind.CompactEffect, GetEntryTitle(entry), GetEntryPreview(entry), 18f);

    private void RebuildAnchors()
    {
        _anchors.Clear();
        var dialogOccurrences = new Dictionary<string, int>(StringComparer.Ordinal);
        DialogEntry lastDialog = null;
        int dialogCount = 0;
        foreach (ISequenceEntry entry in _target.Entries)
        {
            if (entry is DialogEntry dialog)
            {
                dialogCount++;
                string signature = BuildDialogSignature(dialog.Speaker, dialog.Content);
                dialogOccurrences.TryGetValue(signature, out int count);
                dialogOccurrences[signature] = count + 1;
                lastDialog = dialog;
                continue;
            }

            if (entry is not EffectEntry effect) continue;
            string anchorSignature = lastDialog == null ? string.Empty : BuildDialogSignature(lastDialog.Speaker, lastDialog.Content);
            dialogOccurrences.TryGetValue(anchorSignature, out int occurrence);
            _anchors[effect] = new DialogSequenceEffectAnchorRecord
            {
                EffectType = effect.GetType().Name,
                EffectFingerprint = BuildEffectFingerprint(effect),
                AnchorSpeaker = lastDialog?.Speaker ?? string.Empty,
                AnchorContentPreview = Preview(lastDialog?.Content, 48),
                AnchorSignature = anchorSignature,
                AnchorOccurrence = lastDialog == null ? 0 : occurrence,
                FallbackBucket = dialogCount
            };
        }
    }
    private bool MatchesSearch(ISequenceEntry entry)
    {
        if (string.IsNullOrWhiteSpace(_search)) return true;
        string haystack = entry switch
        {
            DialogEntry dialog => string.Join(" ", dialog.Id, dialog.Speaker, dialog.Content),
            AvatarEffectEntry avatar => string.Join(" ", "Avatar", avatar.Action, avatar.SlotIndex, avatar.Profile?.CharacterId, GetName(avatar.Profile), avatar.EmotionKey, GetAnchorText(avatar)),
            BackgroundEffectEntry background => string.Join(" ", "Background", background.Preset?.Id, GetName(background.Preset), GetAnchorText(background)),
            VoiceEffectEntry voice => string.Join(" ", "Voice", voice.TargetDialogId, GetName(voice.VoiceClip), GetAnchorText(voice)),
            CameraEffectEntry camera => string.Join(" ", "Camera", camera.EffectKey, camera.UseDurationOverride ? camera.DurationOverride.ToString() : string.Empty, camera.UseIntensityOverride ? camera.IntensityOverride.ToString() : string.Empty, GetAnchorText(camera)),
            ScreenFlashEntry flash => string.Join(" ", "Flash", flash.FlashType, flash.Duration, GetAnchorText(flash)),
            _ => string.Empty
        };
        return haystack.IndexOf(_search.Trim(), StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private void SelectNextResult()
    {
        if (_rows.Count == 0) return;
        int current = _rows.FindIndex(x => x.Index == _selectedIndex);
        int next = current >= 0 ? (current + 1) % _rows.Count : 0;
        _selectedIndex = _rows[next].Index;
        Repaint();
    }

    private string GetEntryTitle(ISequenceEntry entry) => entry switch
    {
        DialogEntry => "对话",
        AvatarEffectEntry => "头像",
        BackgroundEffectEntry => "背景",
        VoiceEffectEntry => "语音",
        CameraEffectEntry => "镜头",
        ScreenFlashEntry => "闪屏",
        _ => "未知"
    };

    private string GetEntryPreview(ISequenceEntry entry) => entry switch
    {
        DialogEntry dialog => GetDialogPreview(dialog),
        AvatarEffectEntry avatar => $"{avatar.Action} S{avatar.SlotIndex} / {GetName(avatar.Profile, "(未指定角色预设)")} / {avatar.EmotionKey}{GetAnchorSuffix(avatar)}",
        BackgroundEffectEntry background => $"{GetName(background.Preset, "(未指定背景预设)")}{GetAnchorSuffix(background)}",
        VoiceEffectEntry voice => $"{GetName(voice.VoiceClip, "(无语音)")} -> {voice.TargetDialogId}{GetAnchorSuffix(voice)}",
        CameraEffectEntry camera => $"{camera.EffectKey}{GetCameraOverrideSuffix(camera)}{GetAnchorSuffix(camera)}",
        ScreenFlashEntry flash => $"{flash.FlashType} / {flash.Duration:0.##}s{GetAnchorSuffix(flash)}",
        _ => string.Empty
    };

    private string GetCameraOverrideSuffix(CameraEffectEntry camera)
    {
        string content = string.Empty;
        if (camera.UseDurationOverride)
            content += $"dur={camera.DurationOverride:0.##}";
        if (camera.UseIntensityOverride)
            content += string.IsNullOrEmpty(content)
                ? $"int={camera.IntensityOverride:0.##}"
                : $" int={camera.IntensityOverride:0.##}";

        return string.IsNullOrEmpty(content) ? string.Empty : $" ({content})";
    }

    private string GetDialogPreview(DialogEntry dialog) => $"{dialog.Speaker}: {Preview(dialog.Content, 42)}";

    private string GetAnchorSuffix(EffectEntry effect)
    {
        if (!_anchors.TryGetValue(effect, out DialogSequenceEffectAnchorRecord record) || string.IsNullOrWhiteSpace(record.AnchorSignature)) return string.Empty;
        return $"  <- {record.AnchorSpeaker}{(record.AnchorOccurrence > 1 ? $" #{record.AnchorOccurrence}" : string.Empty)}";
    }

    private string GetAnchorText(EffectEntry effect)
    {
        return _anchors.TryGetValue(effect, out DialogSequenceEffectAnchorRecord record)
            ? string.Join(" ", record.AnchorSpeaker, record.AnchorContentPreview, record.AnchorSignature, record.AnchorOccurrence)
            : string.Empty;
    }

    private string Preview(string text, int max)
    {
        if (string.IsNullOrEmpty(text)) return "(空)";
        return text.Length <= max ? text : text.Substring(0, max) + "...";
    }

    private string EnsureSequenceMetaId()
    {
        string assetPath = AssetDatabase.GetAssetPath(_target);
        string normalized = assetPath.Replace('\\', '/');
        int marker = normalized.IndexOf("/Resources/", StringComparison.OrdinalIgnoreCase);
        if (marker < 0) return null;

        string guid = AssetDatabase.AssetPathToGUID(assetPath);
        string resourcePath = Path.ChangeExtension(normalized[(marker + "/Resources/".Length)..], null);
        string id = $"dialog-sequence:{guid}";

        MetaLib.Reload();
        MetaLib.MetaEntry meta = MetaLib.GetMetaByPath(resourcePath);
        if (meta == null || meta.EffectiveID != id)
        {
            if (meta != null) MetaLib.Unregister(meta.EffectiveID);
            MetaLib.Register(id, new MetaLib.MetaEntry
            {
                ID = id,
                PackID = id,
                Kind = MetaLib.EntryKind.ResourceObject,
                Storage = MetaLib.StorageType.Resources,
                ResourcePath = resourcePath,
                ObjectType = typeof(DialogSequenceSO).FullName,
                DisplayName = _target.name,
                Description = _target.Description,
                CustomFields = new Dictionary<string, string> { ["AssetPath"] = assetPath, ["AssetGuid"] = guid }
            });
            MetaLib.Save();
            MetaLib.Reload();
        }

        return id;
    }

    private DialogSequenceAnchorMeta LoadOrCreateAnchorMeta()
    {
        string guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(_target));
        EnsureFolder(Path.GetDirectoryName(AnchorMetaRoot).Replace('\\', '/'));
        EnsureFolder(AnchorMetaRoot);
        string path = $"{AnchorMetaRoot}/{guid}.asset";
        DialogSequenceAnchorMeta meta = AssetDatabase.LoadAssetAtPath<DialogSequenceAnchorMeta>(path);
        if (meta == null)
        {
            meta = CreateInstance<DialogSequenceAnchorMeta>();
            AssetDatabase.CreateAsset(meta, path);
            AssetDatabase.SaveAssets();
        }
        meta.SequenceMetaId = _sequenceMetaId;
        meta.SequenceAssetGuid = guid;
        EditorUtility.SetDirty(meta);
        return meta;
    }
    private void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
        if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }

    private void SaveAnchorMeta()
    {
        if (_anchorMeta == null) return;
        _anchorMeta.SequenceMetaId = _sequenceMetaId;
        _anchorMeta.SequenceAssetGuid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(_target));
        _anchorMeta.Records = BuildAnchorRecords();
        EditorUtility.SetDirty(_anchorMeta);
        AssetDatabase.SaveAssets();
    }

    private List<DialogSequenceEffectAnchorRecord> BuildAnchorRecords()
    {
        var records = new List<DialogSequenceEffectAnchorRecord>();
        var dialogOccurrences = new Dictionary<string, int>(StringComparer.Ordinal);
        var effectOccurrences = new Dictionary<string, int>(StringComparer.Ordinal);
        DialogEntry lastDialog = null;
        int dialogCount = 0;
        int effectOrder = 0;

        foreach (ISequenceEntry entry in _target.Entries)
        {
            if (entry is DialogEntry dialog)
            {
                dialogCount++;
                string signature = BuildDialogSignature(dialog.Speaker, dialog.Content);
                dialogOccurrences.TryGetValue(signature, out int count);
                dialogOccurrences[signature] = count + 1;
                lastDialog = dialog;
                continue;
            }

            if (entry is not EffectEntry effect) continue;
            string type = effect.GetType().Name;
            string fingerprint = BuildEffectFingerprint(effect);
            string key = type + "|" + fingerprint;
            effectOccurrences.TryGetValue(key, out int count2);
            int occurrence = count2 + 1;
            effectOccurrences[key] = occurrence;
            string anchorSignature = lastDialog == null ? string.Empty : BuildDialogSignature(lastDialog.Speaker, lastDialog.Content);
            dialogOccurrences.TryGetValue(anchorSignature, out int anchorOccurrence);

            records.Add(new DialogSequenceEffectAnchorRecord
            {
                EffectType = type,
                EffectFingerprint = fingerprint,
                FingerprintOccurrence = occurrence,
                AnchorSpeaker = lastDialog?.Speaker ?? string.Empty,
                AnchorContentPreview = Preview(lastDialog?.Content, 48),
                AnchorSignature = anchorSignature,
                AnchorOccurrence = lastDialog == null ? 0 : anchorOccurrence,
                FallbackBucket = dialogCount,
                OriginalOrder = effectOrder
            });
            effectOrder++;
        }

        return records;
    }

    private Dictionary<string, List<int>> BuildDialogOccurrenceLookup(List<DialogEntry> dialogs)
    {
        var map = new Dictionary<string, List<int>>(StringComparer.Ordinal);
        for (int i = 0; i < dialogs.Count; i++)
        {
            string signature = BuildDialogSignature(dialogs[i].Speaker, dialogs[i].Content);
            if (!map.TryGetValue(signature, out List<int> list))
            {
                list = new List<int>();
                map.Add(signature, list);
            }
            list.Add(i);
        }
        return map;
    }

    private bool MatchesAnchor(DialogSequenceEffectAnchorRecord record, DialogEntry dialog, Dictionary<string, List<int>> lookup, int dialogIndex)
    {
        if (record == null || string.IsNullOrWhiteSpace(record.AnchorSignature)) return false;
        string signature = BuildDialogSignature(dialog.Speaker, dialog.Content);
        if (signature != record.AnchorSignature || !lookup.TryGetValue(signature, out List<int> list) || list.Count == 0) return false;
        return record.AnchorOccurrence > 0 && record.AnchorOccurrence <= list.Count ? list[record.AnchorOccurrence - 1] == dialogIndex : list[0] == dialogIndex;
    }

    private string BuildDialogSignature(string speaker, string content) => $"{NormalizeText(speaker)}|{NormalizeText(content)}";

    private string NormalizeText(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;
        var sb = new StringBuilder();
        bool whitespace = false;
        foreach (char c in text.Trim())
        {
            if (char.IsWhiteSpace(c))
            {
                if (!whitespace)
                {
                    sb.Append(' ');
                    whitespace = true;
                }
                continue;
            }
            sb.Append(char.ToLowerInvariant(c));
            whitespace = false;
        }
        return sb.ToString();
    }

    private string BuildEffectFingerprint(EffectEntry effect) => effect switch
    {
        AvatarEffectEntry avatar => $"avatar|{avatar.Action}|{avatar.SlotIndex}|{GetGuid(avatar.Profile)}|{avatar.EmotionKey}|{avatar.FromLeft}",
        BackgroundEffectEntry background => $"background|{GetGuid(background.Preset)}",
        VoiceEffectEntry voice => $"voice|{voice.TargetDialogId}|{GetGuid(voice.VoiceClip)}",
        CameraEffectEntry camera => $"camera|{camera.EffectKey}|{camera.UseDurationOverride}|{camera.DurationOverride:0.###}|{camera.UseIntensityOverride}|{camera.IntensityOverride:0.###}",
        ScreenFlashEntry flash => $"flash|{flash.FlashType}|{flash.Duration:0.###}",
        _ => effect.GetType().Name
    };

    private string GetGuid(UnityEngine.Object asset)
    {
        if (!asset) return string.Empty;
        string path = AssetDatabase.GetAssetPath(asset);
        return string.IsNullOrWhiteSpace(path) ? string.Empty : AssetDatabase.AssetPathToGUID(path);
    }

    private string GetName(UnityEngine.Object asset, string fallback = "") => asset ? asset.name : fallback;

    private void MarkDirty()
    {
        EditorUtility.SetDirty(_target);
        RebuildRows();
        SaveAnchorMeta();
    }

    private sealed class EffectPlacement
    {
        public EffectEntry Effect;
        public DialogSequenceEffectAnchorRecord Record;
        public int FallbackBucket;
        public int Order;
        public bool HasAnchor => Record != null && !string.IsNullOrWhiteSpace(Record.AnchorSignature);
    }

    private enum RowKind { Normal, CompactEffect, Collapsed }

    private sealed class Row
    {
        public Row(int index, RowKind kind, string title, string preview, float height)
        {
            Index = index;
            Kind = kind;
            Title = title;
            Preview = preview;
            Height = height;
        }

        public int Index;
        public RowKind Kind;
        public string Title;
        public string Preview;
        public float Height;
        public int CollapsedCount;
    }
}
}
#endif
