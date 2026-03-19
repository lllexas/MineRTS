using UnityEngine;
using UnityEditor;
using TMPro;
using System.Collections.Generic;
using System.Linq;

namespace MineRTS.Editor.Tools
{
    /// <summary>
    /// TMP 字体批量替换工具
    /// 
    /// <para>功能：批量替换 Hierarchy 中选中的物体及其子物体的 TMP_Text 字体</para>
    /// <para>位置：Tools > MineRTS > TMP字体批量替换</para>
    /// </summary>
    public class TMPFontBatchReplacer : EditorWindow
    {
        [SerializeField] private TMP_FontAsset targetFont;
        [SerializeField] private bool includeInactive = true;
        [SerializeField] private bool showDetailedLog = true;

        private Vector2 scrollPosition;
        private List<ReplaceResult> lastResults = new List<ReplaceResult>();
        private bool hasResults = false;

        private class ReplaceResult
        {
            public string GameObjectName;
            public string GameObjectPath;
            public string OldFontName;
            public bool Success;
            public string ErrorMessage;
        }

        [MenuItem("Tools/MineRTS/TMP字体批量替换", false, 100)]
        public static void ShowWindow()
        {
            var window = GetWindow<TMPFontBatchReplacer>("TMP字体替换");
            window.minSize = new Vector2(400, 300);
            window.Show();
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(10);

            // 标题
            GUIStyle titleStyle = new GUIStyle(EditorStyles.largeLabel)
            {
                fontSize = 16,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            EditorGUILayout.LabelField("🐱 TMP 字体批量替换工具", titleStyle);

            EditorGUILayout.Space(10);

            // 设置区域
            EditorGUILayout.BeginVertical(GUI.skin.box);
            EditorGUILayout.LabelField("设置", EditorStyles.boldLabel);

            targetFont = EditorGUILayout.ObjectField(
                new GUIContent("目标字体", "要替换成的字体"),
                targetFont,
                typeof(TMP_FontAsset),
                false
            ) as TMP_FontAsset;

            includeInactive = EditorGUILayout.Toggle(
                new GUIContent("包含未激活物体", "是否替换未激活的物体上的字体"),
                includeInactive
            );

            showDetailedLog = EditorGUILayout.Toggle(
                new GUIContent("显示详细日志", "是否显示每个替换的详细信息"),
                showDetailedLog
            );

            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(10);

            // 操作按钮区域
            EditorGUILayout.BeginVertical(GUI.skin.box);
            EditorGUILayout.LabelField("操作", EditorStyles.boldLabel);

            GUIStyle buttonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 12,
                fontStyle = FontStyle.Bold,
                padding = new RectOffset(10, 10, 8, 8)
            };

            EditorGUILayout.BeginHorizontal();

            // 替换选中按钮
            GUI.backgroundColor = new Color(0.4f, 0.8f, 1f, 1f);
            if (GUILayout.Button("🎯 替换选中物体", buttonStyle, GUILayout.Height(40)))
            {
                ReplaceSelected();
            }
            GUI.backgroundColor = Color.white;

            // 替换场景中所有按钮
            GUI.backgroundColor = new Color(1f, 0.6f, 0.4f, 1f);
            if (GUILayout.Button("🌍 替换场景中所有", buttonStyle, GUILayout.Height(40)))
            {
                if (EditorUtility.DisplayDialog(
                    "确认替换",
                    "确定要替换场景中所有 TMP_Text 的字体吗？\n这可能需要一些时间。",
                    "确定",
                    "取消"))
                {
                    ReplaceAllInScene();
                }
            }
            GUI.backgroundColor = Color.white;

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(10);

            // 结果显示区域
            if (hasResults && lastResults != null)
            {
                DrawResults();
            }

            EditorGUILayout.Space(10);

            // 使用说明
            EditorGUILayout.BeginVertical(GUI.skin.box);
            EditorGUILayout.LabelField("使用说明", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("1. 在 Hierarchy 中选择要替换的父物体", EditorStyles.miniLabel);
            EditorGUILayout.LabelField("2. 将目标字体拖到上面的字段中", EditorStyles.miniLabel);
            EditorGUILayout.LabelField("3. 点击 '🎯 替换选中物体' 按钮", EditorStyles.miniLabel);
            EditorGUILayout.LabelField("4. 所有子物体中的 TMP_Text 字体都会被替换", EditorStyles.miniLabel);
            EditorGUILayout.EndVertical();
        }

        private void DrawResults()
        {
            EditorGUILayout.BeginVertical(GUI.skin.box);
            EditorGUILayout.LabelField($"替换结果 (共 {lastResults.Count} 个)", EditorStyles.boldLabel);

            // 统计信息
            int successCount = lastResults.Count(r => r.Success);
            int failCount = lastResults.Count - successCount;

            EditorGUILayout.BeginHorizontal();
            GUI.color = Color.green;
            EditorGUILayout.LabelField($"✅ 成功: {successCount}", EditorStyles.boldLabel);
            GUI.color = Color.red;
            EditorGUILayout.LabelField($"❌ 失败: {failCount}", EditorStyles.boldLabel);
            GUI.color = Color.white;
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(5);

            // 详细列表
            if (showDetailedLog)
            {
                scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUILayout.Height(150));

                foreach (var result in lastResults)
                {
                    if (result == null) continue;

                    EditorGUILayout.BeginHorizontal();

                    if (result.Success)
                    {
                        GUI.color = new Color(0.6f, 1f, 0.6f);
                        EditorGUILayout.LabelField("✓", GUILayout.Width(20));
                    }
                    else
                    {
                        GUI.color = new Color(1f, 0.6f, 0.6f);
                        EditorGUILayout.LabelField("✗", GUILayout.Width(20));
                    }

                    GUI.color = Color.white;

                    // 显示物体名称
                    EditorGUILayout.LabelField(result.GameObjectName, GUILayout.Width(120));

                    if (!result.Success)
                    {
                        EditorGUILayout.LabelField($"[{result.ErrorMessage}]", EditorStyles.miniLabel);
                    }
                    else if (!string.IsNullOrEmpty(result.OldFontName))
                    {
                        EditorGUILayout.LabelField($"({result.OldFontName} → {targetFont?.name ?? "None"})", EditorStyles.miniLabel);
                    }

                    EditorGUILayout.EndHorizontal();
                }

                EditorGUILayout.EndScrollView();
            }

            EditorGUILayout.EndVertical();
        }

