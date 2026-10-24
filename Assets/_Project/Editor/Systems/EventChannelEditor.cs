using Game.Events;
using UnityEditor;
using UnityEngine;

namespace Game.EditorTools
{
    // Every event channel asset shows how many listeners it has while the game runs, which makes
    // a missing subscription visible at a glance.
    [CustomEditor(typeof(EventChannelBase), true)]
    sealed class EventChannelEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            if (!Application.isPlaying)
                return;

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Listeners now", ((EventChannelBase)target).ListenerCount.ToString());
            Repaint();
        }
    }
}
