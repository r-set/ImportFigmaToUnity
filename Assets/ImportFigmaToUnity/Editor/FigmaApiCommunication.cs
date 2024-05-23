using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using System;
using System.Threading.Tasks;
using System.IO;
using System.Threading;
using TMPro;

namespace Figma
{
    public partial class ImportFigmaToUnity : EditorWindow
    {
        private async Task CreatePrefabs(CancellationToken cancellationToken)
        {
            try
            {
                if (_figmaFile == null || _figmaFile.document == null || _figmaFile.document.children == null || _figmaFile.document.children.Length == 0)
                {
                    Debug.LogError("Invalid file data or no children found in the document.");
                    return;
                }

                foreach (var page in _figmaFile.document.children)
                {
                    if (page.type == "CANVAS")
                    {
                        foreach (var layer in page.children)
                        {
                            if (layer.type == "SECTION")
                            {
                                var sectionObj = CreateSection(layer);
                                foreach (var subLayer in layer.children)
                                {
                                    if (subLayer.type == "FRAME")
                                    {
                                        await CreateCanvasForFrame(subLayer, sectionObj.transform);
                                    }
                                    else
                                    {
                                        await CreateLayerRecursive(subLayer, layer, sectionObj.transform);
                                    }
                                }
                            }
                            else if (layer.type == "FRAME")
                            {
                                await CreateCanvasForFrame(layer, null);
                            }
                        }
                    }
                }

                while (_imagesToLoad > 0)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        Debug.Log("Image loading was cancelled.");
                        return;
                    }
                    await Task.Delay(100);
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"Error creating prefabs: {e.Message}");
            }
        }

        private async Task CreateCanvasForFrame(Layer frame, Transform parent)
        {
            if (frame.type != "FRAME")
            {
                return;
            }

            var canvasObj = new GameObject("Canvas_" + frame.name);
            var canvas = canvasObj.AddComponent<Canvas>();
            canvas.pixelPerfect = true;
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var canvasScaler = canvasObj.AddComponent<CanvasScaler>();
            canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasScaler.referenceResolution = new Vector2(frame.absoluteBoundingBox.width, frame.absoluteBoundingBox.height);

            canvasObj.AddComponent<GraphicRaycaster>();

            if (parent != null)
            {
                canvasObj.transform.SetParent(parent, false);
            }

            var panelObj = new GameObject("Panel");
            var panelRect = panelObj.AddComponent<RectTransform>();
            panelRect.SetParent(canvasObj.transform, false);

            panelRect.sizeDelta = new Vector2(frame.absoluteBoundingBox.width, frame.absoluteBoundingBox.height);

            panelObj.AddComponent<CanvasRenderer>();

            if (frame.children?.Length > 0)
            {
                foreach (var child in frame.children)
                {
                    await CreateLayerRecursive(child, frame, panelObj.transform);
                }
            }

            string prefabFolderPath = "Assets/Prefabs/";

            if (!Directory.Exists(prefabFolderPath))
            {
                Directory.CreateDirectory(prefabFolderPath);
            }

            string prefabPath = prefabFolderPath + "Canvas_" + frame.name + ".prefab";

            PrefabUtility.SaveAsPrefabAsset(canvasObj, prefabPath);
        }

        private GameObject CreateSection(Layer section)
        {
            var sectionObj = new GameObject("Section_" + section.name);

            var rect = sectionObj.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(section.absoluteBoundingBox.width, section.absoluteBoundingBox.height);

            return sectionObj;
        }

        private async Task CreateLayerRecursive(Layer layer, Layer parentLayer, Transform parent)
        {
            var obj = new GameObject(layer.name);
            var rect = obj.AddComponent<RectTransform>();

            var rectPos = new Vector2(layer.absoluteBoundingBox.x, -layer.absoluteBoundingBox.y);
            var rectSize = new Vector2(layer.absoluteBoundingBox.width, layer.absoluteBoundingBox.height);

            if (parentLayer.type != "FRAME" && parentLayer.type != "CANVAS")
            {
                rectPos -= new Vector2(parentLayer.absoluteBoundingBox.x, -parentLayer.absoluteBoundingBox.y);
            }
            rect.anchorMin = Vector2.up;
            rect.anchorMax = Vector2.up;
            rect.pivot = Vector2.up;
            rect.anchoredPosition = rectPos;
            rect.sizeDelta = rectSize;

            rect.SetParent(parent, false);

            await SetLayerParameters(layer, obj);

            if (layer.children?.Length > 0)
            {
                foreach (var child in layer.children)
                {
                    await CreateLayerRecursive(child, layer, obj.transform);
                }
            }
        }

        private async Task SetLayerParameters(Layer layer, GameObject obj)
        {
            try
            {
                switch (layer.type)
                {
                    case "GROUP":
                    case "SECTION":
                    case "FRAME":
                    case "SLICE":

                        obj.name = layer.name;
                        break;

                    case "TEXT":
                        if (layer.fills != null && layer.fills.Length > 0 && layer.fills[0].color != null)
                        {
                            if (_textType == TextType.TextMeshPro)
                            {
                                var textMeshPro = obj.AddComponent<TextMeshProUGUI>();
                                textMeshPro.alignment = TextAlignmentOptions.TopLeft;
                                textMeshPro.text = layer.characters;
                                textMeshPro.fontSize = Mathf.RoundToInt(layer.style.fontSize);
                                textMeshPro.color = new Color(layer.fills[0].color.r, layer.fills[0].color.g, layer.fills[0].color.b, layer.fills[0].color.a);
                            }
                            else
                            {
                                var text = obj.AddComponent<Text>();
                                text.alignment = TextAnchor.UpperLeft;
                                text.text = layer.characters;
                                text.fontSize = Mathf.RoundToInt(layer.style.fontSize);
                                text.color = new Color(layer.fills[0].color.r, layer.fills[0].color.g, layer.fills[0].color.b, layer.fills[0].color.a);
                            }
                        }
                        else
                        {
                            Debug.LogError("No fills or colors found for the text layer.");
                        }
                        break;

                    case "VECTOR":
                    case "BOOLEAN_OPERATION":
                    case "RECTANGLE":
                    case "LINE":
                    case "ELLIPSE":
                    case "REGULAR_POLYGON":
                    case "STAR":
                        await GetImageURL(layer.id, layer.name, obj.AddComponent<Image>(), _imageFormat.ToString().ToLower(), _cancellationTokenSource.Token);
                        break;

                    default:
                        Debug.LogWarning($"Layer type '{layer.type}' is not supported.");
                        break;
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"Error setting layer parameters: {e.Message}");
            }
        }
    }
}
