using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

namespace Whisper.Utils
{
    /// <summary>
    /// Utility to download Silero VAD model at runtime.
    /// This is optional - users can also download manually.
    /// </summary>
    public class SileroModelDownloader : MonoBehaviour
    {
        private const string SILERO_VAD_MODEL_URL = "https://github.com/snakers4/silero-vad/raw/master/src/silero_vad/data/silero_vad.onnx";
        private const string MODEL_FILENAME = "silero_vad.onnx";
        
        [Header("Download Configuration")]
        [Tooltip("Download model automatically on start if not found")]
        public bool downloadOnStart = false;
        [Tooltip("Show download progress in console")]
        public bool showProgress = true;
        
        public delegate void DownloadCompleteDelegate(bool success, string modelPath);
        public event DownloadCompleteDelegate OnDownloadComplete;
        
        private void Start()
        {
            if (downloadOnStart && !SileroVadSetup.IsModelAvailable())
            {
                StartDownload();
            }
        }
        
        /// <summary>
        /// Start downloading the Silero VAD model.
        /// </summary>
        public void StartDownload()
        {
            StartCoroutine(DownloadModel());
        }
        
        /// <summary>
        /// Check if model needs to be downloaded.
        /// </summary>
        public bool IsDownloadNeeded()
        {
            return !SileroVadSetup.IsModelAvailable();
        }
        
        private IEnumerator DownloadModel()
        {
            var targetPath = SileroVadSetup.GetRecommendedModelPath();
            var targetDir = Path.GetDirectoryName(targetPath);
            
            // Create StreamingAssets directory if it doesn't exist
            if (!Directory.Exists(targetDir))
            {
                Directory.CreateDirectory(targetDir);
                if (showProgress) Debug.Log($"Created directory: {targetDir}");
            }
            
            if (showProgress) Debug.Log($"Starting download of Silero VAD model from: {SILERO_VAD_MODEL_URL}");
            
            using (var www = UnityWebRequest.Get(SILERO_VAD_MODEL_URL))
            {
                var operation = www.SendWebRequest();
                
                while (!operation.isDone)
                {
                    if (showProgress)
                    {
                        var progress = operation.progress * 100f;
                        Debug.Log($"Download progress: {progress:F1}%");
                    }
                    yield return new WaitForSeconds(0.5f);
                }
                
                if (www.result == UnityWebRequest.Result.Success)
                {
                    try
                    {
                        // Write the downloaded data to file
                        File.WriteAllBytes(targetPath, www.downloadHandler.data);
                        
                        if (showProgress) Debug.Log($"✓ Successfully downloaded Silero VAD model to: {targetPath}");
                        if (showProgress) Debug.Log($"Model size: {www.downloadHandler.data.Length / 1024} KB");
                        
                        OnDownloadComplete?.Invoke(true, targetPath);
                    }
                    catch (System.Exception e)
                    {
                        Debug.LogError($"✗ Failed to save Silero VAD model: {e.Message}");
                        OnDownloadComplete?.Invoke(false, "");
                    }
                }
                else
                {
                    Debug.LogError($"✗ Failed to download Silero VAD model: {www.error}");
                    OnDownloadComplete?.Invoke(false, "");
                }
            }
        }
        
        /// <summary>
        /// Get download status information.
        /// </summary>
        public string GetDownloadStatusInfo()
        {
            var modelPath = SileroVadSetup.GetRecommendedModelPath();
            var isAvailable = SileroVadSetup.IsModelAvailable();
            
            if (isAvailable)
            {
                var fileInfo = new FileInfo(modelPath);
                var sizeKB = fileInfo.Length / 1024;
                return $"Model available: {modelPath} ({sizeKB} KB)";
            }
            else
            {
                return $"Model not found. Expected at: {modelPath}";
            }
        }
        
        /// <summary>
        /// Delete the downloaded model (for testing or cleanup).
        /// </summary>
        public void DeleteModel()
        {
            var modelPath = SileroVadSetup.GetRecommendedModelPath();
            if (File.Exists(modelPath))
            {
                File.Delete(modelPath);
                Debug.Log($"Deleted Silero VAD model: {modelPath}");
            }
            else
            {
                Debug.Log("Silero VAD model not found, nothing to delete.");
            }
        }
        
        /// <summary>
        /// Context menu method for manual download trigger.
        /// </summary>
        [ContextMenu("Download Silero VAD Model")]
        public void DownloadModelManual()
        {
            if (IsDownloadNeeded())
            {
                StartDownload();
            }
            else
            {
                Debug.Log("Silero VAD model already exists.");
            }
        }
        
        /// <summary>
        /// Context menu method to check download status.
        /// </summary>
        [ContextMenu("Check Model Status")]
        public void CheckModelStatus()
        {
            Debug.Log(GetDownloadStatusInfo());
        }
    }
}