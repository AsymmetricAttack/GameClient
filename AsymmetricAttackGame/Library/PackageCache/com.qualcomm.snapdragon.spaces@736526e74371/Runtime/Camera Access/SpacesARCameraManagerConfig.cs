/******************************************************************************
 * File: SpacesARCameraManagerConfig.cs
 * Copyright (c) 2023 Qualcomm Technologies, Inc. and/or its subsidiaries. All rights reserved.
 *
 * Confidential and Proprietary - Qualcomm Technologies, Inc.
 *
 ******************************************************************************/

using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.OpenXR;

namespace Qualcomm.Snapdragon.Spaces
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(ARCameraManager))]
    public class SpacesARCameraManagerConfig : MonoBehaviour
    {
        private CameraAccessFeature _cameraAccess;
        [SerializeField]
        private uint _maxVerticalResolution;

        public uint MaxVerticalResolution
        {
            get => _cameraAccess?.RuntimeMaxVerticalResolution ?? 0;
            set
            {
                if (_cameraAccess != null && _cameraAccess.RuntimeMaxVerticalResolution != value)
                {
                    _cameraAccess.RuntimeMaxVerticalResolution = value;
                }
            }
        }

        public uint DownsamplingStride => _cameraAccess?.DownsamplingFactor ?? 0;

        void Start()
        {
            _cameraAccess = OpenXRSettings.Instance.GetFeature<CameraAccessFeature>();
            if (_cameraAccess == null)
            {
                Debug.LogError("Could not get valid camera access feature");
                return;
            }

            _maxVerticalResolution = _cameraAccess.RuntimeMaxVerticalResolution;
            MaxVerticalResolution = _maxVerticalResolution;
        }
    }
}
