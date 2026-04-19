using System;
using System.Collections.Generic;
using System.Linq;
using NekoGraph;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(StartBoots))]
public sealed class StartBootsEditor : Editor
{
    private SerializedProperty _entriesProperty;
    private List<Type> _facadeTypes;

    private void OnEnable()
    {
        _entriesProperty = serializedObject.FindProperty("_entries");
        _facadeTypes = TypeCache.GetTypesDerivedFrom<PackFacadeBase>()
            .Where(type => !type.IsAbstract && !type.IsGenericType)
            .OrderBy(type => type.Name)
            .ToList();
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.PropertyField(_entriesProperty, includeChildren: false);
        EditorGUI.indentLevel++;

        for (int i = 0; i < _entriesProperty.arraySize; i++)
        {
            DrawEntry(_entriesProperty.GetArrayElementAtIndex(i), i);
        }

        EditorGUI.indentLevel--;

        EditorGUILayout.Space();
        if (GUILayout.Button("Add Start Boot Entry"))
        {
            _entriesProperty.InsertArrayElementAtIndex(_entriesProperty.arraySize);
            var newEntry = _entriesProperty.GetArrayElementAtIndex(_entriesProperty.arraySize - 1);
            newEntry.FindPropertyRelative("Facade").managedReferenceValue = null;
            newEntry.FindPropertyRelative("Source").enumValueIndex = 0;
            newEntry.FindPropertyRelative("PackAsset").objectReferenceValue = null;
            newEntry.FindPropertyRelative("EmptyPackID").stringValue = string.Empty;
        }

        serializedObject.ApplyModifiedProperties();
    }

    private void DrawEntry(SerializedProperty entryProperty, int index)
    {
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"Entry {index + 1}", EditorStyles.boldLabel);
            if (GUILayout.Button("Remove", GUILayout.Width(80)))
            {
                _entriesProperty.DeleteArrayElementAtIndex(index);
                EditorGUILayout.EndHorizontal();
                return;
            }

            EditorGUILayout.EndHorizontal();

            var facadeProperty = entryProperty.FindPropertyRelative("Facade");
            DrawFacadeField(facadeProperty);

            EditorGUILayout.PropertyField(entryProperty.FindPropertyRelative("Source"));

            var sourceProperty = entryProperty.FindPropertyRelative("Source");
            var source = (StartBootSource)sourceProperty.enumValueIndex;

            if (source == StartBootSource.PackAsset)
            {
                EditorGUILayout.PropertyField(entryProperty.FindPropertyRelative("PackAsset"));
            }
            else
            {
                EditorGUILayout.PropertyField(entryProperty.FindPropertyRelative("EmptyPackID"));
            }
        }
    }

    private void DrawFacadeField(SerializedProperty facadeProperty)
    {
        string currentName = facadeProperty.managedReferenceValue?.GetType().Name ?? "(None)";

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.PrefixLabel("Facade");

        if (GUILayout.Button(currentName, EditorStyles.popup))
        {
            ShowFacadeMenu(facadeProperty);
        }

        using (new EditorGUI.DisabledScope(facadeProperty.managedReferenceValue == null))
        {
            if (GUILayout.Button("Clear", GUILayout.Width(60)))
            {
                facadeProperty.managedReferenceValue = null;
            }
        }

        EditorGUILayout.EndHorizontal();
    }

    private void ShowFacadeMenu(SerializedProperty facadeProperty)
    {
        var menu = new GenericMenu();

        foreach (var facadeType in _facadeTypes)
        {
            bool isCurrent = facadeProperty.managedReferenceValue?.GetType() == facadeType;
            menu.AddItem(new GUIContent(facadeType.Name), isCurrent, () =>
            {
                serializedObject.Update();
                facadeProperty.managedReferenceValue = Activator.CreateInstance(facadeType);
                serializedObject.ApplyModifiedProperties();
            });
        }

        if (_facadeTypes.Count == 0)
        {
            menu.AddDisabledItem(new GUIContent("No PackFacade types found"));
        }

        menu.ShowAsContext();
    }
}
