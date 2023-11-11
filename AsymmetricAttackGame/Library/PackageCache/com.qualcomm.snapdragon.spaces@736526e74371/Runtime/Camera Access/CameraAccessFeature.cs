/******************************************************************************
 * File: CameraAccessFeature.cs
 * Copyright (c) 2023 Qualcomm Technologies, Inc. and/or its subsidiaries. All rights reserved.
 *
 * Confidential and Proprietary - Qualcomm Technologies, Inc.
 *
 ******************************************************************************/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.XR.ARSubsystems;
using UnityEngine.XR.OpenXR;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.XR.OpenXR.Features;
#endif
#if UNITY_ANDROID
using UnityEngine.Android;
#endif

namespace Qualcomm.Snapdragon.Spaces
{
#if UNITY_EDITOR
    [OpenXRFeature(
        UiName = FeatureName,
        BuildTargetGroups = new[] { BuildTargetGroup.Android },
        Company = "Qualcomm",
        Desc = "Enables Camera Access feature on Snapdragon Spaces enabled devices",
        DocumentationLink = "",
        OpenxrExtensionStrings = FeatureExtensions,
        Version = "0.17.0",
        Required = false,
        Category = FeatureCategory.Feature,
        FeatureId = FeatureID)]
#endif
    internal sealed partial class CameraAccessFeature : SpacesOpenXRFeature
    {
        public const string FeatureName = "Camera Access (Experimental)";
        public const string FeatureID = "com.qualcomm.snapdragon.spaces.cameraaccess";
        public const string FeatureExtensions = "XR_QCOMX_camera_frame_access";

        [Tooltip("Camera frames will be downsampled by powers of 2 until the specified max resolution is satisfied. Not reflected on sensor intrinsics. Lower values may improve performance on certain devices.")]
        public uint MaxVerticalResolution = 720;

        public uint RuntimeMaxVerticalResolution{
            get => _runtimeMaxVerticalResolution;
            set
            {
                if (value != _runtimeMaxVerticalResolution)
                {
                    _runtimeMaxVerticalResolution = value;
                    _downsamplingFactorDirty = true;
                }
            }
        }
        private static List<XRCameraSubsystemDescriptor> _cameraSubsystemDescriptors = new List<XRCameraSubsystemDescriptor>();
        private BaseRuntimeFeature _baseRuntimeFeature;

        private List<XrCameraInfoQCOM> _cameraInfos = new List<XrCameraInfoQCOM>();
        private XrCameraFrameBufferQCOM _defaultFrameBuffer;
        private XrCameraFrameHardwareBufferQCOM _defaultFrameHardwareBuffer;
        private XrCameraSensorPropertiesQCOM _defaultSensorProperties;
        private uint _runtimeMaxVerticalResolution;
        private uint _downsamplingFactor = 1;
        private bool _downsamplingFactorDirty = true;

        private XrCameraFrameBufferQCOM[] _frameBuffers;
        private XrCameraFrameHardwareBufferQCOM[] _frameHardwareBuffers;

        private bool _frameReleased = true;

        private XrCameraFrameConfigurationQCOM _lastFrameConfig;
        private XrCameraFrameDataQCOM _cachedFrameData;
        private SpacesYUVFrame _cachedYuvFrame;
        private uint _lastDownsamplingFactor;

        private XrCameraSensorPropertiesQCOM[] _sensorProperties;
        private XrPosef _lastFramePose = new XrPosef(XrQuaternionf.identity, XrVector3f.zero);

        internal List<XrCameraInfoQCOM> CameraInfos => _cameraInfos;
        internal uint DownsamplingFactor => _downsamplingFactor;

        internal XrCameraSensorPropertiesQCOM[] SensorProperties => _sensorProperties;
        internal XrCameraFrameDataQCOM CachedFrameData => _cachedFrameData;
        internal SpacesYUVFrame CachedYuvFrame => _cachedYuvFrame;
        internal XrCameraFrameBufferQCOM[] FrameBuffers => _frameBuffers;
        internal Pose LastFramePose => _lastFramePose.ToPose();

        protected override bool IsRequiringBaseRuntimeFeature => true;

        protected override string GetXrLayersToLoad()
        {
            return "XR_APILAYER_QCOM_retina_tracking";
        }

