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
        private const float CollapsedHeight = 30.0f;        // 只留标题条

        private readonly List<lilMaterialManagerLogEntry> entries = new List<lilMaterialManagerLogEntry>();
        private Vector2 scroll;
        private bool expanded = true;
        private float height = 150.0f;

        // 合并连续改动用（拖动滑条：值一变一变地走，日志只留一条）
        private lilMaterialManagerLogEntry mergingEntry;
        private string mergingProperty;
        private string mergingOldValue;
        private int mergingCount;

        private static GUIStyle logStyle;
        private static GUIStyle warningStyle;

        public static float HeaderHeight { get { return CollapsedHeight; } }
        public float Height { get { return expanded ? height : CollapsedHeight; } }
        public int Count { get { return entries.Count; } }

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

        // summary：画在标题条右端的选中摘要
        public void Draw(Rect rect, string summary)
        {
            GUILayout.BeginArea(rect);
            lilMaterialManagerStyles.DrawSectionHeader(ref expanded, "日志", summary, new Color(0.20f, 0.20f, 0.24f));

            if(expanded)
            {
                using(new EditorGUILayout.HorizontalScope())
                {
                    if(GUILayout.Button("清空", EditorStyles.miniButton, GUILayout.Width(44.0f))) Clear();
                    GUILayout.Label("改动即时生效，Ctrl+Z 撤销；没吃到的材质会在这里点名", EditorStyles.miniLabel);
                }

                scroll = EditorGUILayout.BeginScrollView(scroll);
                if(entries.Count == 0)
                {
                    EditorGUILayout.LabelField("还没有记录。", EditorStyles.miniLabel);
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
            }

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
