/******************************************************************************
 * File: SpacesOpenXRFeature.cs
 * Copyright (c) 2021 Qualcomm Technologies, Inc. and/or its subsidiaries. All rights reserved.
 *
 * Confidential and Proprietary - Qualcomm Technologies, Inc.
 *
 ******************************************************************************/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using UnityEngine.XR.OpenXR;
using UnityEngine.XR.OpenXR.Features;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Qualcomm.Snapdragon.Spaces
{
    public abstract class SpacesOpenXRFeature : OpenXRFeature
    {
        [UnmanagedFunctionPointerAttribute(CallingConvention.Cdecl)]
        public delegate int GetInstanceProcAddrDelegate(IntPtr xrInstance, [MarshalAs(UnmanagedType.LPStr)] string name, ref IntPtr functionPtr);

        public static GetInstanceProcAddrDelegate GetInstanceProcAddrPtr;
#if UNITY_ANDROID
        private const string InterceptOpenXRLibrary = "libInterceptOpenXR";
#else
        private const string InterceptOpenXRLibrary = "InterceptOpenXR";
#endif

        public ulong SessionHandle { get; private set; }
        public ulong SystemIDHandle { get; private set; }
        public ulong InstanceHandle { get; private set; }
        public ulong SpaceHandle { get; private set; }
        public bool IsSessionRunning { get; private set; }
        public int SessionState { get; private set; }
        protected virtual bool IsRequiringBaseRuntimeFeature => false;

        protected override bool OnInstanceCreate(ulong instanceHandle)
        {
            InstanceHandle = instanceHandle;
            GetInstanceProcAddrPtr = (GetInstanceProcAddrDelegate)Marshal.GetDelegateForFunctionPointer(xrGetInstanceProcAddr, typeof(GetInstanceProcAddrDelegate));
            OnHookMethods();
            return true;
        }

        protected virtual void OnHookMethods()
        {
        }

        protected void HookMethod<TDelegate>(string methodName, out TDelegate delegatePointer) where TDelegate : Delegate
        {
            IntPtr functionPtr = IntPtr.Zero;
            if ((XrResult)GetInstanceProcAddrPtr((IntPtr)InstanceHandle, methodName, ref functionPtr) ==
                XrResult.XR_SUCCESS)
            {
                delegatePointer =
                    (TDelegate)Marshal.GetDelegateForFunctionPointer(functionPtr,
                        typeof(TDelegate));
            }
            else
            {
                delegatePointer = null;
            }
        }

        protected override void OnInstanceDestroy(ulong instanceHandle)
        {
            SystemIDHandle = 0;
            InstanceHandle = 0;
        }

        protected override void OnSystemChange(ulong systemIDHandle)
        {
            SystemIDHandle = systemIDHandle;
        }

        protected override void OnSessionCreate(ulong sessionHandle)
        {
            SessionHandle = sessionHandle;
        }

        protected override void OnSessionBegin(ulong sessionHandle)
        {
            IsSessionRunning = true;
        }

        protected override void OnSessionStateChange(int oldState, int newState)
        {
            SessionState = newState;
        }

        protected override void OnSessionEnd(ulong sessionHandle)
        {
            IsSessionRunning = false;
        }

        protected override void OnSessionDestroy(ulong sessionHandle)
        {
            SessionHandle = 0;
        }

        protected override void OnAppSpaceChange(ulong spaceHandle)
        {
            SpaceHandle = spaceHandle;
        }

        protected IEnumerable<string> GetMissingExtensions(string extensions)
        {
            return extensions.Split(null)
                .Where(extension => !OpenXRRuntime.IsExtensionEnabled(extension));
        }

        protected virtual string GetXrLayersToLoad()
        {
            return "";
        }

        [DllImport(InterceptOpenXRLibrary, EntryPoint = "RequestLayers")]
        protected static extern uint RequestLayers([MarshalAs(UnmanagedType.LPStr)] string requestedLayers);

        protected override IntPtr HookGetInstanceProcAddr(IntPtr func)
        {
            var result = base.HookGetInstanceProcAddr(func);
            RequestLayers(GetXrLayersToLoad());
            return result;
        }

#if UNITY_EDITOR
        protected override void GetValidationChecks(List<ValidationRule> rules, BuildTargetGroup targetGroup)
        {
            if (!IsRequiringBaseRuntimeFeature)
            {
                return;
            }

            rules.Add(new ValidationRule(this)
            {
                message = "The \"Base Runtime\" feature has to be enabled for this feature.",
                checkPredicate = () =>
                {
                    var settings = OpenXRSettings.GetSettingsForBuildTargetGroup(targetGroup);
                    if (!settings)
                    {
                        return false;
                    }

                    var feature = settings.GetFeature<BaseRuntimeFeature>();
                    if (!feature)
                    {
                        return false;
                    }

                    return feature.enabled;
                },
                fixIt = () =>
                {
                    var settings = OpenXRSettings.GetSettingsForBuildTargetGroup(targetGroup);
                    if (!settings)
                    {
                        return;
                    }

                    var feature = settings.GetFeature<BaseRuntimeFeature>();
                    if (!feature)
                    {
                        return;
                    }

                    feature.enabled = true;
                },
                error = true
            });
        }
#endif
    }
}
