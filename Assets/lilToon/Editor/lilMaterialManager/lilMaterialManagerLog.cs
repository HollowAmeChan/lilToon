#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace lilToon
{
    //------------------------------------------------------------------------------------------------------------------------------
    // 材质管理器：内部日志控制台（中栏下半区）
    // 把"本次改动"和"诊断"合成一块，只做展示、不参与写入：
    //   * 改动记录：属性名  旧值 → 新值  ×N 个材质（拖动滑条时同一属性合并成一条）
    //   * 诊断：被跳过的材质点名（哪个选中的材质没吃到这次改动、为什么）
    //   * 标题条右端带选中摘要（原来工具条 / 右栏上方的那些数字都挪到这里）
    //------------------------------------------------------------------------------------------------------------------------------
    internal sealed class lilMaterialManagerLogEntry
    {
        public string time;
        public string message;
        public bool warning;
        public string newValue;     // 合并连续改动时用来判断"这一条是不是同一个属性的延续"
    }

    internal sealed class lilMaterialManagerLogView
    {
        private const int MaxEntries = 400;

        private readonly List<lilMaterialManagerLogEntry> entries = new List<lilMaterialManagerLogEntry>();
        private Vector2 scroll;

        // 合并连续改动用（拖动滑条：值一变一变地走，日志只留一条）
        private lilMaterialManagerLogEntry mergingEntry;
        private string mergingProperty;
        private string mergingOldValue;
        private int mergingCount;

        private static GUIStyle logStyle;
        private static GUIStyle warningStyle;
        private static GUIStyle groupSummaryStyle;

        public int Count { get { return entries.Count; } }
        public float ScrollY { get { return scroll.y; } }

        public void Clear()
        {
            entries.Clear();
            mergingEntry = null;
        }

        public void Add(string message, bool warning = false)
        {
            mergingEntry = null;
            entries.Insert(0, new lilMaterialManagerLogEntry
            {
                time = System.DateTime.Now.ToString("HH:mm:ss"),
                message = message,
                warning = warning
            });
            Trim();
        }

        // 记录一次属性改动（appliedCount = 真的写进去并回读确认的材质数）
        public void RecordChange(string propertyName, string oldValue, string newValue, int appliedCount)
        {
            if(mergingEntry != null && mergingProperty == propertyName && mergingCount == appliedCount && mergingEntry.newValue == oldValue)
            {
                mergingEntry.newValue = newValue;
                mergingEntry.message = BuildChangeMessage(propertyName, mergingOldValue, newValue, appliedCount);
                return;
            }

            var entry = new lilMaterialManagerLogEntry
            {
                time = System.DateTime.Now.ToString("HH:mm:ss"),
                newValue = newValue,
                message = BuildChangeMessage(propertyName, oldValue, newValue, appliedCount)
            };
            entries.Insert(0, entry);
            mergingEntry = entry;
            mergingProperty = propertyName;
            mergingOldValue = oldValue;
            mergingCount = appliedCount;
            Trim();
        }

        private static string BuildChangeMessage(string propertyName, string oldValue, string newValue, int appliedCount)
        {
            return propertyName + "   " + oldValue + " → " + newValue + "    ×" + appliedCount + " 个材质";
        }

        private void Trim()
        {
            while(entries.Count > MaxEntries) entries.RemoveAt(entries.Count - 1);
        }

        // 导出成纯文本（右键菜单"复制日志到剪贴板"）。
        // 注意方向：entries 是新的在前，导出成人读的顺序（旧 → 新）。
        public string BuildText()
        {
            if(entries.Count == 0) return string.Empty;

            var builder = new System.Text.StringBuilder();
            for(int i = entries.Count - 1; i >= 0; i--)
            {
                lilMaterialManagerLogEntry entry = entries[i];
                builder.Append(entry.time).Append("  ").Append(entry.message).Append('\n');
            }
            return builder.ToString();
        }

        // 永远展开，没有折叠头栏：顶部一行"分组构成"，下面是列表。
        // 高度由窗口的横向分隔条控制；清空 / 复制在右键菜单里（列表为空时会提示一句）。
        public void Draw(Rect rect, string groupsSummary)
        {
            GUILayout.BeginArea(rect);

            // 顶行：这批选中材质分了几组、各是什么 shader、各多少材质
            // （原来是画在右栏顶部的组标题，挪到这里，右栏就只剩属性列表）
            if(!string.IsNullOrEmpty(groupsSummary))
            {
                EditorGUILayout.LabelField(groupsSummary, GroupSummaryStyle);
            }

            scroll = EditorGUILayout.BeginScrollView(scroll);
            if(entries.Count == 0)
            {
                EditorGUILayout.LabelField("还没有记录。改动会记在这里；右键可清空或复制。", EditorStyles.miniLabel);
            }
            else
            {
                for(int i = 0; i < entries.Count; i++)
                {
                    lilMaterialManagerLogEntry entry = entries[i];
                    using(new EditorGUILayout.HorizontalScope())
                    {
                        GUILayout.Label(entry.time, EditorStyles.miniLabel, GUILayout.Width(52.0f));
                        GUILayout.Label(entry.message, entry.warning ? WarningStyle : LogStyle);
                    }
                }
            }
            EditorGUILayout.EndScrollView();

            GUILayout.EndArea();
        }

        private static GUIStyle LogStyle
        {
            get
            {
                if(logStyle == null) logStyle = new GUIStyle(EditorStyles.miniLabel) { wordWrap = true };
                return logStyle;
            }
        }

        // 顶部"分组构成"那一行：比日志正文稍微醒目一点，但不要抢
        private static GUIStyle GroupSummaryStyle
        {
            get
            {
                if(groupSummaryStyle == null)
                {
                    groupSummaryStyle = new GUIStyle(EditorStyles.miniLabel) { wordWrap = true };
                    groupSummaryStyle.normal.textColor = EditorGUIUtility.isProSkin ? new Color(0.72f, 0.76f, 0.82f) : new Color(0.28f, 0.30f, 0.34f);
                }
                return groupSummaryStyle;
            }
        }

        private static GUIStyle WarningStyle
        {
            get
            {
                if(warningStyle == null)
                {
                    warningStyle = new GUIStyle(EditorStyles.miniLabel) { wordWrap = true };
                    warningStyle.normal.textColor = EditorGUIUtility.isProSkin ? new Color(1.0f, 0.72f, 0.35f) : new Color(0.55f, 0.28f, 0.0f);
                }
                return warningStyle;
            }
        }
    }
}
#endif
