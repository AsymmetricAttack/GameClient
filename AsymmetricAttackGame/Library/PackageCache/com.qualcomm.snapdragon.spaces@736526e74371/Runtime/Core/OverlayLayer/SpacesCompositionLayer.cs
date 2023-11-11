/******************************************************************************
 * File: SpacesOverlayLayer.cs
 * Copyright (c) 2023 Qualcomm Technologies, Inc. and/or its subsidiaries. All rights reserved.
 *
 * Confidential and Proprietary - Qualcomm Technologies, Inc.
 *
 ******************************************************************************/

using System;
using UnityEngine;
using UnityEngine.XR.OpenXR;

namespace Qualcomm.Snapdragon.Spaces
{
    public class SpacesCompositionLayer : MonoBehaviour
    {
        public Texture LayerTexture;

        [Tooltip("If checked, the layer texture will be copied on update. Turning this on will affect performance.")]
        public bool IsTextureDynamic = false;

        private uint _layerId;

        [Tooltip("If checked, the layer will use this object's Transform component for it's position, orientation, and extents.")]
        public bool UseTransform = false;

        [SerializeField]
        private Vector2 _extents = new Vector2(0.1f, 0.1f);
        public Vector2 Extents
        {
            get => _extents;
            set
            {
                _extents = value;
                if (_baseRuntimeFeature != null && _overlayCreated)
                {
                    _baseRuntimeFeature.SetExtentsForQuadLayer(_layerId, _extents);
                }
            }
        }

        [SerializeField]
        private Quaternion _orientation = Quaternion.identity;
        public Quaternion Orientation
        {
            get => _orientation;
            set
            {
                _orientation = value;
                if (_baseRuntimeFeature != null && _overlayCreated)
                {
                    _baseRuntimeFeature.SetOrientationForQuadLayer(_layerId, _orientation);
                }
            }
        }

        [SerializeField]
        private Vector3 _position = new Vector3(0.0f, 0.0f, -1.0f);
        public Vector3 Position
        {
            get => _position;
            set
            {
                _position = value;
                if (_baseRuntimeFeature != null && _overlayCreated)
                {
                    _baseRuntimeFeature.SetPositionForQuadLayer(_layerId, _position);
                }
            }
        }

        [SerializeField]
        private uint _sortingOrder = 100;
        public uint SortingOrder
        {
            get => _sortingOrder;
            set
            {
                _sortingOrder = value;
                if (_baseRuntimeFeature != null && _overlayCreated)
                {
                    _baseRuntimeFeature.SetSortingOrderForQuadLayer(_layerId, _sortingOrder);
                }
            }
        }

        private BaseRuntimeFeature _baseRuntimeFeature = null;
        private bool _overlayCreated = false;

        private void Start()
        {
            _baseRuntimeFeature = OpenXRSettings.Instance.GetFeature<BaseRuntimeFeature>();
            if (_baseRuntimeFeature == null)
            {
                Debug.LogWarning("BaseRuntimeFeature is unavailable!");
                return;
            }

            _layerId = _baseRuntimeFeature.CreateCompositionLayer(LayerTexture.width, LayerTexture.height, SortingOrder);
            _overlayCreated = true;

            if (!UseTransform)
            {
                _baseRuntimeFeature.SetPositionForQuadLayer(_layerId, Position);
                _baseRuntimeFeature.SetOrientationForQuadLayer(_layerId, Orientation);
                _baseRuntimeFeature.SetExtentsForQuadLayer(_layerId, Extents);
            }

            UpdateSwapchainImage();
        }

        private void Update()
        {
            if (_baseRuntimeFeature != null && IsTextureDynamic)
            {
                UpdateSwapchainImage();
            }

            if (_baseRuntimeFeature != null && UseTransform)
            {
                Vector3 position = transform.position;
                position.z = -position.z;
                _baseRuntimeFeature.SetPositionForQuadLayer(_layerId, position);
                _baseRuntimeFeature.SetOrientationForQuadLayer(_layerId, transform.rotation);
                _baseRuntimeFeature.SetExtentsForQuadLayer(_layerId, transform.localScale);
            }
        }

        private void OnDestroy()
        {
            if (_baseRuntimeFeature && _overlayCreated)
            {
                _baseRuntimeFeature.DestroyCompositionLayer(_layerId);
            }
        }

        private void OnEnable()
        {
            if (_baseRuntimeFeature != null && _overlayCreated)
            {
                _baseRuntimeFeature.SetQuadLayerVisible(_layerId, true);
            }
        }

        private void OnDisable()
        {
            if (_baseRuntimeFeature != null && _overlayCreated)
            {
                _baseRuntimeFeature.SetQuadLayerVisible(_layerId, false);
            }
        }

        private void UpdateSwapchainImage()
        {
            // graphicsUVStartsAtTop is false for gles3, true for vulkan
            if (!SystemInfo.graphicsUVStartsAtTop)
            {
                IntPtr swapchainImagePtr = _baseRuntimeFeature.AcquireSwapchainImageForLayer(_layerId);
                Texture2D swapchainImage = Texture2D.CreateExternalTexture(LayerTexture.width, LayerTexture.height, TextureFormat.ARGB32, false, true, swapchainImagePtr);
                Graphics.CopyTexture(LayerTexture, swapchainImage);
                _baseRuntimeFeature.ReleaseSwapchainImageForLayer(_layerId);
            }
            // Dont update swapchain image if this call occurs between calls to XrBeginFrame and XrEndFrame because it causes crashes (esp. around scene transitions)
            else if (!_baseRuntimeFeature.IsXrFrameInProgress())
            {
                IntPtr swapchainImagePtr = _baseRuntimeFeature.AcquireSwapchainImageForLayer(_layerId);
                Texture2D swapchainImage = Texture2D.CreateExternalTexture(LayerTexture.width, LayerTexture.height, TextureFormat.ARGB32, false, true, swapchainImagePtr);

                var tempRenderTex = RenderTexture.GetTemporary(LayerTexture.width, LayerTexture.height, 0, RenderTextureFormat.ARGB32,  RenderTextureReadWrite.sRGB);
                Graphics.Blit(LayerTexture, tempRenderTex, new Vector2(1.0f, -1.0f), new Vector2(0.0f, 1.0f));
                Graphics.CopyTexture(tempRenderTex, swapchainImage);

                _baseRuntimeFeature.ReleaseSwapchainImageForLayer(_layerId);

                RenderTexture.ReleaseTemporary(tempRenderTex);
            }
        }
    }
}
