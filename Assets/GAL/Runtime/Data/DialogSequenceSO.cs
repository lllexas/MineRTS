using System.Collections.Generic;
using UnityEngine;

namespace GAL
{
    /// <summary>
    /// DialogSequenceSO - 对话序列资源
    ///
    /// CSV 源文件放在 Resources 目录下，直接拖拽引用
    /// </summary>
    [CreateAssetMenu(fileName = "NewDialogSequence", menuName = "GAL01/Dialog/Dialog Sequence")]
    public class DialogSequenceSO : ScriptableObject
    {
        [Tooltip("CSV 源文件（必须放在 Resources 目录下）")]
        public TextAsset CsvSource;

        [Tooltip("序列标识，用于生成 DialogEntry 的 ID 前缀")]
        public string SequenceId;

        [Tooltip("描述信息")]
        [TextArea(2, 4)]
        public string Description;

        [SerializeReference]
        [Tooltip("混合条目序列（对话文本 + 演出效果）")]
        public List<ISequenceEntry> Entries = new();

        public DialogEntry GetDialogEntry(int dialogIndex)
        {
            int count = 0;
            foreach (ISequenceEntry entry in Entries)
            {
                if (entry is not DialogEntry dialog)
                    continue;

                if (count == dialogIndex)
                    return dialog;

                count++;
            }

            return null;
        }

        public int DialogCount
        {
            get
            {
                int count = 0;
                foreach (ISequenceEntry entry in Entries)
                {
                    if (entry is DialogEntry)
                        count++;
                }

                return count;
            }
        }

        public DialogEntry FindDialogById(string id)
        {
            foreach (ISequenceEntry entry in Entries)
            {
                if (entry is DialogEntry dialog && dialog.Id == id)
                    return dialog;
            }

            return null;
        }
    }
}

