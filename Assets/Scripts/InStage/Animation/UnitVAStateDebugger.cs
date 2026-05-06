using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

/// <summary>
/// Runtime debug overlay: shows the ACTUAL animation state below each unit.
/// Reads directly from DrawSystem's playback-state dictionaries
/// (both VA and atlas paths) — no recomputation, no guessing.
///
/// OnGUI refreshes every frame. Inspector list refreshes at a slower rate.
/// </summary>
public sealed class UnitVAStateDebugger : SingletonMono<UnitVAStateDebugger>
{
    [Header("OnGUI Overlay")]
    public bool ShowInGameOverlay = true;
    [Range(8, 24)] public int FontSize = 14;
    public bool ShowOnlyMoving;

    [Header("Colors")]
    public Color IdleColor   = new Color(0.4f, 0.4f, 0.4f);
    public Color MoveColor   = new Color(0.0f, 0.7f, 0.9f);
    public Color AttackColor = new Color(0.9f, 0.2f, 0.2f);
    public Color WorkColor   = new Color(0.9f, 0.8f, 0.1f);
    public Color DeathColor  = new Color(0.7f, 0.1f, 0.7f);

    [Header("Interceptor Chain (read-only)")]
    [SerializeField] private List<InterceptorDebugEntry> _interceptorChain = new List<InterceptorDebugEntry>();

    [Header("Entity States (Inspector)")]
    [Range(0.1f, 2f)] public float InspectorRefreshInterval = 0.5f;
    [SerializeField] private int _totalActive;
    [SerializeField] private List<EntityDebugEntry> _entityStates = new List<EntityDebugEntry>();

    // ------------------------------------------------------------------
    // Internal
    // ------------------------------------------------------------------

    [Serializable]
    private struct InterceptorDebugEntry
    {
        public string Name; public int Priority; public bool Enabled; public string Description;
    }

    [Serializable]
    private struct EntityDebugEntry
    {
        public string Blueprint; public int CreationIndex; public string State;
    }

    private struct LabelData
    {
        public Vector3 WorldPos;
        public string StateName;
        public Color Color;
    }

    // Cached reflection handles (set once).
    private FieldInfo _vaStatesField;
    private FieldInfo _vaActiveKeysField;
    private FieldInfo _atlasStatesField;
    private FieldInfo _atlasActiveKeysField;
    private bool _reflectionReady;

    // Per-frame label data (rebuilt every frame for OnGUI).
    private readonly List<LabelData> _labels = new List<LabelData>(256);

    // Inspector throttle.
    private float _nextInspectorRefresh;

    // ------------------------------------------------------------------
    // MonoBehaviour
    // ------------------------------------------------------------------

    protected override void Awake()
    {
        base.Awake();
        RefreshInterceptorChainView();
        CacheReflectionFields();
    }

    private void CacheReflectionFields()
    {
        Type drawType = typeof(DrawSystem);
        const BindingFlags Bf = BindingFlags.NonPublic | BindingFlags.Instance;

        _vaStatesField       = drawType.GetField("_vaPlaybackStates", Bf);
        _vaActiveKeysField   = drawType.GetField("_activeVaPlaybackKeys", Bf);
        _atlasStatesField    = drawType.GetField("_atlasPlaybackStates", Bf);
        _atlasActiveKeysField = drawType.GetField("_activeAtlasPlaybackKeys", Bf);
        _reflectionReady = _vaStatesField != null || _atlasStatesField != null;
    }

    private void Update()
    {
        // OnGUI labels — rebuild every frame for smooth display.
        RebuildLabels();

        // Inspector list — throttled.
        if (Time.time >= _nextInspectorRefresh)
        {
            _nextInspectorRefresh = Time.time + InspectorRefreshInterval;
            RefreshInterceptorChainView();
            RefreshInspectorList();
        }
    }

    private void OnGUI()
    {
        if (!ShowInGameOverlay) return;
        Camera cam = Camera.main;
        if (cam == null) return;

        foreach (LabelData lbl in _labels)
        {
            Vector3 sp = cam.WorldToScreenPoint(lbl.WorldPos);
            if (sp.z < 0f) continue;

            float x = sp.x;
            float y = Screen.height - sp.y;

            Color bg = lbl.Color;
            bg.a = 0.7f;
            DrawLabel(x, y, lbl.StateName, bg);
        }
    }

    // ------------------------------------------------------------------
    // Label rebuild (every frame)
    // ------------------------------------------------------------------

