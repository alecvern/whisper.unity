using UnityEngine;
using UnityEditor;
using System.IO;
using Whisper.Utils;

namespace Whisper.Editor
{
    /// <summary>
    /// Custom inspector for MicrophoneRecord to provide better VAD configuration UI.
    /// </summary>
    [CustomEditor(typeof(MicrophoneRecord))]
    public class MicrophoneRecordEditor : UnityEditor.Editor
    {
        private SerializedProperty vadTypeProperty;
        private SerializedProperty sileroModelPathProperty;
        private SerializedProperty sileroThresholdProperty;
        
        private void OnEnable()
        {
            vadTypeProperty = serializedObject.FindProperty("vadType");
            sileroModelPathProperty = serializedObject.FindProperty("sileroModelPath");
            sileroThresholdProperty = serializedObject.FindProperty("sileroThreshold");
        }
        
        public override void OnInspectorGUI()
        {
            // Draw default inspector
            DrawDefaultInspector();
            
            serializedObject.Update();
            
            // Add custom VAD configuration section
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("VAD Configuration", EditorStyles.boldLabel);
            
            var microphoneRecord = (MicrophoneRecord)target;
            var currentVadType = (VadType)vadTypeProperty.enumValueIndex;
            
            if (currentVadType == VadType.Silero)
            {
                DrawSileroVadConfiguration(microphoneRecord);
            }
            
            serializedObject.ApplyModifiedProperties();
        }
        
        private void DrawSileroVadConfiguration(MicrophoneRecord microphoneRecord)
        {
            EditorGUILayout.Space();
            
            // Check if Silero model is available
            var modelPath = sileroModelPathProperty.stringValue;
            var fullModelPath = GetFullModelPath(modelPath);
            var modelExists = !string.IsNullOrEmpty(fullModelPath) && File.Exists(fullModelPath);
            
            // Status box
            var statusColor = modelExists ? Color.green : Color.yellow;
            var statusMessage = modelExists ? 
                "✓ Silero VAD model found" : 
                "⚠ Silero VAD model not found";
            
            var oldColor = GUI.backgroundColor;
            GUI.backgroundColor = statusColor;
            EditorGUILayout.HelpBox(statusMessage, modelExists ? MessageType.Info : MessageType.Warning);
            GUI.backgroundColor = oldColor;
            
            if (!modelExists)
            {
                EditorGUILayout.Space();
                
                if (GUILayout.Button("Show Setup Instructions"))
                {
                    ShowSileroVadSetupWindow();
                }
                
                if (GUILayout.Button("Create StreamingAssets Folder"))
                {
                    SileroVadSetup.CreateStreamingAssetsFolder();
                }
                
                EditorGUILayout.Space();
                EditorGUILayout.HelpBox(
                    "Download the Silero VAD model and place it in your StreamingAssets folder. " +
                    "See setup instructions for details.", 
                    MessageType.Info);
            }
            
            // Show model path info
            if (!string.IsNullOrEmpty(fullModelPath))
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Resolved Model Path:", EditorStyles.miniLabel);
                EditorGUILayout.SelectableLabel(fullModelPath, EditorStyles.textField, GUILayout.Height(16));
            }
            
            // ONNX Runtime status
            EditorGUILayout.Space();
            CheckOnnxRuntimeStatus();
        }
        
        private string GetFullModelPath(string modelPath)
        {
            if (string.IsNullOrEmpty(modelPath))
                return "";
                
            // Try StreamingAssets folder first
            var streamingAssetsPath = Path.Combine(Application.streamingAssetsPath, modelPath);
            if (File.Exists(streamingAssetsPath))
                return streamingAssetsPath;
                
            // Try absolute path
            if (Path.IsPathRooted(modelPath) && File.Exists(modelPath))
                return modelPath;
                
            // Try relative to project root
            var projectPath = Path.Combine(Application.dataPath, "..", modelPath);
            if (File.Exists(projectPath))
                return Path.GetFullPath(projectPath);
                
            return "";
        }
        
