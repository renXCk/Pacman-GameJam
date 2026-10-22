using Game.Ghosts;
using UnityEditor;
using UnityEngine;

namespace Game.EditorTools
{
    // Live read-outs in Play mode, for debugging the ghosts without extra logging.
    [CustomEditor(typeof(Ghost))]
    sealed class GhostEditor : Editor
    {
        public override bool RequiresConstantRepaint() => Application.isPlaying;

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            if (!Application.isPlaying)
                return;

            var ghost = (Ghost)target;
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Live", EditorStyles.boldLabel);

            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.EnumPopup("State", ghost.State);
                EditorGUILayout.Toggle("Frozen", ghost.IsFrozen);
                EditorGUILayout.Toggle("Lit by flashlight", ghost.IsLit);
                EditorGUILayout.Vector3Field("Destination", ghost.Destination);
                EditorGUILayout.IntField("Destination updates", ghost.DestinationUpdates);
            }
        }
    }

    [CustomEditor(typeof(GhostDirector))]
    sealed class GhostDirectorEditor : Editor
    {
        public override bool RequiresConstantRepaint() => Application.isPlaying;

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            if (!Application.isPlaying)
                return;

            var director = (GhostDirector)target;
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Live", EditorStyles.boldLabel);

            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.Toggle("Round running", director.IsRunning);
                EditorGUILayout.EnumPopup("Mode", director.Mode);
                EditorGUILayout.FloatField("Seconds left in mode", director.ModeSecondsLeft);
                EditorGUILayout.FloatField("Round time", director.RoundTime);
                EditorGUILayout.IntField("Banish combo", director.Combo);
            }
        }
    }
}
