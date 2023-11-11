/******************************************************************************
 * File: BaseRuntimeFeature.InterceptOverlayLayer.cs
 * Copyright (c) 2022 Qualcomm Technologies, Inc. and/or its subsidiaries. All rights reserved.
 *
 * Confidential and Proprietary - Qualcomm Technologies, Inc.
 *
 ******************************************************************************/

using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace Qualcomm.Snapdragon.Spaces
{
    public partial class BaseRuntimeFeature
    {
        [DllImport(InterceptOpenXRLibrary, EntryPoint = "CreateCompositionLayer")]
        private static extern uint Internal_CreateCompositionLayer(ulong instance, ulong session, uint width, uint height, uint sortingOrder);

        [DllImport(InterceptOpenXRLibrary, EntryPoint = "DestroyCompositionLayer")]
        private static extern void Internal_DestroyCompositionLayer(uint layerId);

        [DllImport(InterceptOpenXRLibrary, EntryPoint = "AcquireSwapchainImageForLayer")]
        private static extern IntPtr Internal_AcquireSwapchainImageForLayer(uint layerId);

        [DllImport(InterceptOpenXRLibrary, EntryPoint = "ReleaseSwapchainImageForLayer")]
        private static extern void Internal_ReleaseSwapchainImageForLayer(uint layerId);

        [DllImport(InterceptOpenXRLibrary, EntryPoint = "SetExtentsForQuadLayer")]
        private static extern void Internal_SetExtentsForQuadLayer(uint layerId, float width, float height);

        [DllImport(InterceptOpenXRLibrary, EntryPoint = "SetOrientationForQuadLayer")]
        private static extern void Internal_SetOrientationForQuadLayer(uint layerId, float x, float y, float z, float w);

        [DllImport(InterceptOpenXRLibrary, EntryPoint = "SetPositionForQuadLayer")]
        private static extern void Internal_SetPositionForQuadLayer(uint layerId, float x, float y, float z);

        [DllImport(InterceptOpenXRLibrary, EntryPoint = "SetSortingOrderForQuadLayer")]
        private static extern void Internal_SetSortingOrderForQuadLayer(uint layerId, uint sortingOrder);

        [DllImport(InterceptOpenXRLibrary, EntryPoint = "SetQuadLayerVisible")]
        private static extern void Internal_SetQuadLayerVisible(uint layerId, bool visible);

        [DllImport(InterceptOpenXRLibrary, EntryPoint = "SetMaxCompositionLayerCount")]
        private static extern void Internal_SetMaxCompositionLayerCount(uint maxLayerCount);

        [DllImport(InterceptOpenXRLibrary, EntryPoint = "IsXrFrameInProgress")]
        private static extern bool Internal_IsXrFrameInProgress();

        public uint CreateCompositionLayer(int width, int height, uint sortingOrder = 0)
        {
            if (width > SystemProperties.GetGraphicsProperties().MaxSwapchainImageWidth || height > SystemProperties.GetGraphicsProperties().MaxSwapchainImageHeight)
            {
                Debug.LogWarning($"Trying to create composition layer with dimensions: \"{width}, {height}\". Max Swapchain Image Dimensions are: \"{SystemProperties.GetGraphicsProperties().MaxSwapchainImageWidth}, {SystemProperties.GetGraphicsProperties().MaxSwapchainImageHeight}\"");
                return 0;
            }

            return Internal_CreateCompositionLayer(InstanceHandle, SessionHandle, (uint)width, (uint)height, sortingOrder);
        }

        public void DestroyCompositionLayer(uint layerId)
        {
            Internal_DestroyCompositionLayer(layerId);
        }

        public IntPtr AcquireSwapchainImageForLayer(uint layerId)
        {
            return Internal_AcquireSwapchainImageForLayer(layerId);
        }

        public void ReleaseSwapchainImageForLayer(uint layerId)
        {
            Internal_ReleaseSwapchainImageForLayer(layerId);
        }

        public void SetExtentsForQuadLayer(uint layerId, Vector2 extents)
        {
            Internal_SetExtentsForQuadLayer(layerId, extents.x, extents.y);
        }

        public void SetOrientationForQuadLayer(uint layerId, Quaternion orientation)
        {
            Internal_SetOrientationForQuadLayer(layerId, orientation.x, orientation.y, orientation.z, orientation.w);
        }

        public void SetPositionForQuadLayer(uint layerId, Vector3 position)
        {
            Internal_SetPositionForQuadLayer(layerId, position.x, position.y, position.z);
        }

        public void SetSortingOrderForQuadLayer(uint layerId, uint sortingOrder)
        {
            Internal_SetSortingOrderForQuadLayer(layerId, sortingOrder);
        }

        public void SetQuadLayerVisible(uint layerId, bool visible)
        {
            Internal_SetQuadLayerVisible(layerId, visible);
        }

        public bool IsXrFrameInProgress()
        {
            return Internal_IsXrFrameInProgress();
        }
    }
}
