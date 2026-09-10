using System;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace CasualKit.Utility
{
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class HideIfAttribute : PropertyAttribute
    {
        public string ConditionField { get; }
        public CompareOperation CompareOp { get; }
        public object CompareValue { get; }

        public HideIfAttribute(string conditionField)
        {
            ConditionField = conditionField;
            CompareOp = CompareOperation.Equals;
            CompareValue = true;
        }

        public HideIfAttribute(string conditionField, object compareValue)
        {
            ConditionField = conditionField;
            CompareOp = CompareOperation.Equals;
            CompareValue = compareValue;
        }

        public HideIfAttribute(string conditionField, CompareOperation compareOp, object compareValue)
        {
            ConditionField = conditionField;
            CompareOp = compareOp;
            CompareValue = compareValue;
        }
    }

#if UNITY_EDITOR
    [CustomPropertyDrawer(typeof(HideIfAttribute))]
    public sealed class HideIfAttributeDrawer : PropertyDrawer
    {
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            var hideIf = (HideIfAttribute)attribute;
            return CompareOperationExtensions.GetHeight(
                property, label, hideIf.ConditionField, hideIf.CompareOp, hideIf.CompareValue, invert: true);
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var hideIf = (HideIfAttribute)attribute;
            CompareOperationExtensions.Draw(
                position, property, label, hideIf.ConditionField, hideIf.CompareOp, hideIf.CompareValue, invert: true);
        }
    }
#endif
}
