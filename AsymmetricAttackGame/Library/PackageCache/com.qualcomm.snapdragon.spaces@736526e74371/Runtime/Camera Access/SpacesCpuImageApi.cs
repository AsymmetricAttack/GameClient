/******************************************************************************
 * File: SpacesCpuImageApi.cs
 * Copyright (c) 2023 Qualcomm Technologies, Inc. and/or its subsidiaries. All rights reserved.
 *
 * Confidential and Proprietary - Qualcomm Technologies, Inc.
 *
 ******************************************************************************/

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Qualcomm.Snapdragon.Spaces;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.XR.ARSubsystems;
using UnityEngine.XR.OpenXR;

public class SpacesCpuImageApi : XRCpuImage.Api
{
    private bool _deviceIsA3;
    private List<XRCpuImage.Format> _supportedInputFormats = new List<XRCpuImage.Format> { XRCpuImage.Format.AndroidYuv420_888 };

    private List<TextureFormat> _supportedOutputFormats = new List<TextureFormat> { TextureFormat.RGB24, TextureFormat.RGBA32, TextureFormat.BGRA32 };

    private CameraAccessFeature _underlyingFeature = OpenXRSettings.Instance.GetFeature<CameraAccessFeature>();
    public static SpacesCpuImageApi instance { get; private set; }

    public static SpacesCpuImageApi CreateInstance()
    {
        instance ??= new SpacesCpuImageApi();
        instance._deviceIsA3 = SystemInfo.deviceModel.ToLower().Contains("motorola edge");
        return instance;
    }

    public override bool NativeHandleValid(int nativeHandle)
    {
        return nativeHandle == (int)_underlyingFeature.CachedFrameData.Handle;
    }

    public override bool FormatSupported(XRCpuImage image, TextureFormat format)
    {
        if (!_supportedInputFormats.Contains(image.format))
        {
            return false;
        }

        if (!_supportedOutputFormats.Contains(format))
        {
            return false;
        }

        return true;
    }

    public override bool TryGetPlane(int nativeHandle, int planeIndex, out XRCpuImage.Plane.Cinfo planeCinfo)
    {
        planeCinfo = new XRCpuImage.Plane.Cinfo();

        if (!NativeHandleValid(nativeHandle))
        {
            Debug.LogWarning("Native handle [" + nativeHandle + "] is not valid. The frame might have expired.");
            return false;
        }

        SpacesYUVFrame frame = _underlyingFeature.CachedYuvFrame;
        unsafe
        {
            switch (planeIndex)
            {
                // Y Plane
                case 0:
                    planeCinfo = new XRCpuImage.Plane.Cinfo((IntPtr)frame.YPlaneData.GetUnsafePtr(), frame.YPlaneData.Length, frame.Dimensions.x, 1);
                    break;
                // UV Plane
                case 1:
                    planeCinfo = new XRCpuImage.Plane.Cinfo((IntPtr)frame.UVPlaneData.GetUnsafePtr(), frame.UVPlaneData.Length, frame.Dimensions.x, 1);
                    break;
            }
        }

        return true;
    }

    public override bool TryGetConvertedDataSize(int nativeHandle, Vector2Int dimensions, TextureFormat format, out int size)
    {
        size = 0;

        if (!NativeHandleValid(nativeHandle) || dimensions.x < 0 || dimensions.y < 0)
        {
            return false;
        }

        if (!_supportedOutputFormats.Contains(format))
        {
            return false;
        }

        switch (format)
        {
            case TextureFormat.RGB24:
                size = dimensions.x * dimensions.y * 3;
                break;
            case TextureFormat.RGBA32:
            case TextureFormat.BGRA32:
                size = dimensions.x * dimensions.y * 4;
                break;
        }

        return true;
    }

