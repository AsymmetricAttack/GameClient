/******************************************************************************
 * File: CameraSubsystem.cs
 * Copyright (c) 2023 Qualcomm Technologies, Inc. and/or its subsidiaries. All rights reserved.
 *
 * Confidential and Proprietary - Qualcomm Technologies, Inc.
 *
 ******************************************************************************/


using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Unity.Collections;
using UnityEngine;
using UnityEngine.XR.ARSubsystems;
using UnityEngine.XR.OpenXR;


namespace Qualcomm.Snapdragon.Spaces
{
    public class CameraSubsystem : XRCameraSubsystem
    {
        public const string ID = "Spaces-CameraSubsystem";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void RegisterDescriptor()
        {
            XRCameraSubsystemCinfo _cinfo = new XRCameraSubsystemCinfo
            {
                id = ID,
                providerType = typeof(CameraProvider),
                subsystemTypeOverride = typeof(CameraSubsystem),
                supportsAverageBrightness = false,
                supportsAverageColorTemperature = false,
                supportsColorCorrection = false,
                supportsDisplayMatrix = false,
                supportsProjectionMatrix = false,
                supportsTimestamp = false,
                supportsCameraConfigurations = false,
                supportsCameraImage = true,
                supportsAverageIntensityInLumens = false,
                supportsFaceTrackingAmbientIntensityLightEstimation = false,
                supportsFaceTrackingHDRLightEstimation = false,
                supportsWorldTrackingAmbientIntensityLightEstimation = false,
                supportsWorldTrackingHDRLightEstimation = false,
                supportsFocusModes = false,
                supportsCameraGrain = false
            };

            Register(_cinfo);
        }

        private class CameraProvider : Provider
        {
            private bool _autoFocusEnabled = false;
            private bool _autoFocusRequested;
            private ulong _cameraHandle;
            private XrCameraInfoQCOM _cameraInfo;
            private Material _cameraMaterial;

            private XRCpuImage.Api _cpuImageApi;
            private Feature _currentCamera;
            private XRCameraConfiguration? _currentConfiguration;
            private Feature _currentLightEstimation;
            private List<XrCameraFrameConfigurationQCOM> _deviceFrameConfigurations;
            private XrCameraFrameConfigurationQCOM _frameConfiguration;
            private bool _invertCulling;

            private XRCameraFrame _lastFrame;
            private ulong _lastFrameHandle;
            private bool _permissionGranted;
            private Feature _requestedCamera;
            private Feature _requestedLightEstimation;
            private CameraAccessFeature _underlyingFeature;

            public override XRCpuImage.Api cpuImageApi => _cpuImageApi;

            public override Material cameraMaterial => _cameraMaterial;

            public override bool permissionGranted => _permissionGranted;

            public override bool invertCulling => _invertCulling;

            public override Feature currentCamera => _currentCamera;

            public override Feature requestedCamera
            {
                get => _requestedCamera;
                set => _requestedCamera = value;
            }

            public override bool autoFocusEnabled => _autoFocusEnabled;

            public override bool autoFocusRequested
            {
                get => _autoFocusRequested;
                set => _autoFocusRequested = value;
            }

            public override Feature currentLightEstimation => _currentLightEstimation;

            public override Feature requestedLightEstimation
            {
                get => _requestedLightEstimation;
                set => _requestedLightEstimation = value;
            }

            public override XRCameraConfiguration? currentConfiguration
            {
                get => _currentConfiguration;
                set => _currentConfiguration = value;
            }

            public override void Start()
            {
                _underlyingFeature = OpenXRSettings.Instance.GetFeature<CameraAccessFeature>();

                if (!_underlyingFeature || !_underlyingFeature.enabled)
                {
                    Debug.LogError("Spaces CameraAccessFeature is missing or not enabled.");
                    Destroy();
                }

                _cpuImageApi = SpacesCpuImageApi.CreateInstance();
                _permissionGranted = true;
                _invertCulling = false;
                _currentCamera = Feature.WorldFacingCamera;
                _requestedCamera = Feature.WorldFacingCamera;
                _currentLightEstimation = Feature.None;
                _requestedLightEstimation = Feature.None;
                _currentConfiguration = null;
            }