        private void CheckOnnxRuntimeStatus()
        {
            var symbols = PlayerSettings.GetScriptingDefineSymbolsForGroup(BuildTargetGroup.Standalone);
            var hasOnnxSymbol = symbols.Contains("ONNX_RUNTIME_AVAILABLE");
            
            var statusMessage = hasOnnxSymbol ? 
                "✓ ONNX Runtime symbol defined" : 
                "⚠ ONNX Runtime not configured";
                
            var messageType = hasOnnxSymbol ? MessageType.Info : MessageType.Warning;
            EditorGUILayout.HelpBox(statusMessage, messageType);
            
            if (!hasOnnxSymbol)
            {
                EditorGUILayout.Space();
                if (GUILayout.Button("Add ONNX_RUNTIME_AVAILABLE Symbol"))
                {
                    AddOnnxRuntimeSymbol();
                }
                
                EditorGUILayout.HelpBox(
                    "Add the ONNX_RUNTIME_AVAILABLE scripting define symbol if you have " +
                    "Microsoft.ML.OnnxRuntime installed in your project.", 
                    MessageType.Info);
            }
        }
        
        private void AddOnnxRuntimeSymbol()
        {
            var group = BuildTargetGroup.Standalone;
            var symbols = PlayerSettings.GetScriptingDefineSymbolsForGroup(group);
            
            if (!symbols.Contains("ONNX_RUNTIME_AVAILABLE"))
            {
                var newSymbols = string.IsNullOrEmpty(symbols) ? 
                    "ONNX_RUNTIME_AVAILABLE" : 
                    symbols + ";ONNX_RUNTIME_AVAILABLE";
                    
                PlayerSettings.SetScriptingDefineSymbolsForGroup(group, newSymbols);
                Debug.Log("Added ONNX_RUNTIME_AVAILABLE to scripting define symbols");
            }
        }
        
        private void ShowSileroVadSetupWindow()
        {
            SileroVadSetupWindow.ShowWindow();
        }
    }
    
    /// <summary>
    /// Window showing Silero VAD setup instructions.
    /// </summary>
    public class SileroVadSetupWindow : EditorWindow
    {
        private Vector2 scrollPosition;
        
        [MenuItem("Tools/Whisper/Silero VAD Setup")]
        public static void ShowWindow()
        {
            var window = GetWindow<SileroVadSetupWindow>("Silero VAD Setup");
            window.minSize = new Vector2(500, 400);
            window.Show();
        }
        
        private void OnGUI()
        {
            EditorGUILayout.LabelField("Silero VAD Setup Instructions", EditorStyles.boldLabel);
            EditorGUILayout.Space();
            
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
            
            var instructions = SileroVadSetup.GetSetupInstructions();
            EditorGUILayout.TextArea(instructions, EditorStyles.wordWrappedLabel);
            
            EditorGUILayout.EndScrollView();
            
            EditorGUILayout.Space();
            
            EditorGUILayout.BeginHorizontal();
            
            if (GUILayout.Button("Create StreamingAssets Folder"))
            {
                SileroVadSetup.CreateStreamingAssetsFolder();
            }
            
            if (GUILayout.Button("Open StreamingAssets Folder"))
            {
                var path = Application.streamingAssetsPath;
                if (Directory.Exists(path))
                {
                    EditorUtility.RevealInFinder(path);
                }
                else
                {
                    Debug.LogWarning("StreamingAssets folder doesn't exist. Create it first.");
                }
            }
            
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.Space();
            
            var modelPath = SileroVadSetup.GetRecommendedModelPath();
            var modelExists = SileroVadSetup.IsModelAvailable();
            
            var statusMessage = modelExists ? 
                "✓ Silero VAD model found at: " + modelPath :
                "⚠ Silero VAD model not found. Expected at: " + modelPath;
                
            var messageType = modelExists ? MessageType.Info : MessageType.Warning;
            EditorGUILayout.HelpBox(statusMessage, messageType);
        }
    }
}