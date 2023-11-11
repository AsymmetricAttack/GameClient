/******************************************************************************
 * File: XrSpaceLocation.cs
 * Copyright (c) 2021 Qualcomm Technologies, Inc. and/or its subsidiaries. All rights reserved.
 *
 * Confidential and Proprietary - Qualcomm Technologies, Inc.
 *
 ******************************************************************************/

using System;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.XR.ARSubsystems;

namespace Qualcomm.Snapdragon.Spaces
{
    [StructLayout(LayoutKind.Sequential)]
    internal struct XrSpaceLocation
    {
        private XrStructureType _type;
        private IntPtr _next;
        private ulong _locationFlags;
        private XrPosef _pose;

        public void InitStructureType()
        {
            _type = XrStructureType.XR_TYPE_SPACE_LOCATION;
        }

        public Pose GetPose()
        {
            return _pose.ToPose();
        }

        public TrackingState GetTrackingState()
        {
            if (_locationFlags == (ulong)(XrSpaceLocationFlags.XR_SPACE_LOCATION_ORIENTATION_VALID_BIT ^ XrSpaceLocationFlags.XR_SPACE_LOCATION_POSITION_VALID_BIT))
            {
                return TrackingState.Tracking;
            }

            return _locationFlags == (ulong)XrSpaceLocationFlags.None ? TrackingState.None : TrackingState.Limited;
        }
    }
}
