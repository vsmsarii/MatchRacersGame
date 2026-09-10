using System;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace CasualKit.Utility
{
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class ShowIfAttribute : PropertyAttribute
    {
        public string ConditionField { get; }
        public CompareOperation CompareOp { get; }
        public object CompareValue { get; }

        public ShowIfAttribute(string conditionField)
        {
            ConditionField = conditionField;
            CompareOp = CompareOperation.Equals;
            CompareValue = true;
        }

        public ShowIfAttribute(string conditionField, object compareValue)
        {
            ConditionField = conditionField;
            CompareOp = CompareOperation.Equals;
            CompareValue = compareValue;
        }

        public ShowIfAttribute(string conditionField, CompareOperation compareOp, object compareValue)
        {
            ConditionField = conditionField;
            CompareOp = compareOp;
            CompareValue = compareValue;
        }
    }

#if UNITY_EDITOR
    [CustomPropertyDrawer(typeof(ShowIfAttribute))]
    public sealed class ShowIfAttributeDrawer : PropertyDrawer
    {
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            var showIf = (ShowIfAttribute)attribute;
            return CompareOperationExtensions.GetHeight(
                property, label, showIf.ConditionField, showIf.CompareOp, showIf.CompareValue, invert: false);
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var showIf = (ShowIfAttribute)attribute;
            CompareOperationExtensions.Draw(
                position, property, label, showIf.ConditionField, showIf.CompareOp, showIf.CompareValue, invert: false);
        }
    }
#endif
}
