/******************************************************************************
 * File: SpacesOpenXRFeatureSet.cs
 * Copyright (c) 2021 Qualcomm Technologies, Inc. and/or its subsidiaries. All rights reserved.
 *
 * Confidential and Proprietary - Qualcomm Technologies, Inc.
 *
 ******************************************************************************/

using UnityEditor;
using UnityEditor.XR.OpenXR.Features;

namespace Qualcomm.Snapdragon.Spaces.Editor
{
    [OpenXRFeatureSet(FeatureIds = new[]
        {
            BaseRuntimeFeature.FeatureID,
            "com.qualcomm.snapdragon.spaces.spatialanchors",
            "com.qualcomm.snapdragon.spaces.planedetection",
            "com.qualcomm.snapdragon.spaces.imagetracking",
            "com.qualcomm.snapdragon.spaces.handtracking",
            "com.qualcomm.snapdragon.spaces.handtracking.deprecated",
            "com.qualcomm.snapdragon.spaces.raycasting",
            "com.qualcomm.snapdragon.spaces.cameraaccess",
            "com.qualcomm.snapdragon.spaces.sceneunderstanding",
            "com.qualcomm.snapdragon.spaces.fusion"
        },
        DefaultFeatureIds = new[]
        {
            BaseRuntimeFeature.FeatureID,
            "com.qualcomm.snapdragon.spaces.spatialanchors",
            "com.qualcomm.snapdragon.spaces.planedetection",
            "com.qualcomm.snapdragon.spaces.imagetracking",
            "com.qualcomm.snapdragon.spaces.handtracking",
            "com.qualcomm.snapdragon.spaces.handtracking.deprecated",
            "com.qualcomm.snapdragon.spaces.raycasting",
            "com.qualcomm.snapdragon.spaces.cameraaccess",
            "com.qualcomm.snapdragon.spaces.sceneunderstanding",
            "com.qualcomm.snapdragon.spaces.fusion"
        },
        UiName = "Snapdragon Spaces",
        Description = "Feature set with all of Snapdragon Spaces' glorious capabilities.",
        FeatureSetId = "com.qualcomm.snapdragon.spaces",
        SupportedBuildTargets = new[]
        {
            BuildTargetGroup.Android
        })]
    internal class SpacesOpenXRFeatureSet
    {
    }
}
