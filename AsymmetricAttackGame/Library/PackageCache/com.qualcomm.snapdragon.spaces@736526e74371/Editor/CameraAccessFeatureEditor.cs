/******************************************************************************
 * File: CameraAccessFeatureEditor.cs
 * Copyright (c) 2023 Qualcomm Technologies, Inc. and/or its subsidiaries. All rights reserved.
 *
 * Confidential and Proprietary - Qualcomm Technologies, Inc.
 *
 ******************************************************************************/

using UnityEditor;
using UnityEngine;

namespace Qualcomm.Snapdragon.Spaces.Editor
{
    [CustomEditor(typeof(CameraAccessFeature))]
    public class CameraAccessFeatureEditor : UnityEditor.Editor
    {
        private SerializedProperty _maxVerticalResolution;

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EditorGUILayout.PropertyField(_maxVerticalResolution);
            if (serializedObject.hasModifiedProperties)
            {
                _maxVerticalResolution.intValue = Mathf.Clamp(_maxVerticalResolution.intValue, 1, 2160);
            }
            serializedObject.ApplyModifiedProperties();
        }

        private void OnEnable()
        {
            _maxVerticalResolution = serializedObject.FindProperty("MaxVerticalResolution");
        }
    }
}
