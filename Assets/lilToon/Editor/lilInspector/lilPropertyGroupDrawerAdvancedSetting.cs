#if UNITY_EDITOR
using UnityEditor;

namespace lilToon
{
    public partial class lilToonInspector
    {
        private void DrawSpecularMode()
        {
            int specularMode = 0;
            if(specularToon.floatValue == 0.0f) specularMode = 1;
            if(specularToon.floatValue == 1.0f) specularMode = 2;
            if(applySpecular.floatValue == 0.0f) specularMode = 0;
            EditorGUI.BeginChangeCheck();
            EditorGUI.showMixedValue = specularToon.hasMixedValue || applySpecular.hasMixedValue;
            specularMode = lilEditorGUI.Popup(GetLoc("sSpecularMode"), specularMode, new string[] {
                GetLoc("sSpecularNone"),
                GetLoc("sSpecularReal"),
                GetLoc("sSpecularToon")
            });
            EditorGUI.showMixedValue = false;
            if(EditorGUI.EndChangeCheck())
            {
                if(specularMode == 0)
                {
                    applySpecular.floatValue = 0.0f;
                    specularToon.floatValue = 0.0f;
                }
                if(specularMode == 1)
                {
                    applySpecular.floatValue = 1.0f;
                    specularToon.floatValue = 0.0f;
                }
                if(specularMode == 2)
                {
                    applySpecular.floatValue = 1.0f;
                    specularToon.floatValue = 1.0f;
                }
            }

            if(specularMode == 1)
            {
                EditorGUI.indentLevel++;
                LocalizedProperty(specularNormalStrength);
                LocalizedProperty(applySpecularFA);
                EditorGUI.indentLevel--;
            }
            if(specularMode == 2)
            {
                EditorGUI.indentLevel++;
                LocalizedProperty(specularNormalStrength);
                LocalizedProperty(specularBorder);
                LocalizedProperty(specularBlur);
                LocalizedProperty(applySpecularFA);
                EditorGUI.indentLevel--;
            }
        }
    }
}
#endif
