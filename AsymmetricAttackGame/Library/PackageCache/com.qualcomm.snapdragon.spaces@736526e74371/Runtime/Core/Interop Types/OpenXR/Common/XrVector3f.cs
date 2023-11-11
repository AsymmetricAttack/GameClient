/******************************************************************************
 * File: XrVector3f.cs
 * Copyright (c) 2021 Qualcomm Technologies, Inc. and/or its subsidiaries. All rights reserved.
 *
 * Confidential and Proprietary - Qualcomm Technologies, Inc.
 *
 ******************************************************************************/

using System.Runtime.InteropServices;
using UnityEngine;

namespace Qualcomm.Snapdragon.Spaces
{
    [StructLayout(LayoutKind.Sequential)]
    internal struct XrVector3f
    {
        private float _x;
        private float _y;
        private float _z;

        public XrVector3f(Vector3 position)
        {
            _x = position.x;
            _y = position.y;
            _z = -position.z;
        }

        public static XrVector3f zero => new XrVector3f(new Vector3(0, 0, -0));

        public Vector3 ToVector3()
        {
            return new Vector3(_x, _y, -_z);
        }

        public override string ToString()
        {
            return $"[XrVector3f]{{{_x},{_y},{_z}}}";
        }
    }
}