        /// <summary>
        /// 替换选中物体及其子物体的字体
        /// </summary>
        private void ReplaceSelected()
        {
            if (targetFont == null)
            {
                EditorUtility.DisplayDialog("错误", "请先选择目标字体！", "确定");
                return;
            }

            GameObject[] selectedObjects = Selection.gameObjects;
            if (selectedObjects.Length == 0)
            {
                EditorUtility.DisplayDialog("错误", "请先在 Hierarchy 中选择至少一个物体！", "确定");
                return;
            }

            lastResults = new List<ReplaceResult>();

            // 收集所有需要处理的物体
            List<GameObject> allTargets = new List<GameObject>();
            foreach (var go in selectedObjects)
            {
                if (go == null) continue;
                allTargets.Add(go);
                // 获取所有子物体
                var children = go.GetComponentsInChildren<Transform>(includeInactive);
                foreach (var child in children)
                {
                    if (child != null && child.gameObject != go)
                    {
                        allTargets.Add(child.gameObject);
                    }
                }
            }

            // 去重
            allTargets = allTargets.Distinct().ToList();

            // 执行替换
            int totalCount = 0;
            Undo.SetCurrentGroupName("批量替换 TMP 字体");
            int group = Undo.GetCurrentGroup();

            foreach (var target in allTargets)
            {
                if (target == null) continue;

                var texts = target.GetComponents<TMP_Text>();
                foreach (var text in texts)
                {
                    if (text == null) continue;

                    var result = new ReplaceResult
                    {
                        GameObjectName = target.name,
                        GameObjectPath = GetGameObjectPath(target),
                        OldFontName = text.font != null ? text.font.name : "None"
                    };

                    try
                    {
                        Undo.RecordObject(text, "替换字体");
                        text.font = targetFont;
                        EditorUtility.SetDirty(text);
                        result.Success = true;
                        totalCount++;
                    }
                    catch (System.Exception e)
                    {
                        result.Success = false;
                        result.ErrorMessage = e.Message;
                    }

                    lastResults.Add(result);
                }
            }

            Undo.CollapseUndoOperations(group);

            hasResults = true;
            Repaint();
            EditorUtility.DisplayDialog("完成", $"替换完成！\n共处理了 {allTargets.Count} 个物体，\n替换了 {totalCount} 个 TMP_Text 的字体。", "确定");
        }

        /// <summary>
        /// 替换场景中所有 TMP_Text 的字体
        /// </summary>
        private void ReplaceAllInScene()
        {
            if (targetFont == null)
            {
                EditorUtility.DisplayDialog("错误", "请先选择目标字体！", "确定");
                return;
            }

            lastResults = new List<ReplaceResult>();

            // 获取场景中所有 TMP_Text
            var allTexts = FindObjectsOfType<TMP_Text>(includeInactive);

            Undo.SetCurrentGroupName("批量替换场景中所有 TMP 字体");
            int group = Undo.GetCurrentGroup();

            int count = 0;
            foreach (var text in allTexts)
            {
                if (text == null) continue;

                var result = new ReplaceResult
                {
                    GameObjectName = text.gameObject.name,
                    GameObjectPath = GetGameObjectPath(text.gameObject),
                    OldFontName = text.font != null ? text.font.name : "None"
                };

                try
                {
                    Undo.RecordObject(text, "替换字体");
                    text.font = targetFont;
                    EditorUtility.SetDirty(text);
                    result.Success = true;
                    count++;
                }
                catch (System.Exception e)
                {
                    result.Success = false;
                    result.ErrorMessage = e.Message;
                }

                lastResults.Add(result);
            }

            Undo.CollapseUndoOperations(group);

            hasResults = true;
            Repaint();
            EditorUtility.DisplayDialog("完成", $"场景替换完成！\n共替换了 {count} 个 TMP_Text 的字体。", "确定");
        }

        private string GetGameObjectPath(GameObject go)
        {
            if (go == null) return "null";
            string path = go.name;
            Transform parent = go.transform.parent;
            while (parent != null)
            {
                path = parent.name + "/" + path;
                parent = parent.parent;
            }
            return path;
        }
    }
}
