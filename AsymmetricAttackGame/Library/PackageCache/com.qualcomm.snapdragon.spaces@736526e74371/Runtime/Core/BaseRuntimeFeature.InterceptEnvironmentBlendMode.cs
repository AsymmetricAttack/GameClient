/******************************************************************************
 * File: BaseRuntimeFeature.InterceptEnvironmentBlendMode.cs
 * Copyright (c) 2022 Qualcomm Technologies, Inc. and/or its subsidiaries. All rights reserved.
 *
 * Confidential and Proprietary - Qualcomm Technologies, Inc.
 *
 ******************************************************************************/

using System.Runtime.InteropServices;
using UnityEngine;

namespace Qualcomm.Snapdragon.Spaces
{
    public partial class BaseRuntimeFeature
    {
        public void SetPassthroughEnabled(bool enable)
        {
            if (!IsPassthroughSupported())
            {
                Debug.LogWarning("This device does not support Passthrough.");
                return;
            }

            var originTransform = OriginLocationUtility.GetOriginTransform();
            if (enable && originTransform != null)
            {
                var originCameras = originTransform.GetComponentsInChildren<Camera>(true);
                foreach (var camera in originCameras)
                {
                    if (camera != null && camera.backgroundColor.a > 0.0f)
                    {
                        Debug.LogWarning("Passthrough will be obstructed by the session origin's camera '" + camera.name + "'. Consider changing the background alpha channel from '" + camera.backgroundColor.a.ToString("F1") + "' to '0.0'");
                    }
                }
            }

            SetPassthroughEnabled_Native(enable);
        }

        public bool GetPassthroughEnabled()
        {
            return GetPassthroughEnabled_Native();
        }

        public bool IsPassthroughSupported()
        {
            return IsPassthroughSupported_Native();
        }

        [DllImport(InterceptOpenXRLibrary, EntryPoint = "SetPassthroughEnabled")]
        private static extern void SetPassthroughEnabled_Native(bool enable);

        [DllImport(InterceptOpenXRLibrary, EntryPoint = "GetPassthroughEnabled")]
        private static extern bool GetPassthroughEnabled_Native();

        [DllImport(InterceptOpenXRLibrary, EntryPoint = "IsPassthroughSupported")]
        private static extern bool IsPassthroughSupported_Native();
    }
}