            public override void Stop()
            {
                if (_cameraHandle == 0)
                {
                    return;
                }

                _underlyingFeature.ClearFrameCache();
                if (!_underlyingFeature.TryReleaseFrame())
                {
                    Debug.LogError("Could not release frame with handle [" + _underlyingFeature.CachedFrameData.Handle + "].");
                }

                if (!_underlyingFeature.TryReleaseCameraHandle(_cameraHandle))
                {
                    Debug.LogError("Failed to release camera handle for camera [" + _cameraHandle + "].");
                }
                else
                {
                    _cameraHandle = 0;
                }
            }

            public override void Destroy()
            {
                Stop();
            }

            public override NativeArray<XRCameraConfiguration> GetConfigurations(XRCameraConfiguration defaultCameraConfiguration, Allocator allocator)
            {
                // InitialiseRGBCamera() should be performed asynchronously on Start() in the future
                // In this case, a null configuration should still return a NativeArray<..> of length == 0
                if (_currentConfiguration == null && !InitialiseRGBCamera())
                {
                    Debug.LogError("Failed to initialise target camera.");
                    return new NativeArray<XRCameraConfiguration>(0, allocator);
                }

                NativeArray<XRCameraConfiguration> cameraConfigs = new NativeArray<XRCameraConfiguration>(1, allocator);
                cameraConfigs[0] = (XRCameraConfiguration)_currentConfiguration;
                return cameraConfigs;
            }

            public override bool TryGetIntrinsics(out XRCameraIntrinsics cameraIntrinsics)
            {
                cameraIntrinsics = new XRCameraIntrinsics();

                // InitialiseRGBCamera() should be performed asynchronously on Start() in the future
                if (_cameraHandle == 0 && !InitialiseRGBCamera())
                {
                    Debug.LogError("Failed to initialise target camera.");
                    return false;
                }

                var shouldRequestSensorInfo = _underlyingFeature.SensorProperties.Length == 0;
                if (shouldRequestSensorInfo && !_underlyingFeature.TryAccessFrame(_cameraHandle, _frameConfiguration, _cameraInfo.SensorCount))
                {
                    Debug.LogError("Failed to acquire intrinsics of current camera.");
                    return false;
                }

                XrCameraSensorPropertiesQCOM[] sensorProperties = _underlyingFeature.SensorProperties;
                cameraIntrinsics = new XRCameraIntrinsics(
                    sensorProperties[0].Intrinsics.FocalLength.ToVector2(),
                    sensorProperties[0].Intrinsics.PrincipalPoint.ToVector2(),
                    sensorProperties[0].ImageDimensions.ToVector2Int());
                return true;
            }

            public override bool TryAcquireLatestCpuImage(out XRCpuImage.Cinfo cameraImageCinfo)
            {
                cameraImageCinfo = new XRCpuImage.Cinfo();

                if (_underlyingFeature.CachedYuvFrame.Handle == 0)
                {
                    Debug.LogError("Tried to acquire latest CPU image, but no CPU image is available yet.");
                    return false;
                }
                cameraImageCinfo = new XRCpuImage.Cinfo(
                    (int)_underlyingFeature.CachedYuvFrame.Handle,
                    _underlyingFeature.CachedYuvFrame.Dimensions,
                    (int)_underlyingFeature.FrameBuffers[0].PlaneCount,
                    _underlyingFeature.CachedYuvFrame.Timestamp,
                    XRCpuImage.Format.AndroidYuv420_888);

                return true;
            }

