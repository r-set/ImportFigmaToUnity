using UnityEngine;
using UnityEditor;
using System.Threading.Tasks;
using System.Threading;
using Newtonsoft.Json;
using System.Net.Http;
using System;

namespace Figma
{
    public partial class ImportFigmaToUnity : EditorWindow
    {
        private string _fileKey = "";
        private string _token = "";
        private FigmaFile _figmaFile;
        private const string URL = "https://api.figma.com/v1/files/";
        private const string ImageURL = "https://api.figma.com/v1/images/";
        private CancellationTokenSource _cancellationTokenSource;

        private enum ImageFormat { JPG, PNG }
        private ImageFormat _imageFormat = ImageFormat.PNG;
        private int _imagesToLoad = 0;

        private enum TextType { Text, TextMeshPro }
        private TextType _textType = TextType.Text;

        private bool _isProcessing = false;
        private string _processMessage = "";

        [MenuItem("Tools/ImportFigmaToUnity/ImportAllFrame")]
        public static void ShowWindow()
        {
            GetWindow<ImportFigmaToUnity>().Show();
        }

        private async void OnGUI()
        {
            if (Event.current == null)
                return;

            GUILayout.Label("Figma Importer", EditorStyles.boldLabel);

            EditorGUILayout.Space();

            GUILayout.Label("Access", EditorStyles.boldLabel);
            _fileKey = EditorGUILayout.TextField("File key", _fileKey);
            _token = EditorGUILayout.TextField("Token", _token);

            EditorGUILayout.Space();

            GUILayout.Label("Text Settings", EditorStyles.boldLabel);
            _textType = (TextType)EditorGUILayout.EnumPopup("Text Type", _textType);

            EditorGUILayout.Space();

            GUILayout.Label("Image Settings", EditorStyles.boldLabel);
            _imageFormat = (ImageFormat)EditorGUILayout.EnumPopup("Image Format", _imageFormat);

            EditorGUILayout.Space();
            GUILayout.FlexibleSpace();

            if (!_isProcessing)
            {
                if (GUILayout.Button("Import File", GUILayout.Width(160), GUILayout.Height(40)))
                {
                    await GetFileAsync();
                }
            }
            else
            {
                if (GUILayout.Button("Cancel", GUILayout.Width(160), GUILayout.Height(40)))
                {
                    CancelImport();
                }
                GUILayout.Label("Processing...", EditorStyles.boldLabel);
            }

            EditorGUILayout.Space();

            if (!string.IsNullOrEmpty(_processMessage))
            {
                GUILayout.Label(_processMessage);
            }
        }

        private void CancelImport()
        {
            if (_cancellationTokenSource != null && !_cancellationTokenSource.IsCancellationRequested)
            {
                _cancellationTokenSource.Cancel();
                _processMessage = "Cancelling...";
            }
        }

        private async Task GetFileAsync()
        {
            try
            {
                _isProcessing = true;
                _processMessage = "Fetching file...";
                _cancellationTokenSource = new CancellationTokenSource();

                using (var client = new HttpClient())
                {
                    client.DefaultRequestHeaders.Add("X-Figma-Token", _token);
                    var response = await client.GetStringAsync(URL + _fileKey);

                    _figmaFile = JsonConvert.DeserializeObject<FigmaFile>(response);

                    await CreatePrefabs(_cancellationTokenSource.Token);
                }
            }
            catch (OperationCanceledException)
            {
                Debug.Log("File fetching was cancelled.");
                _processMessage = "Import cancelled.";
            }
            catch (Exception e)
            {
                if (e.Message != null)
                {
                    Debug.LogError($"Error fetching file: {e.Message}");
                    _processMessage = $"Error fetching file: {e.Message}";
                }
                else
                {
                    Debug.LogError("Unknown error fetching file.");
                    _processMessage = "Unknown error fetching file.";
                }
            }
            finally
            {
                _isProcessing = false;
                if (_imagesToLoad == 0 && _processMessage != "Import cancelled.")
                {
                    _processMessage = "All processes completed.";
                }
            }
        }
    }
}
