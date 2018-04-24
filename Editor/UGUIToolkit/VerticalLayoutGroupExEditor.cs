using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UI;
using UnityEngine;
using UnityEngine.UI;

[CustomEditor(typeof(VerticalLayoutGroupEx), true)]
[CanEditMultipleObjects]
public class VerticalLayoutGroupExEditor : HorizontalOrVerticalLayoutGroupEditor
{
    SerializedProperty m_MaxSize;
    SerializedProperty m_Spacing;
    SerializedProperty m_ChildAlignment;
    SerializedProperty m_ChildControlWidth;
    SerializedProperty m_ChildControlHeight;
    SerializedProperty m_ChildForceExpandWidth;
    SerializedProperty m_ChildForceExpandHeight;

    protected override void OnEnable()
    {
        base.OnEnable();
        m_MaxSize = serializedObject.FindProperty("m_MaxSize");
    }

    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();
        serializedObject.Update();
        EditorGUILayout.PropertyField(m_MaxSize, true);
        serializedObject.ApplyModifiedProperties();
    }
}