        protected override bool OnInstanceCreate(ulong instanceHandle)
        {
            Application.SetStackTraceLogType(LogType.Log, StackTraceLogType.None);
            base.OnInstanceCreate(instanceHandle);

            _baseRuntimeFeature = OpenXRSettings.Instance.GetFeature<BaseRuntimeFeature>();

            var missingExtensions = GetMissingExtensions(FeatureExtensions);
            if (missingExtensions.Any())
            {
                Debug.Log(FeatureName + " is missing following extension in the runtime: " + String.Join(",", missingExtensions));
                return false;
            }
#if UNITY_ANDROID
            if (!Permission.HasUserAuthorizedPermission(Permission.Camera))
            {
                Debug.LogError(FeatureName + " Feature is missing the camera permissions and can't be created therefore!");
                return false;
            }
#endif
            RuntimeMaxVerticalResolution = MaxVerticalResolution;
            return true;
        }

        protected override void OnSubsystemCreate()
        {
            CreateSubsystem<XRCameraSubsystemDescriptor, XRCameraSubsystem>(_cameraSubsystemDescriptors, CameraSubsystem.ID);
        }

        protected override void OnSubsystemStop()
        {
            StopSubsystem<XRCameraSubsystem>();
        }

        protected override void OnSubsystemDestroy()
        {
            DestroySubsystem<XRCameraSubsystem>();
        }

        protected override void OnHookMethods()
        {
            HookMethod("xrEnumerateCamerasQCOMX", out _xrEnumerateCamerasQCOM);
            HookMethod("xrGetSupportedFrameConfigurationsQCOMX", out _xrGetSupportedFrameConfigurationsQCOM);
            HookMethod("xrCreateCameraHandleQCOMX", out _xrCreateCameraHandleQCOM);
            HookMethod("xrReleaseCameraHandleQCOMX", out _xrReleaseCameraHandleQCOM);
            HookMethod("xrAccessFrameQCOMX", out _xrAccessFrameQCOM);
            HookMethod("xrReleaseFrameQCOMX", out _xrReleaseFrameQCOM);
        }

        public bool TryEnumerateCameras()
        {
            _cameraInfos = new List<XrCameraInfoQCOM>();

            if (_xrEnumerateCamerasQCOM == null)
            {
                Debug.LogError("xrEnumerateCamerasQCOM method not found!");
                return false;
            }

            uint cameraInfoCountOutput = 0;

            var result = _xrEnumerateCamerasQCOM(SessionHandle, 0, ref cameraInfoCountOutput, IntPtr.Zero);
            if (result != XrResult.XR_SUCCESS)
            {
                Debug.LogError("Enumerate device cameras (1) failed: " + Enum.GetName(typeof(XrResult), result));
                return false;
            }

            using ScopeArrayPtr<XrCameraInfoQCOM> cameraInfosPtr = new((int)cameraInfoCountOutput);
            var defaultCameraInfo = new XrCameraInfoQCOM(String.Empty, 0, 0);
            for (int i = 0; i < cameraInfoCountOutput; i++)
            {
                cameraInfosPtr.Copy(defaultCameraInfo, i);
            }

            result = _xrEnumerateCamerasQCOM(SessionHandle, cameraInfoCountOutput, ref cameraInfoCountOutput, cameraInfosPtr.Raw);
            if (result != XrResult.XR_SUCCESS)
            {
                Debug.LogError("Enumerate device cameras (2) failed: " + Enum.GetName(typeof(XrResult), result));
                return false;
            }

            for (int i = 0; i < cameraInfoCountOutput; i++)
            {
                _cameraInfos.Add(cameraInfosPtr.AtIndex(i));
            }

            // Initialise default frame access structures for convenience
            //
            // XR_MAX_CAMERA_RADIAL_DISTORSION_PARAMS_LENGTH_QCOMX == 6
            // XR_MAX_CAMERA_TANGENTIAL_DISTORSION_PARAMS_LENGTH_QCOMX == 2
            //
            // Marshal.SizeOf(XrCameraFramePlaneQCOMX) == 32
            // XR_CAMERA_FRAME_PLANES_SIZE_QCOMX == 4

            var defaultSensorIntrinsics = new XrCameraSensorIntrinsicsQCOM(
                new XrVector2f(Vector2.zero),
                new XrVector2f(Vector2.zero),
                new float[6],
                new float[2],
                0);
            _defaultSensorProperties = new XrCameraSensorPropertiesQCOM(
                defaultSensorIntrinsics,
                new XrPosef(new XrQuaternionf(Quaternion.identity), new XrVector3f(Vector3.zero)),
                new XrOffset2Di(Vector2Int.zero),
                new XrExtent2Di(Vector2Int.zero),
                0,
                0);
            _defaultFrameBuffer = new XrCameraFrameBufferQCOM(
                0,
                IntPtr.Zero,
                new XrOffset2Di(Vector2Int.zero),
                0,
                new byte[32 * 4]);
            _defaultFrameHardwareBuffer = new XrCameraFrameHardwareBufferQCOM(IntPtr.Zero);

            return true;
        }

