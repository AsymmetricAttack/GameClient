/****************************************************************
 * File: SpacesYUVFrame.cs
 * Copyright (c) 2023 Qualcomm Technologies, Inc. and/or its subsidiaries. All rights reserved.
 *
 * Confidential and Proprietary - Qualcomm Technologies, Inc.
 ****************************************************************/

using System;
using Unity.Collections;
using UnityEngine;

namespace Qualcomm.Snapdragon.Spaces
{
    internal class SpacesYUVFrame
    {
        public ulong Handle => _handle;
        public long Timestamp => _timestamp;
        public Vector2Int Dimensions => _dimensions;
        public XrCameraFrameFormatQCOM Format => _format;
        public NativeArray<byte> YPlaneData
        {
            get
            {
                if (!_isValid)
                {
                    throw new MemberAccessException("Cannot access already disposed frame data.");
                }
                return _yPlaneData;
            }
        }
        public NativeArray<byte> UVPlaneData
        {
            get
            {
                if (!_isValid)
                {
                    throw new MemberAccessException("Cannot access already disposed frame data.");
                }
                return _uvPlaneData;
            }
        }
        public bool IsValid => _isValid;

        private ulong _handle;
        private long _timestamp;
        private XrCameraFrameFormatQCOM _format;
        private Vector2Int _dimensions;
        private NativeArray<byte> _yPlaneData;
        private NativeArray<byte> _uvPlaneData;
        private bool _isValid;

        public SpacesYUVFrame(ulong handle, long timestamp, XrCameraFrameFormatQCOM format, Vector2Int dimensions, ref NativeArray<byte> yPlaneData, ref NativeArray<byte> uvPlaneData)
        {
            _handle = handle;
            _timestamp = timestamp;
            _dimensions = dimensions;
            _format = format;
            _yPlaneData = yPlaneData;
            _uvPlaneData = uvPlaneData;
            _isValid = true;
        }

        public void Dispose()
        {
            _yPlaneData.Dispose();
            _uvPlaneData.Dispose();
            _isValid = false;
        }
    }
}
