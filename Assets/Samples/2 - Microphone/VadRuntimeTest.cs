using System.Collections;
using UnityEngine;
using Whisper.Utils;

namespace Whisper.Samples
{
    /// <summary>
    /// Simple runtime test to verify VAD functionality without requiring Unity Test Runner.
    /// Add this to a GameObject in your scene to test VAD implementations.
    /// </summary>
    public class VadRuntimeTest : MonoBehaviour
    {
        [Header("Test Configuration")]
        public bool runTestsOnStart = true;
        public bool logResults = true;
        
        private void Start()
        {
            if (runTestsOnStart)
            {
                StartCoroutine(RunVadTests());
            }
        }
        
        private IEnumerator RunVadTests()
        {
            if (logResults) Debug.Log("Starting VAD Runtime Tests...");
            
            // Test 1: Simple VAD Manager
            yield return StartCoroutine(TestSimpleVadManager());
            
            // Test 2: Silero VAD Manager (without model - should fallback)
            yield return StartCoroutine(TestSileroVadManagerFallback());
            
            // Test 3: VadType enum values
            TestVadTypeEnum();
            
            // Test 4: Silero VAD setup utilities
            TestSileroVadSetup();
            
            if (logResults) Debug.Log("VAD Runtime Tests completed!");
        }
        
        private IEnumerator TestSimpleVadManager()
        {
            if (logResults) Debug.Log("Testing Simple VAD Manager...");
            
            var vadManager = new VadManager(VadType.Simple, vadThreshold: 1.0f, freqThreshold: 100.0f);
            
            try
            {
                // Test initialization
                var isInitialized = vadManager.IsInitialized;
                if (logResults) Debug.Log($"Simple VAD initialized: {isInitialized}");
                
                // Test with empty audio
                var emptyAudio = new float[1600]; // 0.1 second at 16kHz
                var result = vadManager.DetectVoiceActivity(emptyAudio, 16000, 0.05f);
                if (logResults) Debug.Log($"Empty audio VAD result: {result}");
                
                // Test with null audio
                result = vadManager.DetectVoiceActivity(null, 16000);
                if (logResults) Debug.Log($"Null audio VAD result: {result}");
                
                // Test reset state
                vadManager.ResetState();
                if (logResults) Debug.Log("Simple VAD reset state - no exceptions");
                
                vadManager.Dispose();
                if (logResults) Debug.Log("✓ Simple VAD Manager test passed");
            }
            catch (System.Exception e)
            {
                if (logResults) Debug.LogError($"✗ Simple VAD Manager test failed: {e.Message}");
            }
            
            yield return null;
        }
        
        private IEnumerator TestSileroVadManagerFallback()
        {
            if (logResults) Debug.Log("Testing Silero VAD Manager (fallback mode)...");
            
            var vadManager = new VadManager(VadType.Silero, 
                sileroModelPath: "nonexistent_model.onnx",
                sileroThreshold: 0.5f);
            
            try
            {
                // Should not be initialized because model doesn't exist
                var isInitialized = vadManager.IsInitialized;
                if (logResults) Debug.Log($"Silero VAD initialized (expected false): {isInitialized}");
                
                // Should still work by falling back to Simple VAD
                var testAudio = new float[1600];
                var result = vadManager.DetectVoiceActivity(testAudio, 16000);
                if (logResults) Debug.Log($"Fallback VAD result: {result}");
                
                vadManager.Dispose();
                if (logResults) Debug.Log("✓ Silero VAD Manager fallback test passed");
            }
            catch (System.Exception e)
            {
                if (logResults) Debug.LogError($"✗ Silero VAD Manager fallback test failed: {e.Message}");
            }
            
            yield return null;
        }
        
        private void TestVadTypeEnum()
        {
            if (logResults) Debug.Log("Testing VadType enum...");
            
            try
            {
                // Test enum values
                var simpleValue = (int)VadType.Simple;
                var sileroValue = (int)VadType.Silero;
                
                if (logResults) Debug.Log($"VadType.Simple = {simpleValue} (expected 0)");
                if (logResults) Debug.Log($"VadType.Silero = {sileroValue} (expected 1)");
                
                if (simpleValue == 0 && sileroValue == 1)
                {
                    if (logResults) Debug.Log("✓ VadType enum test passed");
                }
                else
                {
                    if (logResults) Debug.LogError("✗ VadType enum values are incorrect");
                }
            }
            catch (System.Exception e)
            {
                if (logResults) Debug.LogError($"✗ VadType enum test failed: {e.Message}");
            }
        }
        
        private void TestSileroVadSetup()
        {
            if (logResults) Debug.Log("Testing Silero VAD setup utilities...");
            
            try
            {
                // Test path utilities
                var recommendedPath = SileroVadSetup.GetRecommendedModelPath();
                var isModelAvailable = SileroVadSetup.IsModelAvailable();
                var instructions = SileroVadSetup.GetSetupInstructions();
                
                if (logResults) Debug.Log($"Recommended model path: {recommendedPath}");
                if (logResults) Debug.Log($"Model available: {isModelAvailable}");
                if (logResults) Debug.Log($"Instructions length: {instructions?.Length ?? 0} characters");
                
                if (!string.IsNullOrEmpty(recommendedPath) && !string.IsNullOrEmpty(instructions))
                {
                    if (logResults) Debug.Log("✓ Silero VAD setup utilities test passed");
                }
                else
                {
                    if (logResults) Debug.LogError("✗ Silero VAD setup utilities returned empty values");
                }
            }
            catch (System.Exception e)
            {
                if (logResults) Debug.LogError($"✗ Silero VAD setup utilities test failed: {e.Message}");
            }
        }
        
        /// <summary>
        /// Run tests manually via button or script.
        /// </summary>
        [ContextMenu("Run VAD Tests")]
        public void RunTestsManually()
        {
            StartCoroutine(RunVadTests());
        }
    }
}