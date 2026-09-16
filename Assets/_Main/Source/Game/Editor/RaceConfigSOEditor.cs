using UnityEditor;
using UnityEngine.UIElements;

namespace MatchRacers.Editor
{
    [CustomEditor(typeof(RaceConfigSO))]
    public sealed class RaceConfigSOEditor : UnityEditor.Editor
    {
        public override VisualElement CreateInspectorGUI()
        {
            return null;
        }

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
        }
    }
}
