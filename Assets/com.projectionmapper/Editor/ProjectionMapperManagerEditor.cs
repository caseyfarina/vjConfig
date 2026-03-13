using UnityEditor;
using UnityEngine;

namespace ProjectionMapper.Editor
{
    [CustomEditor(typeof(ProjectionMapperManager))]
    public class ProjectionMapperManagerEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            var mgr = (ProjectionMapperManager)target;

            EditorGUILayout.HelpBox(
                "Projection Mapper\n" +
                "Press the GUI Toggle Key at runtime to open the config panel.\n" +
                "Surfaces are configured via the runtime GUI.\n" +
                "Profiles are saved to Application.persistentDataPath.",
                MessageType.Info);

            EditorGUILayout.Space();
            mgr.guiToggleKey = (KeyCode)EditorGUILayout.EnumPopup("GUI Toggle Key", mgr.guiToggleKey);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Surfaces", mgr.surfaces.Count.ToString());
            EditorGUILayout.LabelField("Profile", mgr.CurrentProfileName);
            EditorGUILayout.LabelField("Save Path", ProjectionPersistence.GetFilePath());

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Surface Preview", EditorStyles.boldLabel);
            string[] cLabels = { "TL", "TR", "BR", "BL" };
            for (int i = 0; i < mgr.surfaces.Count; i++)
            {
                var s = mgr.surfaces[i];
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.LabelField(s.name, EditorStyles.boldLabel);
                EditorGUI.indentLevel++;
                EditorGUILayout.LabelField("Display", s.targetDisplay.ToString());
                EditorGUILayout.LabelField("Source", s.sourceMode.ToString());
                EditorGUILayout.LabelField("AA", s.aaQuality.ToString());
                for (int c = 0; c < 4; c++)
                    EditorGUILayout.LabelField($"  {cLabels[c]}: ({s.corners[c].x:F4}, {s.corners[c].y:F4})");
                EditorGUI.indentLevel--;
                EditorGUILayout.EndVertical();
            }

            if (GUI.changed) EditorUtility.SetDirty(mgr);
        }
    }
}
