using UnityEngine;

namespace GAL
{
    /// <summary>
    /// 角色显示数据 - 用于 CharacterAnimator
    /// </summary>
    public class CharacterShowData
    {
        public int slotIndex;
        public string id;
        public CharacterRenderProfileSO profile;
        public string emotionKey;
        public bool fromLeft = true;
    }

    public class CharacterHideData
    {
        public int slotIndex;
    }
}
