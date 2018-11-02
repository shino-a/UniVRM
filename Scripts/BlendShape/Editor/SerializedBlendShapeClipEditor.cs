using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace VRM
{
    public class SerializedBlendShapeEditor
    {
        BlendShapeClip m_targetObject;

        SerializedObject m_serializedObject;

        #region  Properties
        SerializedProperty m_BlendShapeNameProp;
        SerializedProperty m_PresetProp;
        #endregion

        #region BlendShapeBind
        public static int BlendShapeBindingHeight = 60;
        ReorderableList m_ValuesList;
        #endregion

        #region  MaterialValueBind
        const int MaterialValueBindingHeight = 90;
        ReorderableList m_MaterialValuesList;
        #endregion

        #region  Editor values
        float m_previewSlider = 1.0f;

        bool m_changed;

        int m_mode;
        static string[] MODES = new[]{
            "BlendShape",
            "Material"
        };
        #endregion

        public SerializedBlendShapeEditor(SerializedObject serializedObject,
            PreviewSceneManager previewSceneManager) : this(
                serializedObject, (BlendShapeClip)serializedObject.targetObject, previewSceneManager)
        { }

        public SerializedBlendShapeEditor(BlendShapeClip blendShapeClip,
            PreviewSceneManager previewSceneManager) : this(
                new SerializedObject(blendShapeClip), blendShapeClip, previewSceneManager)
        { }

        public SerializedBlendShapeEditor(SerializedObject serializedObject, BlendShapeClip targetObject,
            PreviewSceneManager previewSceneManager)
        {
            this.m_serializedObject = serializedObject;
            this.m_targetObject = targetObject;

            m_BlendShapeNameProp = serializedObject.FindProperty("BlendShapeName");
            m_PresetProp = serializedObject.FindProperty("Preset");
            var valuesProp = serializedObject.FindProperty("Values");

            m_ValuesList = new ReorderableList(serializedObject, valuesProp);
            m_ValuesList.elementHeight = BlendShapeBindingHeight;
            m_ValuesList.drawElementCallback =
              (rect, index, isActive, isFocused) =>
              {
                  var element = valuesProp.GetArrayElementAtIndex(index);
                  rect.height -= 4;
                  rect.y += 2;
                  if (DrawBlendShapeBinding(rect, element, previewSceneManager))
                  {
                      m_changed = true;
                  }
              };

            var materialValuesProp = serializedObject.FindProperty("MaterialValues");
            m_MaterialValuesList = new ReorderableList(serializedObject, materialValuesProp);
            m_MaterialValuesList.elementHeight = MaterialValueBindingHeight;
            m_MaterialValuesList.drawElementCallback =
              (rect, index, isActive, isFocused) =>
              {
                  var element = materialValuesProp.GetArrayElementAtIndex(index);
                  rect.height -= 4;
                  rect.y += 2;
                  if (DrawMaterialValueBinding(rect, element, previewSceneManager))
                  {
                      m_changed = true;
                  }
              };
        }

        public static bool DrawBlendShapeBinding(Rect position, SerializedProperty property,
            PreviewSceneManager scene)
        {
            bool changed = false;
            if (scene != null)
            {
                var height = 16;

                var y = position.y;
                var rect = new Rect(position.x, y, position.width, height);
                int pathIndex;
                if (StringPopup(rect, property.FindPropertyRelative("RelativePath"), scene.SkinnedMeshRendererPathList, out pathIndex))
                {
                    changed = true;
                }

                y += height;
                rect = new Rect(position.x, y, position.width, height);
                int blendShapeIndex;
                if (IntPopup(rect, property.FindPropertyRelative("Index"), scene.GetBlendShapeNames(pathIndex), out blendShapeIndex))
                {
                    changed = true;
                }

                y += height;
                rect = new Rect(position.x, y, position.width, height);
                if (FloatSlider(rect, property.FindPropertyRelative("Weight"), 100))
                {
                    changed = true;
                }
            }
            return changed;
        }

        public static bool DrawMaterialValueBinding(Rect position, SerializedProperty property,
            PreviewSceneManager scene)
        {
            bool changed = false;
            if (scene != null)
            {
                var height = 16;

                var y = position.y;
                var rect = new Rect(position.x, y, position.width, height);
                int materialIndex;
                if (StringPopup(rect, property.FindPropertyRelative("MaterialName"), scene.MaterialNames, out materialIndex))
                {
                    changed = true;
                }

                if (materialIndex >= 0)
                {
                    var materialItem = scene.GetMaterialItem(scene.MaterialNames[materialIndex]);
                    if (materialItem != null)
                    {
                        y += height;
                        rect = new Rect(position.x, y, position.width, height);

                        // プロパティ名のポップアップ
                        int propIndex;
                        if (StringPopup(rect, property.FindPropertyRelative("ValueName"), materialItem.PropNames, out propIndex))
                        {
                            changed = true;
                        }

                        if (propIndex >= 0)
                        {
                            // 有効なプロパティ名が選択された
                            var propItem = materialItem.PropMap[materialItem.PropNames[propIndex]];
                            {
                                switch (propItem.PropertyType)
                                {
                                    case ShaderUtil.ShaderPropertyType.Color:
                                        {
                                            property.FindPropertyRelative("BaseValue").vector4Value = propItem.DefaultValues;

                                            // max
                                            y += height;
                                            rect = new Rect(position.x, y, position.width, height);
                                            if (ColorProp(rect, property.FindPropertyRelative("TargetValue")))
                                            {
                                                changed = true;
                                            }
                                        }
                                        break;

                                    case ShaderUtil.ShaderPropertyType.TexEnv:
                                        {
                                            property.FindPropertyRelative("BaseValue").vector4Value = propItem.DefaultValues;

                                            // max
                                            y += height;
                                            rect = new Rect(position.x, y, position.width, height);
                                            if (OffsetProp(rect, property.FindPropertyRelative("TargetValue")))
                                            {
                                                changed = true;
                                            }
                                        }
                                        break;
                                }
                            }
                        }
                    }
                }
            }
            return changed;
        }


        public struct DrawResult
        {
            public bool Changed;
            public float Weight;

            public BlendShapeBinding[] BlendShapeBindings;

            public MaterialValueBinding[] MaterialValueBindings;
        }

        public DrawResult Draw()
        {
            m_changed = false;

            //EditorGUILayout.Space();

            var previewSlider = EditorGUILayout.Slider("Preview Weight", m_previewSlider, 0, 1.0f);
            if (previewSlider != m_previewSlider)
            {
                m_previewSlider = previewSlider;
                m_changed = true;
            }

            m_serializedObject.Update();

            EditorGUILayout.Space();
            EditorGUILayout.PropertyField(m_BlendShapeNameProp, true);
            EditorGUILayout.PropertyField(m_PresetProp, true);

            EditorGUILayout.Space();
            //m_mode = EditorGUILayout.Popup("SourceType", m_mode, MODES);
            m_mode = GUILayout.Toolbar(m_mode, MODES);
            switch (m_mode)
            {
                case 0:
                    {
                        //EditorGUILayout.LabelField("BlendShapeBindings", EditorStyles.boldLabel);
                        m_ValuesList.DoLayoutList();
                    }
                    break;

                case 1:
                    {
                        //EditorGUILayout.LabelField("MaterialValueBindings", EditorStyles.boldLabel);
                        m_MaterialValuesList.DoLayoutList();
                    }
                    break;
            }

            if (m_changed)
            {
                m_serializedObject.ApplyModifiedProperties();
            }

            return new DrawResult
            {
                Changed = m_changed,
                Weight = m_previewSlider,
                BlendShapeBindings = m_targetObject.Values,
                MaterialValueBindings = m_targetObject.MaterialValues
            };
        }

        static bool StringPopup(Rect rect, SerializedProperty prop, string[] options, out int newIndex)
        {
            if (options == null)
            {
                newIndex = -1;
                return false;
            }

            var oldIndex = Array.IndexOf(options, prop.stringValue);
            newIndex = EditorGUI.Popup(rect, oldIndex, options);
            if (newIndex != oldIndex && newIndex >= 0 && newIndex < options.Length)
            {
                prop.stringValue = options[newIndex];
                return true;
            }
            else
            {
                return false;
            }
        }

        static bool IntPopup(Rect rect, SerializedProperty prop, string[] options, out int newIndex)
        {
            if (options == null)
            {
                newIndex = -1;
                return false;
            }

            var oldIndex = prop.intValue;
            newIndex = EditorGUI.Popup(rect, oldIndex, options);
            if (newIndex != oldIndex && newIndex >= 0 && newIndex < options.Length)
            {
                prop.intValue = newIndex;
                return true;
            }
            else
            {
                return false;
            }
        }

        static bool FloatSlider(Rect rect, SerializedProperty prop, float maxValue)
        {
            var oldValue = prop.floatValue;
            var newValue = EditorGUI.Slider(rect, prop.floatValue, 0, 100f);
            if (newValue != oldValue)
            {
                prop.floatValue = newValue;
                return true;
            }
            else
            {
                return false;
            }
        }

        static bool ColorProp(Rect rect, SerializedProperty prop)
        {
            var oldValue = (Color)prop.vector4Value;
            var newValue = EditorGUI.ColorField(rect, prop.displayName, oldValue);
            if (newValue != oldValue)
            {
                prop.vector4Value = newValue;
                return true;
            }
            else
            {
                return false;
            }
        }

        static bool OffsetProp(Rect rect, SerializedProperty prop)
        {
            var oldValue = prop.vector4Value;
            var newValue = EditorGUI.Vector4Field(rect, prop.displayName, oldValue);
            if (newValue != oldValue)
            {
                prop.vector4Value = newValue;
                return true;
            }
            else
            {
                return false;
            }
        }
    }
}
