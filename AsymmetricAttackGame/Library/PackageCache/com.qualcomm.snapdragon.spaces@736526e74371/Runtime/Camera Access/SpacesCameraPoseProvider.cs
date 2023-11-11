/******************************************************************************
 * File: SpacesCameraPoseProvider.cs
 * Copyright (c) 2023 Qualcomm Technologies, Inc. and/or its subsidiaries. All rights reserved.
 *
 * Confidential and Proprietary - Qualcomm Technologies, Inc.
 *
 ******************************************************************************/

using UnityEngine;
using UnityEngine.Experimental.XR.Interaction;
using UnityEngine.SpatialTracking;
using UnityEngine.XR.OpenXR;

namespace Qualcomm.Snapdragon.Spaces
{
    public class SpacesCameraPoseProvider : BasePoseProvider
    {
        private CameraAccessFeature _cameraAccess;

        private void Start()
        {
            _cameraAccess = OpenXRSettings.Instance.GetFeature<CameraAccessFeature>();
            if (_cameraAccess == null)
            {
                Debug.LogError("Could not get valid camera access feature");
                return;
            }
        }

        public override PoseDataFlags GetPoseFromProvider(out Pose output)
        {
            output = default;
            if (_cameraAccess == null)
            {
                return PoseDataFlags.NoData;
            }
            output = _cameraAccess.LastFramePose;
            return PoseDataFlags.Position | PoseDataFlags.Rotation;
        }
    }
}
