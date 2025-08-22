using System.IO;
using UnityEngine;
using UnityEditor;

namespace Whisper.Utils
{
    /// <summary>
    /// Helper utility to download and setup Silero VAD model for Unity.
    /// </summary>
    public class SileroVadSetup
    {
        private const string SILERO_VAD_MODEL_URL = "https://github.com/snakers4/silero-vad/raw/master/src/silero_vad/data/silero_vad.onnx";
        private const string MODEL_FILENAME = "silero_vad.onnx";
        
        /// <summary>
        /// Get the recommended path for storing the Silero VAD model in StreamingAssets.
        /// </summary>
        public static string GetRecommendedModelPath()
        {
            return Path.Combine(Application.streamingAssetsPath, MODEL_FILENAME);
        }
        
        /// <summary>
        /// Check if Silero VAD model exists in the recommended location.
        /// </summary>
        public static bool IsModelAvailable()
        {
            return File.Exists(GetRecommendedModelPath());
        }
        
        /// <summary>
        /// Get instructions for downloading and setting up the Silero VAD model.
        /// </summary>
        public static string GetSetupInstructions()
        {
            return $@"To use Silero VAD, you need to download the ONNX model file:

1. Download the Silero VAD model from:
   {SILERO_VAD_MODEL_URL}

2. Place the model file in your project's StreamingAssets folder:
   {GetRecommendedModelPath()}

3. If StreamingAssets folder doesn't exist, create it in your Assets folder.

4. Install Microsoft.ML.OnnxRuntime package:
   - Open Package Manager
   - Click '+' and 'Add package by name'
   - Enter: com.unity.nuget.onnxruntime (if available)
   - Or manually add Microsoft.ML.OnnxRuntime via NuGet

Alternative: You can also place the model anywhere and specify the full path in the MicrophoneRecord component.

Note: Silero VAD requires ONNX Runtime. If not available, the system will automatically fall back to Simple VAD.";
        }

#if UNITY_EDITOR
        /// <summary>
        /// Create StreamingAssets folder if it doesn't exist.
        /// </summary>
        [MenuItem("Tools/Whisper/Create StreamingAssets Folder")]
        public static void CreateStreamingAssetsFolder()
        {
            var streamingAssetsPath = Path.Combine(Application.dataPath, "StreamingAssets");
            if (!Directory.Exists(streamingAssetsPath))
            {
                Directory.CreateDirectory(streamingAssetsPath);
                AssetDatabase.Refresh();
                Debug.Log($"Created StreamingAssets folder at: {streamingAssetsPath}");
            }
            else
            {
                Debug.Log($"StreamingAssets folder already exists at: {streamingAssetsPath}");
            }
        }
        
        /// <summary>
        /// Show setup instructions in the console.
        /// </summary>
        [MenuItem("Tools/Whisper/Silero VAD Setup Instructions")]
        public static void ShowSetupInstructions()
        {
            Debug.Log(GetSetupInstructions());
        }
#endif
    }
}