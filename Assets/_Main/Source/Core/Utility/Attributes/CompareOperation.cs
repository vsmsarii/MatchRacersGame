using System;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace CasualKit.Utility
{
    public enum CompareOperation
    {
        Equals,
        NotEquals,
        GreaterThan,
        GreaterOrEquals,
        LessThan,
        LessOrEquals
    }

    public static class CompareOperationExtensions
    {
        public static bool Evaluate(this CompareOperation operation, object left, object right)
        {
            if (TryToDouble(left, out var leftNum) && TryToDouble(right, out var rightNum))
                return FromCmp(leftNum.CompareTo(rightNum), operation);

            if (left is UnityEngine.Object || right is UnityEngine.Object)
            {
                bool equal = (left as UnityEngine.Object) == (right as UnityEngine.Object);
                return operation switch
                {
                    CompareOperation.Equals => equal,
                    CompareOperation.NotEquals => !equal,
                    _ => false
                };
            }

            if (left == null || right == null)
            {
                bool equal = left == null && right == null;
                return operation switch
                {
                    CompareOperation.Equals => equal,
                    CompareOperation.NotEquals => !equal,
                    _ => false
                };
            }

            if (left is string || right is string)
                return FromCmp(string.CompareOrdinal(Convert.ToString(left), Convert.ToString(right)), operation);

            if (left is IComparable comparable && right != null && left.GetType() == right.GetType())
                return FromCmp(comparable.CompareTo(right), operation);

            bool areEqual = Equals(left, right);
            return operation switch
            {
                CompareOperation.Equals => areEqual,
                CompareOperation.NotEquals => !areEqual,
                _ => false
            };
        }

        private static bool FromCmp(int cmp, CompareOperation operation)
        {
            return operation switch
            {
                CompareOperation.Equals => cmp == 0,
                CompareOperation.NotEquals => cmp != 0,
                CompareOperation.GreaterThan => cmp > 0,
                CompareOperation.GreaterOrEquals => cmp >= 0,
                CompareOperation.LessThan => cmp < 0,
                CompareOperation.LessOrEquals => cmp <= 0,
                _ => false
            };
        }

        private static bool TryToDouble(object value, out double result)
        {
            switch (value)
            {
                case byte b: result = b; return true;
                case sbyte sb: result = sb; return true;
                case short s: result = s; return true;
                case ushort us: result = us; return true;
                case int i: result = i; return true;
                case uint ui: result = ui; return true;
                case long l: result = l; return true;
                case ulong ul: result = ul; return true;
                case float f: result = f; return true;
                case double d: result = d; return true;
                case decimal m: result = (double)m; return true;
                case Enum: result = Convert.ToDouble(value); return true;
                default:
                    result = 0;
                    return false;
            }
        }

#if UNITY_EDITOR
        internal static bool ShouldDisplay(
            SerializedProperty property,
            string conditionField,
            CompareOperation operation,
            object compareValue,
            bool invert)
        {
            var condition = FindCondition(property, conditionField);
            if (condition == null)
                return true;

            bool met = operation.Evaluate(GetValue(condition), compareValue);
            return invert ? !met : met;
        }

        internal static float GetHeight(
            SerializedProperty property,
            GUIContent label,
            string conditionField,
            CompareOperation operation,
            object compareValue,
            bool invert)
        {
            if (!ShouldDisplay(property, conditionField, operation, compareValue, invert))
                return -EditorGUIUtility.standardVerticalSpacing;

            return EditorGUI.GetPropertyHeight(property, label, true);
        }

        internal static void Draw(
            Rect position,
            SerializedProperty property,
            GUIContent label,
            string conditionField,
            CompareOperation operation,
            object compareValue,
            bool invert)
        {
            if (!ShouldDisplay(property, conditionField, operation, compareValue, invert))
                return;

            EditorGUI.PropertyField(position, property, label, true);
        }

        private static SerializedProperty FindCondition(SerializedProperty property, string conditionField)
        {
            if (property == null || string.IsNullOrEmpty(conditionField))
                return null;

            var fromRoot = property.serializedObject.FindProperty(conditionField);
            if (fromRoot != null)
                return fromRoot;

            string path = property.propertyPath;
            int lastDot = path.LastIndexOf('.');
            if (lastDot < 0)
                return null;

            return property.serializedObject.FindProperty(path.Substring(0, lastDot + 1) + conditionField);
        }

        private static object GetValue(SerializedProperty property)
        {
            switch (property.propertyType)
            {
                case SerializedPropertyType.Integer: return property.intValue;
                case SerializedPropertyType.Boolean: return property.boolValue;
                case SerializedPropertyType.Float: return property.floatValue;
                case SerializedPropertyType.String: return property.stringValue;
                case SerializedPropertyType.Color: return property.colorValue;
                case SerializedPropertyType.ObjectReference: return property.objectReferenceValue;
                case SerializedPropertyType.Enum: return property.intValue;
                case SerializedPropertyType.Vector2: return property.vector2Value;
                case SerializedPropertyType.Vector3: return property.vector3Value;
                case SerializedPropertyType.Vector4: return property.vector4Value;
                case SerializedPropertyType.Rect: return property.rectValue;
                case SerializedPropertyType.Bounds: return property.boundsValue;
                case SerializedPropertyType.Quaternion: return property.quaternionValue;
                case SerializedPropertyType.Vector2Int: return property.vector2IntValue;
                case SerializedPropertyType.Vector3Int: return property.vector3IntValue;
                case SerializedPropertyType.RectInt: return property.rectIntValue;
                case SerializedPropertyType.BoundsInt: return property.boundsIntValue;
                default: return null;
            }
        }
#endif
    }
}