        public List<XrCameraFrameConfigurationQCOM> TryGetSupportedFrameConfigurations(string cameraSet)
        {
            var defaultReturn = new List<XrCameraFrameConfigurationQCOM>();

            if (_xrGetSupportedFrameConfigurationsQCOM == null)
            {
                Debug.LogError("xrGetSupportedFrameConfigurationsQCOM method not found!");
                return defaultReturn;
            }

            uint frameConfigurationCountOutput = 0;

            var result = _xrGetSupportedFrameConfigurationsQCOM(SessionHandle, cameraSet, 0, ref frameConfigurationCountOutput, IntPtr.Zero);
            if (result != XrResult.XR_SUCCESS)
            {
                Debug.LogError("Failed to get Supported Frame Configurations (1): " + Enum.GetName(typeof(XrResult), result));
                return defaultReturn;
            }

            using ScopeArrayPtr<XrCameraFrameConfigurationQCOM> frameConfigurationsPtr = new((int)frameConfigurationCountOutput);
            var defaultFrameConfig = new XrCameraFrameConfigurationQCOM(0, String.Empty, new XrExtent2Di(0, 0), 0, 0, 0, 0);
            for (int i = 0; i < frameConfigurationCountOutput; i++)
            {
                frameConfigurationsPtr.Copy(defaultFrameConfig, i);
            }

            result = _xrGetSupportedFrameConfigurationsQCOM(SessionHandle, cameraSet, frameConfigurationCountOutput, ref frameConfigurationCountOutput, frameConfigurationsPtr.Raw);
            if (result != XrResult.XR_SUCCESS)
            {
                Debug.LogError("Failed to get Supported Frame Configurations (2): " + Enum.GetName(typeof(XrResult), result));
                return defaultReturn;
            }

            var frameConfigurations = new List<XrCameraFrameConfigurationQCOM>();
            for (int i = 0; i < frameConfigurationCountOutput; i++)
            {
                frameConfigurations.Add(frameConfigurationsPtr.AtIndex(i));
            }

            return frameConfigurations;
        }

        public bool TryCreateCameraHandle(out ulong cameraHandle, string cameraSet)
        {
            cameraHandle = 0;

            if (_xrCreateCameraHandleQCOM == null)
            {
                Debug.LogError("xrCreateCameraHandleQCOM method not found!");
                return false;
            }

            XrCameraActivationInfoQCOM activationInfo = new XrCameraActivationInfoQCOM(cameraSet);

            var result = _xrCreateCameraHandleQCOM(SessionHandle, ref activationInfo, ref cameraHandle);
            if (result != XrResult.XR_SUCCESS)
            {
                Debug.LogError("Failed to create camera handle: " + Enum.GetName(typeof(XrResult), result));
                return false;
            }

            return true;
        }

        public bool TryReleaseCameraHandle(ulong cameraHandle)
        {
            if (_xrReleaseCameraHandleQCOM == null)
            {
                Debug.LogError("xrReleaseCameraHandleQCOM method not found!");
                return false;
            }

            var result = _xrReleaseCameraHandleQCOM(cameraHandle);
            if (result != XrResult.XR_SUCCESS)
            {
                Debug.LogError("Failed to release camera handle: " + Enum.GetName(typeof(XrResult), result));
                return false;
            }

            return true;
        }

