/******************************************************************************
 * File: BaseRuntimeFeature.InterceptInstanceProcAddr.cs
 * Copyright (c) 2022 Qualcomm Technologies, Inc. and/or its subsidiaries. All rights reserved.
 *
 * Confidential and Proprietary - Qualcomm Technologies, Inc.
 *
 ******************************************************************************/

using System;
using System.Runtime.InteropServices;

namespace Qualcomm.Snapdragon.Spaces
{
    public partial class BaseRuntimeFeature
    {
#if UNITY_ANDROID
        private const string InterceptOpenXRLibrary = "libInterceptOpenXR";
#else
        private const string InterceptOpenXRLibrary = "InterceptOpenXR";
#endif

        [DllImport(InterceptOpenXRLibrary, EntryPoint = "GetInterceptedInstanceProcAddr")]
        private static extern IntPtr GetInterceptedInstanceProcAddr(IntPtr func);

        protected override IntPtr HookGetInstanceProcAddr(IntPtr func)
        {
            return GetInterceptedInstanceProcAddr(func);
        }
    }
}
