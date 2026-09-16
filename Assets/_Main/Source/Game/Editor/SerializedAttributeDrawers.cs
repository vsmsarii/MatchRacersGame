using UnityEditor;
using UnityEngine;

namespace MatchRacers.Editor
{
    [CustomPropertyDrawer(typeof(MinAttribute))]
    public sealed class MinTooltipDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            GUIContent content = WithTooltip(label, property);
            MinAttribute min = (MinAttribute)attribute;

            EditorGUI.BeginChangeCheck();
            if (property.propertyType == SerializedPropertyType.Float)
            {
                float value = EditorGUI.FloatField(position, content, property.floatValue);
                if (EditorGUI.EndChangeCheck())
                    property.floatValue = Mathf.Max(min.min, value);
                return;
            }

            if (property.propertyType == SerializedPropertyType.Integer)
            {
                int value = EditorGUI.IntField(position, content, property.intValue);
                if (EditorGUI.EndChangeCheck())
                    property.intValue = Mathf.Max(Mathf.RoundToInt(min.min), value);
                return;
            }

            EditorGUI.EndChangeCheck();
            EditorGUI.LabelField(position, content.text, "Use Min with float or int.");
        }

        private static GUIContent WithTooltip(GUIContent label, SerializedProperty property)
        {
            if (label != null && !string.IsNullOrEmpty(label.tooltip))
                return label;

            string tooltip = property != null ? property.tooltip : string.Empty;
            return new GUIContent(label != null ? label.text : property.displayName, tooltip);
        }
    }

    [CustomPropertyDrawer(typeof(RangeAttribute))]
    public sealed class RangeTooltipDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            GUIContent content = WithTooltip(label, property);
            RangeAttribute range = (RangeAttribute)attribute;

            if (property.propertyType == SerializedPropertyType.Float)
                EditorGUI.Slider(position, property, range.min, range.max, content);
            else if (property.propertyType == SerializedPropertyType.Integer)
                EditorGUI.IntSlider(position, property, Mathf.RoundToInt(range.min), Mathf.RoundToInt(range.max), content);
            else
                EditorGUI.LabelField(position, content.text, "Use Range with float or int.");
        }

        private static GUIContent WithTooltip(GUIContent label, SerializedProperty property)
        {
            if (label != null && !string.IsNullOrEmpty(label.tooltip))
                return label;

            string tooltip = property != null ? property.tooltip : string.Empty;
            return new GUIContent(label != null ? label.text : property.displayName, tooltip);
        }
    }
}