        public bool TryAccessFrame(ulong cameraHandle, XrCameraFrameConfigurationQCOM frameConfig, uint sensorCount)
        {
            if (_xrAccessFrameQCOM == null)
            {
                Debug.LogError("xrAccessFrameQCOM method not found!");
                return false;
            }

            // Release any locked frames from the runtime
            if (!_frameReleased && !TryReleaseFrame())
            {
                Debug.LogError("Failed to clear frame buffer before requesting frame.");
                return false;
            }

            if (_downsamplingFactorDirty)
            {
                UpdateDownsamplingFactor(frameConfig);
            }

            // Create XrCameraSensorInfosQCOM structure
            //IntPtr sensorPropertiesPtr = IntPtr.Zero;
            IntPtr sensorInfosPtr = IntPtr.Zero;
            GCHandle pinnedSensorInfos = new GCHandle();

            using ScopeArrayPtr<XrCameraSensorPropertiesQCOM> sensorPropertiesPtr = new((int)sensorCount);
            for (int i = 0; i < sensorCount; i++)
            {
                sensorPropertiesPtr.Copy(_defaultSensorProperties, i);
            }

            XrCameraSensorInfosQCOM sensorInfos = new XrCameraSensorInfosQCOM(_baseRuntimeFeature.SpaceHandle, sensorCount, sensorPropertiesPtr.Raw);
            pinnedSensorInfos = GCHandle.Alloc(sensorInfos, GCHandleType.Pinned);
            sensorInfosPtr = pinnedSensorInfos.AddrOfPinnedObject();

            // Create XrCameraFrameBuffersQCOM structure
            using ScopeArrayPtr<XrCameraFrameBufferQCOM> fbArrayPtr = new((int)frameConfig.FrameBufferCount);
            using ScopeArrayPtr<XrCameraFrameHardwareBufferQCOM> fhwbArrayPtr = new((int)frameConfig.FrameBufferCount);
            bool hardwareBuffersAvailable = frameConfig.FrameHardwareBufferCount == frameConfig.FrameBufferCount;

            // If HardwareBuffers match FrameBuffers, assign a HardwareBuffer to every FrameBuffer based on the default FrameBuffer
            if (hardwareBuffersAvailable)
            {
                // Create HardwareBuffer array
                for (int i = 0; i < (int)frameConfig.FrameHardwareBufferCount; i++)
                {
                    fhwbArrayPtr.Copy(_defaultFrameHardwareBuffer, i);
                }
                // Create FrameBuffer array
                for (int i = 0; i < (int)frameConfig.FrameBufferCount; i++)
                {
                    XrCameraFrameBufferQCOM frameBuffer = new XrCameraFrameBufferQCOM(_defaultFrameBuffer, fhwbArrayPtr.AtIndexRaw(i));
                    fbArrayPtr.Copy(frameBuffer, i);
                }
            }
            // Otherwise, reuse the default FrameBuffer for all FrameBuffers
            else
            {
                for (int i = 0; i < (int)frameConfig.FrameBufferCount; i++)
                {
                    fbArrayPtr.Copy(_defaultFrameBuffer, i);
                }
            }

            XrCameraFrameBuffersQCOM frameBuffers = new XrCameraFrameBuffersQCOM(
                IntPtr.Zero,
                frameConfig.FrameBufferCount,
                fbArrayPtr.Raw
            );

            // Request data from runtime
            XrCameraFrameDataQCOM frameData = new XrCameraFrameDataQCOM(sensorInfosPtr);
            var result = _xrAccessFrameQCOM(cameraHandle, ref frameData, ref frameBuffers);
            if (result != XrResult.XR_SUCCESS)
            {
                Debug.LogError("Failed to access frame: " + Enum.GetName(typeof(XrResult), result));
                pinnedSensorInfos.Free();
                return false;
            }

            _frameReleased = false;

            // Skip received frame if it is the same as the last one
            if (_cachedFrameData.Handle == frameData.Handle)
            {
                pinnedSensorInfos.Free();

                if (!TryReleaseFrame())
                {
                    Debug.LogWarning("Could not release frame after requesting it.");
                }

                return true;
            }

            _cachedFrameData = frameData;

            // Extract sensor data
            _sensorProperties = new XrCameraSensorPropertiesQCOM[sensorCount];
            for (int i = 0; i < sensorCount; i++)
            {
                _sensorProperties[i] = sensorPropertiesPtr.AtIndex(i);
            }
            _lastFramePose = _sensorProperties[0].Extrinsic;

            pinnedSensorInfos.Free();

            // Extract frame data
            _frameBuffers = new XrCameraFrameBufferQCOM[frameConfig.FrameBufferCount];
            for (int i = 0; i < frameConfig.FrameBufferCount; i++)
            {
                _frameBuffers[i] = fbArrayPtr.AtIndex(i);
            }

            if (hardwareBuffersAvailable)
            {
                _frameHardwareBuffers = new XrCameraFrameHardwareBufferQCOM[frameConfig.FrameBufferCount];
                for (int i = 0; i < frameConfig.FrameBufferCount; i++)
                {
                    _frameHardwareBuffers[i] = Marshal.PtrToStructure<XrCameraFrameHardwareBufferQCOM>(_frameBuffers[i].HardwareBuffer);
                }
            }

            if (!TryExtractFrame(frameConfig, _sensorProperties, frameData, _frameBuffers, out _cachedYuvFrame))
            {
                Debug.LogError("Failed to extract RGB frame.");
                if (!TryReleaseFrame())
                {
                    Debug.LogWarning("Could not release frame after requesting it.");
                }
                return false;
            }

            if (!TryReleaseFrame())
            {
                Debug.LogWarning("Could not release frame after requesting it.");
            }

            return true;
        }

