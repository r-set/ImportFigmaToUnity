using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using System.IO;
using System.Threading.Tasks;
using System.Threading;
using Newtonsoft.Json.Linq;
using System.Net.Http;
using System;
using UnityEngineInternal;

namespace Figma
{
    public partial class ImportFigmaToUnity : EditorWindow
    {
        private string GetImagePath(string layerName, string imageFormat)
        {
            string folderPath = "Assets/Resources/Images/";
            folderPath = Path.Combine(folderPath, _imageFormat == ImageFormat.JPG ? "Jpg" : "Png");
            return Path.Combine(folderPath, $"{layerName}.{imageFormat.ToLower()}");
        }

        private async Task GetImageURL(string imageRef, string layerName, Image imageComponent, string imageFormat, CancellationToken cancellationToken)
        {
            try
            {
                _imagesToLoad++;

                using (var client = new HttpClient())
                {
                    client.DefaultRequestHeaders.Add("X-Figma-Token", _token);
                    var response = await client.GetStringAsync($"{ImageURL}{_fileKey}?ids={imageRef}&format={imageFormat}");

                    var jsonResponse = JObject.Parse(response);
                    var url = jsonResponse["images"]?[imageRef]?.ToString();

                    if (url != null)
                    {
                        await LoadImageAsync(url, layerName, imageComponent, imageFormat, cancellationToken);
                    }
                    else
                    {
                        Debug.LogError($"Failed to get URL for image reference: {imageRef}");
                    }
                }
            }
            catch (OperationCanceledException)
            {
                Debug.Log("Image fetching was cancelled.");
            }
            catch (HttpRequestException ex) when (ex.Message.Contains("429"))
            {
                await Task.Delay(200, cancellationToken);
                await GetImageURL(imageRef, layerName, imageComponent, imageFormat, cancellationToken);
            }
            catch (Exception e)
            {
                Debug.LogError($"Error fetching image URL: {e.Message}");
            }
            finally
            {
                _imagesToLoad--;
                if (_imagesToLoad == 0 && !_isProcessing)
                {
                    _processMessage = "All processes completed.";
                }
            }
        }

        private async Task LoadImageAsync(string url, string layerName, Image imageComponent, string imageFormat, CancellationToken cancellationToken)
        {
            try
            {
                using var client = new HttpClient();
                using var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                response.EnsureSuccessStatusCode();

                using var stream = await response.Content.ReadAsStreamAsync();
                Texture2D texture = new(2, 2);
                texture.LoadImage(await ReadFully(stream));

                var imagePath = GetImagePath(layerName, imageFormat);

                var directoryPath = Path.GetDirectoryName(imagePath);
                if (!Directory.Exists(directoryPath))
                {
                    Directory.CreateDirectory(directoryPath);
                }

                var bytes = imageFormat.ToLower() == "jpg" ? texture.EncodeToJPG() : texture.EncodeToPNG();
                File.WriteAllBytes(imagePath, bytes);

                AssetDatabase.Refresh();

                var relativePath = imagePath.Replace(Application.dataPath, "Assets");

                var importer = AssetImporter.GetAtPath(relativePath) as TextureImporter;
                if (importer != null)
                {
                    importer.textureType = TextureImporterType.Sprite;
                    importer.SaveAndReimport();
                }
                else
                {
                    Debug.LogError($"Failed to get importer for image: {relativePath}");
                }

                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(relativePath);
                if (sprite != null)
                {
                    imageComponent.sprite = sprite;
                }
                else
                {
                    Debug.LogError($"Failed to load image from path: {relativePath}");
                }
            }
            catch (OperationCanceledException)
            {
                Debug.Log("Image loading was cancelled.");
                _processMessage = "Import cancelled.";
            }
            catch (HttpRequestException ex) when (ex.Message.Contains("429"))
            {
                await Task.Delay(200, cancellationToken);
                await LoadImageAsync(url, layerName, imageComponent, imageFormat, cancellationToken);
            }
            catch (Exception e)
            {
                Debug.LogError($"Error loading image: {e.Message}");
            }
            finally
            {
                _imagesToLoad--;
                if (_imagesToLoad == 0 && !_isProcessing)
                {
                    _processMessage = "All processes completed.";
                }
            }
        }

        private async Task<byte[]> ReadFully(Stream input)
        {
            using (MemoryStream ms = new())
            {
                await input.CopyToAsync(ms);
                return ms.ToArray();
            }
        }
    }
}