            public override bool TryGetFrame(XRCameraParams cameraParams, out XRCameraFrame cameraFrame)
            {
                cameraFrame = default;

                // InitialiseRGBCamera() should be performed asynchronously on Start() in the future
                if (_cameraHandle == 0 && !InitialiseRGBCamera())
                {
                    Debug.LogError("Failed to initialise target camera.");
                    return false;
                }

                if (!_underlyingFeature.TryAccessFrame(_cameraHandle, _frameConfiguration, _cameraInfo.SensorCount))
                {
                    Debug.LogError("Could not access frame for camera handle [" + _cameraHandle + "].");
                    return false;
                }

                if (_lastFrameHandle == _underlyingFeature.CachedFrameData.Handle)
                {
                    // No new frame received, do not fire ARCameraManager.frameReceived event
                    return false;
                }

                _lastFrameHandle = _underlyingFeature.CachedFrameData.Handle;

                // Note(TD): This is the equivalent of filling the cameraFrame struct through native code.
                // We do this since the XRCameraFrame struct has no constructors or setters.
                CustomXRCameraFrame customCameraFrame = default;
                customCameraFrame.Properties = XRCameraFrameProperties.Timestamp;
                customCameraFrame.TimestampNs = _underlyingFeature.CachedFrameData.Timestamp;

                GCHandle handle = GCHandle.Alloc(customCameraFrame, GCHandleType.Pinned);
                cameraFrame = (XRCameraFrame)Marshal.PtrToStructure(handle.AddrOfPinnedObject(), typeof(XRCameraFrame));

                return true;
            }

            // InitialiseRGBCamera() should be performed asynchronously on Start() in the future
            private bool InitialiseRGBCamera()
            {
                if (!_underlyingFeature.TryEnumerateCameras())
                {
                    Debug.LogError("Failed to enumerate cameras.");
                    Destroy();
                    return false;
                }

                // Find first RGB camera enumerated
                string cameraSet = null;
                foreach (var cameraInfo in _underlyingFeature.CameraInfos)
                {
                    if (cameraInfo.CameraType == XrCameraTypeQCOM.XR_CAMERA_TYPE_RGB_QCOMX)
                    {
                        cameraSet = cameraInfo.CameraSet;
                        _cameraInfo = cameraInfo;
                        break;
                    }
                }

                if (cameraSet == null)
                {
                    Debug.LogError("No RGB camera found.");
                    Destroy();
                    return false;
                }

                // Retrieve target frame configuration for RGB camera set
                _deviceFrameConfigurations = _underlyingFeature.TryGetSupportedFrameConfigurations(cameraSet);
                if (_deviceFrameConfigurations.Count == 0)
                {
                    Debug.LogError("Failed to find supported frame configurations for camera set [" + cameraSet + "].");
                    Destroy();
                    return false;
                }

                // Consider only YUV420_NV12 and YUV420_NV21 formats
                // Retrieve first "full" frame configuration. If none, select first non-"full" frame configuration.
                foreach (var frameConfig in _deviceFrameConfigurations)
                {
                    var formatIsSupported = frameConfig.Format == XrCameraFrameFormatQCOM.XR_CAMERA_FRAME_FORMAT_YUV420_NV12_QCOMX || frameConfig.Format == XrCameraFrameFormatQCOM.XR_CAMERA_FRAME_FORMAT_YUV420_NV21_QCOMX;
                    if (!formatIsSupported)
                    {
                        continue;
                    }

                    if (frameConfig.ResolutionName.Equals("full"))
                    {
                        _frameConfiguration = frameConfig;
                        break;
                    }

                    if (_frameConfiguration.Format == XrCameraFrameFormatQCOM.XR_CAMERA_FRAME_FORMAT_UNKNOWN_QCOMX)
                    {
                        _frameConfiguration = frameConfig;
                    }
                }

                if (_frameConfiguration.Format == XrCameraFrameFormatQCOM.XR_CAMERA_FRAME_FORMAT_UNKNOWN_QCOMX)
                {
                    Debug.LogError("No supported frame configurations found.");
                    Destroy();
                    return false;
                }

                // Create camera handle for access
                if (!_underlyingFeature.TryCreateCameraHandle(out _cameraHandle, cameraSet))
                {
                    Debug.LogError("Failed to create camera handle for camera set [" + cameraSet + "].");
                    Destroy();
                    return false;
                }

                _currentConfiguration = new XRCameraConfiguration(
                    IntPtr.Zero,
                    _frameConfiguration.Dimensions.ToVector2Int(),
                    (int)_frameConfiguration.MaxFPS
                );

                return true;
            }
        }
    }
}
