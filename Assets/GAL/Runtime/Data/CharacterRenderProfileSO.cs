using System;
using System.Collections.Generic;
using UnityEngine;

namespace GAL
{
/// <summary>
/// 角色立绘显示配置 - 统一管理各情绪立绘及其显示参数
/// </summary>
[CreateAssetMenu(fileName = "NewCharacterRenderProfile", menuName = "GAL01/Dialog/Character Render Profile")]
public class CharacterRenderProfileSO : ScriptableObject
{
    [Tooltip("角色唯一标识，用于对话系统中的角色关联")]
    public string CharacterId;

    [Tooltip("角色立绘预设列表（按情绪/状态区分）")]
    public List<CharacterAvatarPreset> Avatars = new();

    public bool TryGetAvatarPreset(string emotionKey, out CharacterAvatarPreset preset)
    {
        if (Avatars != null)
        {
            for (int i = 0; i < Avatars.Count; i++)
            {
                CharacterAvatarPreset item = Avatars[i];
                if (item == null || string.IsNullOrWhiteSpace(item.EmotionKey))
                    continue;
                if (string.Equals(item.EmotionKey.Trim(), emotionKey?.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    preset = item;
                    return true;
                }
            }

            for (int i = 0; i < Avatars.Count; i++)
            {
                CharacterAvatarPreset item = Avatars[i];
                if (item?.Sprite == null) continue;
                preset = item;
                return true;
            }
        }

        preset = null;
        return false;
    }
}

[Serializable]
public class CharacterAvatarPreset
{
    [Tooltip("情绪/状态键（例如 normal/smile/angry）")]
    public string EmotionKey;

    [Tooltip("立绘图")]
    public Sprite Sprite;

    [Tooltip("UI 偏移（Anchored Position）")]
    public Vector2 AnchoredOffset;

    [Tooltip("缩放倍率")]
    public float Scale = 1f;

    [Tooltip("是否覆盖默认 Pivot")]
    public bool OverridePivot;

    [Tooltip("自定义 Pivot（0~1）")]
    public Vector2 Pivot01 = new(0.5f, 0.5f);

    [Tooltip("Image.preserveAspect")]
    public bool PreserveAspect = true;
}
}
