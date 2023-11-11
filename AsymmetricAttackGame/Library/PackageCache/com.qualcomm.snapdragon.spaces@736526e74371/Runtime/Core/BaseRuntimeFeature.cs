/******************************************************************************
 * File: BaseRuntimeFeature.cs
 * Copyright (c) 2021-2023 Qualcomm Technologies, Inc. and/or its subsidiaries. All rights reserved.
 *
 * Confidential and Proprietary - Qualcomm Technologies, Inc.
 *
 ******************************************************************************/

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARSubsystems;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.XR.OpenXR.Features;
#endif

namespace Qualcomm.Snapdragon.Spaces
{
#if UNITY_EDITOR
    [OpenXRFeature(UiName = FeatureName,
        BuildTargetGroups = new[]
        {
            BuildTargetGroup.Android,
            BuildTargetGroup.Standalone
        },
        CustomRuntimeLoaderBuildTargets = new[]
        {
            BuildTarget.Android
        },
        Company = "Qualcomm",
        Desc = "Enables base features like session tracking on Snapdragon Spaces enabled devices",
        DocumentationLink = "",
        OpenxrExtensionStrings = FeatureExtensions,
        Version = "0.17.0",
        Required = true,
        Category = FeatureCategory.Feature,
        FeatureId = FeatureID)]
#endif
    public partial class BaseRuntimeFeature : SpacesOpenXRFeature
    {
        public const string FeatureName = "Base Runtime";
        public const string FeatureID = "com.qualcomm.snapdragon.spaces.base";
        public const string FeatureExtensions = "XR_QCOM_component_versioning";

        [Tooltip("Check the installation of the runtime and show a dialog if it isn't installed.")]
        [Obsolete("Checking of the installed runtime through the BaseRuntimeFeature is deprecated. This is now handled through the launcher activity, where the user will be prompted to install Snapdragon Spaces Services.")]
        [SpacesObsoleteVersion("0.16.0")]
        public bool CheckInstalledRuntime = true;

        [Tooltip("Start the application on the viewer device.")]
        public bool LaunchAppOnViewer = true;

        [Tooltip("Prevents the application to go into sleep mode")]
        public bool PreventSleepMode = true;

        [Tooltip("Show Splash screen on host when starting the application")]
        public bool ShowSplashScreenOnHost = false;

        [Tooltip("Show launch message on host when starting the application")]
        public bool ShowLaunchMessageOnHost;

        [Tooltip("Start the included Spaces Controller on the host device.")]
        public bool LaunchControllerOnHost = true;

        [Tooltip("Use a custom controller included in the asset on the host device instead of the default one in the package.")]
        public bool UseCustomController;

        [Tooltip("If this option is set to true, the exported application will be invisible on the device.")]
        public bool ExportHeadless;

        [Tooltip("If this option is set to a value, the defined activity will be started instead of the default Unity one.")]
        public string AlternateStartActivity = "";

        [Tooltip("If this option is set to true, no permission checks will be carried out during launch of the application.")]
        public bool SkipPermissionChecks;

        private static readonly List<XRSessionSubsystemDescriptor> _sessionSubsystemDescriptors = new List<XRSessionSubsystemDescriptor>();

        protected override string GetXrLayersToLoad()
        {
            return "XR_APILAYER_retina_tracking";
        }

        protected override bool OnInstanceCreate(ulong instanceHandle)
        {
            base.OnInstanceCreate(instanceHandle);
            SetFrameStateCallback(OnFrameStateUpdate);
            SetPassthroughEnabled(true);
            SetPassthroughEnabled(false);
            SetSleepMode();

            if (SystemIDHandle != 0)
            {
                Internal_GetSystemProperties();
            }

            return true;
        }

        protected override void OnSystemChange(ulong systemIDHandle)
        {
            base.OnSystemChange(systemIDHandle);

            if (InstanceHandle != 0)
            {
                Internal_GetSystemProperties();
            }
        }

        private void SetSleepMode()
        {
            if (PreventSleepMode)
            {
                Screen.sleepTimeout = SleepTimeout.NeverSleep;
            }
        }

        protected override void OnHookMethods()
        {
            HookMethod("xrGetComponentVersionsQCOM", out _xrGetComponentVersionsQCOM);
            HookMethod("xrGetSystemProperties", out _xrGetSystemProperties);
        }

        protected override void OnSubsystemCreate()
        {
            CreateSubsystem<XRSessionSubsystemDescriptor, XRSessionSubsystem>(_sessionSubsystemDescriptors, SessionSubsystem.ID);
        }

        protected override void OnSubsystemStart()
        {
            StartSubsystem<XRSessionSubsystem>();
        }

        protected override void OnSubsystemStop()
        {
            StopSubsystem<XRSessionSubsystem>();
        }

        protected override void OnSubsystemDestroy()
        {
            DestroySubsystem<XRSessionSubsystem>();
        }
    }
}
