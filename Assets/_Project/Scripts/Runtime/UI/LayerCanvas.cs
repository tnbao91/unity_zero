using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Zero.UI
{
    /// <summary>
    /// Helper class to build and manage the four layer canvases (Hud, Popup, Overlay, System).
    /// Each canvas is a separate GameObject with CanvasScaler and GraphicRaycaster configured.
    /// </summary>
    internal static class LayerCanvas
    {
        public static void Build(IDictionary<Core.UiLayer, Transform> output)
        {
            if (output == null) throw new System.ArgumentNullException(nameof(output));

            // Create root container
            var root = new GameObject("[Zero.UI]")
            {
                hideFlags = HideFlags.DontSave
            };
            if (Application.isPlaying)
            {
                Object.DontDestroyOnLoad(root);
            }

            var rootRect = root.AddComponent<RectTransform>();
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = Vector2.zero;
            rootRect.offsetMax = Vector2.zero;

            // Create canvases for each layer
            CreateLayerCanvas(root.transform, Core.UiLayer.Hud, (int)Core.UiLayer.Hud, output);
            CreateLayerCanvas(root.transform, Core.UiLayer.Popup, (int)Core.UiLayer.Popup, output);
            CreateLayerCanvas(root.transform, Core.UiLayer.Overlay, (int)Core.UiLayer.Overlay, output);
            CreateLayerCanvas(root.transform, Core.UiLayer.System, (int)Core.UiLayer.System, output);
        }

        private static void CreateLayerCanvas(
            Transform parent,
            Core.UiLayer layer,
            int sortOrder,
            IDictionary<Core.UiLayer, Transform> output)
        {
            var canvasGo = new GameObject($"[Zero.UI.{layer}]")
            {
                hideFlags = HideFlags.DontSave
            };
            canvasGo.transform.SetParent(parent, false);

            var rectTransform = canvasGo.AddComponent<RectTransform>();
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;

            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortOrder;

            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            scaler.matchWidthOrHeight = 0.5f;

            canvasGo.AddComponent<GraphicRaycaster>();

            output[layer] = canvasGo.transform;
        }
    }
}
