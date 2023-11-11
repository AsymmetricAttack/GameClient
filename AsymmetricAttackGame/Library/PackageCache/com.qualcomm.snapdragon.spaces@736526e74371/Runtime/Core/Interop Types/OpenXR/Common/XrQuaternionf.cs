/******************************************************************************
 * File: XrQuaternionf.cs
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
    internal struct XrQuaternionf
    {
        private float _x;
        private float _y;
        private float _z;
        private float _w;

        public XrQuaternionf(Quaternion quaternion)
        {
            _x = quaternion.x;
            _y = quaternion.y;
            _z = -quaternion.z;
            _w = -quaternion.w;
        }

        public static XrQuaternionf identity => new XrQuaternionf(new Quaternion(0, 0, -0, -1));

        public Quaternion ToQuaternion()
        {
            return new Quaternion(_x, _y, -_z, -_w);
        }

        public override string ToString()
        {
            return $"[XrQuaternionf]{{{_x},{_y},{_z},{_w}}}";
        }
    }
}