        public bool TryReleaseFrame()
        {
            if (_frameReleased)
            {
                Debug.LogWarning("Skipped releasing last frame: already released.");
                return true;
            }

            if (_xrReleaseFrameQCOM == null)
            {
                Debug.LogError("xrReleaseFrameQCOM method not found!");
                return false;
            }

            var result = _xrReleaseFrameQCOM(_cachedFrameData.Handle);
            if (result != XrResult.XR_SUCCESS)
            {
                Debug.LogError("Failed to release frame: " + Enum.GetName(typeof(XrResult), result));
                return false;
            }

            _frameReleased = true;
            return true;
        }

        private bool TryExtractFrame(XrCameraFrameConfigurationQCOM frameConfig, XrCameraSensorPropertiesQCOM[] sensorProperties, XrCameraFrameDataQCOM frameData, XrCameraFrameBufferQCOM[] frameBuffers, out SpacesYUVFrame frame)
        {
            if (_cachedYuvFrame != null && _cachedYuvFrame.Handle == frameData.Handle && _cachedYuvFrame.IsValid)
            {
                Debug.LogWarning($"Frame {(int) frameData.Handle} already allocated by the application.");
                frame = _cachedYuvFrame;
                return true;
            }

            Vector2Int srcDimensions = frameConfig.Dimensions.ToVector2Int();
            Vector2Int dimensions = new Vector2Int((int)(srcDimensions.x / _downsamplingFactor), (int)(srcDimensions.y / _downsamplingFactor));

            // Allocate memory for the Y UV buffers
            NativeArray<byte> yPlaneArray;
            NativeArray<byte> uvPlaneArray;

            // Reuse frame cache if:
            // - Frame configuration did not change
            // - Downsampling factor did not change
            // - Cache is allocated
            if (frameConfig.Equals(_lastFrameConfig)
                && _downsamplingFactor == _lastDownsamplingFactor
                && (_cachedYuvFrame?.IsValid ?? false))
            {
                yPlaneArray = _cachedYuvFrame.YPlaneData;
                uvPlaneArray = _cachedYuvFrame.UVPlaneData;
            }
            else
            {
                Debug.Log("Camera Frame Access: Re-allocating frame cache");
                _lastFrameConfig = frameConfig;
                _lastDownsamplingFactor = _downsamplingFactor;
                ClearFrameCache();

                // Account for odd resolutions when allocating UV buffer by adding 1 before dividing.
                // If we don't do this, we might ignore the last row or column which causes misalignment
                yPlaneArray = new NativeArray<byte>(dimensions.x * dimensions.y, Allocator.Persistent);
                uvPlaneArray = new NativeArray<byte>(((dimensions.x + 1) / 2) * ((dimensions.y + 1) / 2) * 2, Allocator.Persistent);
            }

            // Try-catch block to avoid memory leak of NativeArray<byte> in case of exception
            // Do not use 'using', as we want to access the memory out of scope
            try
            {
                // For each Frame Buffer, extract pixel data & image plane properties
                XrCameraFramePlaneQCOM[][] framePlaneArray = new XrCameraFramePlaneQCOM[frameBuffers.Length][];

                for (int i = 0; i < frameBuffers.Length; i++)
                {
                    // Pixel data
                    NativeArray<byte> pixelData;
                    unsafe
                    {
                        pixelData = NativeArrayUnsafeUtility.ConvertExistingDataToNativeArray<byte>((void*)frameBuffers[i].Buffer, (int)frameBuffers[i].BufferSize, Allocator.Invalid);
                    }

                    // Plane metadata
                    framePlaneArray[i] = frameBuffers[i].PlanesArray;
                    XrCameraFramePlaneQCOM yPlane = new XrCameraFramePlaneQCOM();
                    XrCameraFramePlaneQCOM uvPlane = new XrCameraFramePlaneQCOM();
                    foreach (var plane in framePlaneArray[i])
                    {
                        if (plane.PlaneType == XrCameraFramePlaneTypeQCOM.XR_CAMERA_FRAME_PLANE_TYPE_Y_QCOMX)
                        {
                            yPlane = plane;
                        }
                        else if (plane.PlaneType == XrCameraFramePlaneTypeQCOM.XR_CAMERA_FRAME_PLANE_TYPE_UV_QCOMX)
                        {
                            uvPlane = plane;
                        }
                    }

                    // Take 1st sensor properties - Runtime currently reports wrong values for sensors[1]
                    var sourceSensorDimensions = sensorProperties[0].ImageDimensions.ToVector2Int();
                    var sensorOffset =  sensorProperties[0].ImageOffset.ToVector2Int();
                    var sensorDimensions = new Vector2Int((int)(sourceSensorDimensions.x / _downsamplingFactor), (int)(sourceSensorDimensions.y / _downsamplingFactor));

                    // Y buffer, stitched horizontally
                    for (int row = 0; row < sensorDimensions.y; row++)
                    {
                        for (int col = 0; col < sensorDimensions.x; col++)
                        {
                            var sourceOffset = yPlane.Offset + (sensorOffset.y + (row * _downsamplingFactor)) * yPlane.Stride + sensorOffset.x + (col * _downsamplingFactor);
                            yPlaneArray[(row * dimensions.x) + (sensorDimensions.x * i + col)] = pixelData[(int)sourceOffset];
                        }
                    }

                    // UV buffer, stitched horizontally
                    var stitchingOffset = (sensorDimensions.x / 2) * i;
                    for (int row = 0; row < (sensorDimensions.y + 1) / 2; row++)
                    {
                        for (int col = 0; col < (sensorDimensions.x + 1) / 2; col++)
                        {
                            var sourceOffset = uvPlane.Offset + (sensorOffset.y / 2 + (row * _downsamplingFactor)) * uvPlane.Stride + (sensorOffset.x / 2 + ((2 * col) * _downsamplingFactor));
                            uvPlaneArray[row * dimensions.x + (stitchingOffset + col) * 2] = pixelData[(int)sourceOffset];
                            uvPlaneArray[row * dimensions.x + (stitchingOffset + col) * 2 + 1] = pixelData[(int)sourceOffset + 1];
                        }
                    }
                }
            }
            catch (Exception e)
            {
                // Dispose allocated memory in case of an exception
                Debug.LogError($"Failed to extract frame: {e}");
                yPlaneArray.Dispose();
                uvPlaneArray.Dispose();

                // Reset the last Frame Configuration to force cache renewal
                _lastFrameConfig = default;
                frame = default;
                return false;
            }

            frame = new SpacesYUVFrame(
                frameData.Handle,
                frameData.Timestamp,
                frameData.Format,
                dimensions,
                ref yPlaneArray,
                ref uvPlaneArray);

            return true;
        }

        internal void ClearFrameCache()
        {
            if (_cachedYuvFrame == null)
            {
                Debug.LogWarning("Could not dispose of frame that doesn't exist.");
                return;
            }
            if (!_cachedYuvFrame.IsValid)
            {
                Debug.Log("Skipped disposing last frame: already disposed.");
                return;
            }
            _cachedYuvFrame.Dispose();
        }

        private void UpdateDownsamplingFactor(XrCameraFrameConfigurationQCOM frameConfig)
        {
            _downsamplingFactorDirty = false;

            if (_runtimeMaxVerticalResolution == 0)
            {
                Debug.LogWarning("Max Vertical Resolution must be greater than zero. Check your project's OpenXR > Snapdragon Spaces Feature Group > Camera Access settings.");
                return;
            }

            uint verticalResolution = frameConfig.Dimensions.Height;

            if (verticalResolution <= _runtimeMaxVerticalResolution)
            {
                _downsamplingFactor = 1;
                return;
            }

            _downsamplingFactor = (uint) Math.Ceiling(verticalResolution / (float) _runtimeMaxVerticalResolution);
        }
    }
}