    private void RebuildLabels()
    {
        _labels.Clear();
        _totalActive = 0;

        DrawSystem ds = DrawSystem.Instance;
        if (ds == null) return;

        WholeComponent whole = EntitySystem.Instance?.wholeComponent;
        if (whole == null) return;

        // Collect creation-index → entity-index mapping.
        var indexByCreation = new Dictionary<int, int>();
        for (int i = 0; i < whole.entityCount; i++)
        {
            if (whole.coreComponent[i].Active)
            {
                indexByCreation[whole.coreComponent[i].CreationIndex] = i;
            }
        }

        // ── VA entities ──
        if (_reflectionReady && _vaStatesField != null)
        {
            var vaStates = _vaStatesField.GetValue(ds) as Dictionary<int, UnitAnimationPlaybackState>;
            var vaActive  = _vaActiveKeysField?.GetValue(ds) as HashSet<int>;

            if (vaStates != null)
            {
                foreach ((int creationIndex, UnitAnimationPlaybackState state) in vaStates)
                {
                    if (!(vaActive?.Contains(creationIndex) ?? false)) continue;
                    if (!indexByCreation.TryGetValue(creationIndex, out int ei)) continue;

                    ref CoreComponent core = ref whole.coreComponent[ei];
                    if (!core.Active) continue;
                    if ((core.Type & (UnitType.Building | UnitType.Projectile)) != 0) continue;

                    string stateName = state.CurrentState.ToString();
                    if (ShowOnlyMoving && state.CurrentState != UnitAnimationStateId.Move) continue;

                    _labels.Add(new LabelData
                    {
                        WorldPos  = new Vector3(core.Position.x, core.Position.y, 0f),
                        StateName = stateName,
                        Color     = StateToColor(state.CurrentState)
                    });
                    _totalActive++;
                }
            }
        }

        // ── Atlas entities ──
        if (_reflectionReady && _atlasStatesField != null)
        {
            var atlasStates = _atlasStatesField.GetValue(ds) as Dictionary<int, UnitAnimationPlaybackState>;
            var atlasActive = _atlasActiveKeysField?.GetValue(ds) as HashSet<int>;

            if (atlasStates != null)
            {
                foreach ((int key, UnitAnimationPlaybackState state) in atlasStates)
                {
                    if (!(atlasActive?.Contains(key) ?? false)) continue;
                    // Atlas playback key is creationIndex.
                    if (!indexByCreation.TryGetValue(key, out int ei)) continue;

                    ref CoreComponent core = ref whole.coreComponent[ei];
                    if (!core.Active) continue;
                    if ((core.Type & (UnitType.Building | UnitType.Projectile)) != 0) continue;

                    // Don't double-count entities that already have VA labels.
                    // VA path takes priority — check if this creationIndex already added.
                    bool alreadyLabeled = false;
                    for (int li = 0; li < _labels.Count; li++)
                    {
                        // Quick check: approximate position match.
                        Vector3 pos = new Vector3(core.Position.x, core.Position.y, 0f);
                        if (Vector3.Distance(_labels[li].WorldPos, pos) < 0.1f)
                        {
                            alreadyLabeled = true;
                            break;
                        }
                    }
                    if (alreadyLabeled) continue;

                    string stateName = state.CurrentState.ToString();
                    if (ShowOnlyMoving && state.CurrentState != UnitAnimationStateId.Move) continue;

                    _labels.Add(new LabelData
                    {
                        WorldPos  = new Vector3(core.Position.x, core.Position.y, 0f),
                        StateName = stateName,
                        Color     = StateToColor(state.CurrentState)
                    });
                    _totalActive++;
                }
            }
        }
    }

    private Color StateToColor(UnitAnimationStateId state)
    {
        return state switch
        {
            UnitAnimationStateId.Death  => DeathColor,
            UnitAnimationStateId.Attack => AttackColor,
            UnitAnimationStateId.Work   => WorkColor,
            UnitAnimationStateId.Move   => MoveColor,
            _                           => IdleColor
        };
    }

    // ------------------------------------------------------------------
    // Inspector list (throttled)
    // ------------------------------------------------------------------

    private void RefreshInspectorList()
    {
        _entityStates.Clear();
        for (int i = 0; i < _labels.Count && _entityStates.Count < 128; i++)
        {
            _entityStates.Add(new EntityDebugEntry
            {
                Blueprint     = "?",
                CreationIndex = i,
                State         = _labels[i].StateName
            });
        }
    }

    private void RefreshInterceptorChainView()
    {
        VAInterceptorChain.EnsureInitialized();
        IReadOnlyList<VAInterceptorInfo> list = VAInterceptorChain.Interceptors;

        while (_interceptorChain.Count < list.Count)
            _interceptorChain.Add(default);
        while (_interceptorChain.Count > list.Count)
            _interceptorChain.RemoveAt(_interceptorChain.Count - 1);

        for (int i = 0; i < list.Count; i++)
        {
            _interceptorChain[i] = new InterceptorDebugEntry
            {
                Name        = list[i].Name,
                Priority    = list[i].Priority,
                Enabled     = list[i].Enabled,
                Description = list[i].Description
            };
        }
    }

    // ------------------------------------------------------------------
    // OnGUI helpers
    // ------------------------------------------------------------------

    private void DrawLabel(float x, float y, string text, Color bgColor)
    {
        GUIStyle boxStyle = GUI.skin.box;
        boxStyle.fontSize = FontSize;
        boxStyle.alignment = TextAnchor.MiddleCenter;
        Vector2 size = boxStyle.CalcSize(new GUIContent(text));

        float w = size.x + 12f;
        float h = size.y + 4f;
        Rect r = new Rect(x - w / 2f, y + 8f, w, h);

        GUI.color = bgColor;
        GUI.Box(r, "");
        GUI.color = Color.white;

        GUIStyle lblStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = FontSize,
            alignment = TextAnchor.MiddleCenter,
            normal = { textColor = Color.white }
        };
        GUI.Label(r, text, lblStyle);
    }
}
