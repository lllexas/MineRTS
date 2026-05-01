using UnityEngine;

namespace GAL
{
/// <summary>
/// 背景预设 - 供对话序列引用的包装资源
/// </summary>
[CreateAssetMenu(fileName = "NewBackgroundPreset", menuName = "GAL01/Dialog/Background Preset")]
public class BackgroundPresetSO : ScriptableObject
{
    [Tooltip("预设标识，可用于日志和后续扩展")]
    public string Id;

    [Tooltip("主背景图")]
    public Sprite MainSprite;

    [Tooltip("默认淡入方式")]
    public FadeType DefaultFade = FadeType.Instant;

    [TextArea(2, 4)]
    [Tooltip("备注")]
    public string Note;
}
}
