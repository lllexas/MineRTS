using UnityEngine;
using TMPro;

namespace MineRTS.BigMap.UI.Panels
{
    /// <summary>
    /// SocialRootManager - 社交根面板管理器
    /// 
    /// <para>【职责】管理 SocialRootPanel 的显示内容</para>
    /// <para>【TUI 风格】使用制表符和 ASCII 艺术字显示</para>
    /// </summary>
    public class SocialRootManager : MonoBehaviour
    {
        [Header("UI 引用")]
        [Tooltip("显示文本的 TMP_Text 组件")]
        [SerializeField] private TMP_Text displayText;

        [Header("显示设置")]
        [Tooltip("标题颜色")]
        [SerializeField] private Color titleColor = new Color(0.2f, 0.8f, 1f);

        [Tooltip("边框颜色")]
        [SerializeField] private Color borderColor = new Color(0.4f, 0.4f, 0.4f);

        [Tooltip("背景填充颜色")]
        [SerializeField] private Color fillColor = new Color(0.1f, 0.1f, 0.15f);

        private void Start()
        {
            if (displayText == null)
            {
                displayText = GetComponent<TMP_Text>();
            }

            RenderSocialCLI();
        }

        /// <summary>
        /// 渲染 SocialCLI 的 TUI 界面
        /// </summary>
        private void RenderSocialCLI()
        {
            if (displayText == null) return;

            string titleHex = ColorUtility.ToHtmlStringRGB(titleColor);
            string borderHex = ColorUtility.ToHtmlStringRGB(borderColor);
            string fillHex = ColorUtility.ToHtmlStringRGB(fillColor);

            // 使用制表符和 ASCII 艺术字拼出 SocialCLI
            string tui = $@"
<color=#{borderHex}>┌────────────────────────────────────────┐</color>
<color=#{borderHex}>│</color><color=#{fillHex}>                                        </color><color=#{borderHex}>│</color>
<color=#{borderHex}>│</color>  <color=#{titleHex}>███████╗ ██████╗  ██████╗██╗ █████╗ ██╗</color>  <color=#{borderHex}>│</color>
<color=#{borderHex}>│</color>  <color=#{titleHex}>██╔════╝██╔═══██╗██╔════╝██║██╔══██╗██║</color>  <color=#{borderHex}>│</color>
<color=#{borderHex}>│</color>  <color=#{titleHex}>███████╗██║   ██║██║     ██║███████║██║</color>  <color=#{borderHex}>│</color>
<color=#{borderHex}>│</color>  <color=#{titleHex}>╚════██║██║   ██║██║     ██║██╔══██║██║</color>  <color=#{borderHex}>│</color>
<color=#{borderHex}>│</color>  <color=#{titleHex}>███████║╚██████╔╝╚██████╗██║██║  ██║██║</color>  <color=#{borderHex}>│</color>
<color=#{borderHex}>│</color>  <color=#{titleHex}>╚══════╝ ╚═════╝  ╚═════╝╚═╝╚═╝  ╚═╝╚═╝</color>  <color=#{borderHex}>│</color>
<color=#{borderHex}>│</color><color=#{fillHex}>                                        </color><color=#{borderHex}>│</color>
<color=#{borderHex}>│</color>  <color=#{titleHex}> ██████╗██╗     ██╗                    </color>  <color=#{borderHex}>│</color>
<color=#{borderHex}>│</color>  <color=#{titleHex}>██╔════╝██║     ██║                    </color>  <color=#{borderHex}>│</color>
<color=#{borderHex}>│</color>  <color=#{titleHex}>██║     ██║     ██║                    </color>  <color=#{borderHex}>│</color>
<color=#{borderHex}>│</color>  <color=#{titleHex}>██║     ██║     ██║                    </color>  <color=#{borderHex}>│</color>
<color=#{borderHex}>│</color>  <color=#{titleHex}>╚██████╗███████╗██║                    </color>  <color=#{borderHex}>│</color>
<color=#{borderHex}>│</color>  <color=#{titleHex}> ╚═════╝╚══════╝╚═╝                    </color>  <color=#{borderHex}>│</color>
<color=#{borderHex}>│</color><color=#{fillHex}>                                        </color><color=#{borderHex}>│</color>
<color=#{borderHex}>└────────────────────────────────────────┘</color>
";

            displayText.text = tui;
            displayText.richText = true;
            displayText.alignment = TextAlignmentOptions.Center;
        }

        /// <summary>
        /// 更新显示（可在运行时调用）
        /// </summary>
        public void Refresh()
        {
            RenderSocialCLI();
        }

        /// <summary>
        /// 设置自定义颜色
        /// </summary>
        public void SetColors(Color title, Color border, Color fill)
        {
            titleColor = title;
            borderColor = border;
            fillColor = fill;
            RenderSocialCLI();
        }
    }
}