    public override bool TryConvert(int nativeHandle, XRCpuImage.ConversionParams conversionParams, IntPtr destinationBuffer, int bufferLength)
    {
        if (!NativeHandleValid(nativeHandle) || !_supportedOutputFormats.Contains(conversionParams.outputFormat))
        {
            return false;
        }

        // Conversion parameters
        var inputRect = conversionParams.inputRect;
        var outputDimensions = conversionParams.outputDimensions;
        var mirrorX = (conversionParams.transformation & XRCpuImage.Transformation.MirrorX) != 0;
        var mirrorY = (conversionParams.transformation & XRCpuImage.Transformation.MirrorY) != 0;

        SpacesYUVFrame frame = _underlyingFeature.CachedYuvFrame;

        // Example of image buffer layout for a 2x4 image with YUV420 format variants:
        // YUV420_NV12 --> YYYYYYYY,UVUV
        // YUV420_NV21 --> YYYYYYYY,VUVU
        // A3+Rogue device wrongly inverts NV12 and NV21 formats, so we need to flip swapUV on the device
        bool swapUV = frame.Format == XrCameraFrameFormatQCOM.XR_CAMERA_FRAME_FORMAT_YUV420_NV21_QCOMX ^ _deviceIsA3;

        byte[] framePixels = new byte[bufferLength];

        for (int row = 0; row < outputDimensions.y; row++)
        {
            for (int col = 0; col < outputDimensions.x; col++)
            {
                // Nearest neighbour mapping from the output rectangle (target buffer) to the input rectangle (source image)
                int sourceRow = (int) ((inputRect.yMin + inputRect.height * (row / (float) outputDimensions.y)));
                int sourceCol = (int) ((inputRect.xMin + inputRect.width * (col / (float) outputDimensions.x)));

                var y = frame.YPlaneData[sourceRow * frame.Dimensions.x + sourceCol];
                var uvOffset = (sourceRow / 2) * frame.Dimensions.x + (sourceCol / 2) * 2;
                sbyte u = (sbyte) (frame.UVPlaneData[uvOffset] - 128);
                sbyte v = (sbyte) (frame.UVPlaneData[uvOffset + 1] - 128);

                if (swapUV)
                {
                    (u, v) = (v, u);
                }

                // YUV NV21 to RGB conversion
                // https://en.wikipedia.org/wiki/YUV#Y%E2%80%B2UV420sp_(NV21)_to_RGB_conversion_(Android)

                var r = y + (1.370705f * v);
                var g = y - (0.698001f * v) - (0.337633f * u);
                var b = y + (1.732446f * u);

                r = r > 255 ? 255 : r < 0 ? 0 : r;
                g = g > 255 ? 255 : g < 0 ? 0 : g;
                b = b > 255 ? 255 : b < 0 ? 0 : b;

                // Mirror output pixel across X axis (mirror rows) and Y axis (mirror columns)
                int outputRow = mirrorX ? row : outputDimensions.y - row - 1;
                int outputCol = mirrorY ? outputDimensions.x - col - 1 : col;
                int pixelIndex = (outputRow * outputDimensions.x) + outputCol;

                switch (conversionParams.outputFormat)
                {
                    case TextureFormat.RGB24:
                        framePixels[3 * pixelIndex] = (byte)r;
                        framePixels[(3 * pixelIndex) + 1] = (byte)g;
                        framePixels[(3 * pixelIndex) + 2] = (byte)b;
                        break;
                    case TextureFormat.RGBA32:
                        framePixels[4 * pixelIndex] = (byte)r;
                        framePixels[(4 * pixelIndex) + 1] = (byte)g;
                        framePixels[(4 * pixelIndex) + 2] = (byte)b;
                        framePixels[(4 * pixelIndex) + 3] = 255;
                        break;
                    case TextureFormat.BGRA32:
                        framePixels[4 * pixelIndex] = (byte)b;
                        framePixels[(4 * pixelIndex) + 1] = (byte)g;
                        framePixels[(4 * pixelIndex) + 2] = (byte)r;
                        framePixels[(4 * pixelIndex) + 3] = 255;
                        break;
                }
            }
        }

        Marshal.Copy(framePixels, 0, destinationBuffer, bufferLength);
        return true;
    }

    public override void DisposeImage(int nativeHandle)
    {
        // NOTE(CH): No need to dispose images. The underlying feature takes care of
        // releasing frames from the runtime after requesting and of managing the
        // frame cache's native memory. We override to avoid a NotImplementedException.
    }
}
