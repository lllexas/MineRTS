using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(CameraProfileDriver))]
public class CameraProfileDriverEditor : Editor
{
    private enum PendingAction
    {
        None,
        Apply,
        Capture,
        CreateFromCurrent
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        CameraProfileDriver driver = (CameraProfileDriver)target;
        SerializedProperty bindingsProperty = serializedObject.FindProperty("_bindings");
        PendingAction pendingAction = PendingAction.None;
        int pendingIndex = -1;

        DrawHeader(driver);

        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("State Bindings", EditorStyles.boldLabel);

        for (int i = 0; i < bindingsProperty.arraySize; i++)
        {
            SerializedProperty bindingProperty = bindingsProperty.GetArrayElementAtIndex(i);
            SerializedProperty stateProperty = bindingProperty.FindPropertyRelative("State");
            SerializedProperty profileProperty = bindingProperty.FindPropertyRelative("ProfileSO");

            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField(stateProperty.enumDisplayNames[stateProperty.enumValueIndex], EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(profileProperty, GUIContent.none);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Apply"))
            {
                pendingAction = PendingAction.Apply;
                pendingIndex = i;
            }

            if (GUILayout.Button("Capture"))
            {
                pendingAction = PendingAction.Capture;
                pendingIndex = i;
            }

            if (GUILayout.Button("New From Current"))
            {
                pendingAction = PendingAction.CreateFromCurrent;
                pendingIndex = i;
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
        }

        serializedObject.ApplyModifiedProperties();

        if (pendingAction != PendingAction.None && pendingIndex >= 0)
        {
            ExecutePendingAction(driver, bindingsProperty, pendingAction, pendingIndex);
            GUIUtility.ExitGUI();
        }
    }

    private static void DrawHeader(CameraProfileDriver driver)
    {
        using (new EditorGUI.DisabledScope(true))
        {
            EditorGUILayout.ObjectField("Target Camera", driver.TargetCamera, typeof(Camera), true);
            EditorGUILayout.ObjectField("Camera Controller", driver.CameraController, typeof(CameraController), true);
        }
    }

    private static void ExecutePendingAction(
        CameraProfileDriver driver,
        SerializedProperty bindingsProperty,
        PendingAction pendingAction,
        int pendingIndex)
    {
        SerializedProperty bindingProperty = bindingsProperty.GetArrayElementAtIndex(pendingIndex);
        SerializedProperty stateProperty = bindingProperty.FindPropertyRelative("State");
        SerializedProperty profileProperty = bindingProperty.FindPropertyRelative("ProfileSO");
        GameFlowController.GameState state = (GameFlowController.GameState)stateProperty.enumValueIndex;

        switch (pendingAction)
        {
            case PendingAction.Apply:
                driver.ApplyProfileForState(state);
                EditorUtility.SetDirty(driver.gameObject);
                if (driver.TargetCamera != null)
                {
                    EditorUtility.SetDirty(driver.TargetCamera);
                }
                if (driver.CameraController != null)
                {
                    EditorUtility.SetDirty(driver.CameraController);
                }
                SceneView.RepaintAll();
                break;

            case PendingAction.Capture:
                if (driver.CaptureProfileForState(state) && profileProperty.objectReferenceValue != null)
                {
                    EditorUtility.SetDirty(profileProperty.objectReferenceValue);
                    AssetDatabase.SaveAssets();
                }
                break;

            case PendingAction.CreateFromCurrent:
                CreateProfileAssetFromCurrent(driver, state, profileProperty);
                break;
        }
    }

    private static void CreateProfileAssetFromCurrent(
        CameraProfileDriver driver,
        GameFlowController.GameState state,
        SerializedProperty profileProperty)
    {
        string defaultName = $"{state}CameraProfileSO.asset";
        string path = EditorUtility.SaveFilePanelInProject(
            "Create Camera Profile",
            defaultName,
            "asset",
            "Create a new camera profile asset.");

        if (string.IsNullOrEmpty(path))
        {
            return;
        }

        CameraProfileSO profile = CreateInstance<CameraProfileSO>();
        profile.ProfileId = state.ToString();
        AssetDatabase.CreateAsset(profile, path);
        profile.CaptureFrom(driver.TargetCamera, driver.CameraController);
        profileProperty.objectReferenceValue = profile;
        profileProperty.serializedObject.ApplyModifiedProperties();
        EditorUtility.SetDirty(profile);
        AssetDatabase.SaveAssets();
    }
}
