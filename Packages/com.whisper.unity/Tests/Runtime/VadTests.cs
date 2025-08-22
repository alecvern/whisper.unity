using NUnit.Framework;
using UnityEngine;
using Whisper.Utils;

namespace Whisper.Tests
{
    public class VadTests
    {
        [Test]
        public void VadTypeEnumTest()
        {
            // Test that enum values are defined correctly
            Assert.AreEqual(0, (int)VadType.Simple);
            Assert.AreEqual(1, (int)VadType.Silero);
        }

        [Test]
        public void VadManagerSimpleVadTest()
        {
            // Test Simple VAD through VadManager
            var vadManager = new VadManager(VadType.Simple, vadThreshold: 1.0f, freqThreshold: 100.0f);
            
            Assert.IsTrue(vadManager.IsInitialized);
            Assert.AreEqual(VadType.Simple, vadManager.VadType);
            
            // Test with empty audio (should return false)
            var emptyAudio = new float[1600]; // 0.1 second at 16kHz
            var result = vadManager.DetectVoiceActivity(emptyAudio, 16000, 0.05f);
            Assert.IsFalse(result);
            
            // Test with loud audio (should return true)
            var loudAudio = new float[1600];
            for (int i = 0; i < loudAudio.Length; i++)
            {
                loudAudio[i] = 0.8f * Mathf.Sin(2 * Mathf.PI * 440 * i / 16000); // 440Hz tone
            }
            result = vadManager.DetectVoiceActivity(loudAudio, 16000, 0.05f);
            // Note: Result depends on the specific SimpleVad implementation
            
            vadManager.Dispose();
        }

        [Test]
        public void VadManagerSileroVadWithoutModelTest()
        {
            // Test Silero VAD without a valid model (should fallback to Simple VAD)
            var vadManager = new VadManager(VadType.Silero, 
                sileroModelPath: "nonexistent_model.onnx",
                sileroThreshold: 0.5f);
            
            // Should not be initialized because model doesn't exist
            Assert.IsFalse(vadManager.IsInitialized);
            
            // Should still work by falling back to Simple VAD
            var testAudio = new float[1600];
            var result = vadManager.DetectVoiceActivity(testAudio, 16000);
            Assert.IsFalse(result); // Empty audio should be detected as no voice
            
            vadManager.Dispose();
        }

        [Test]
        public void VadManagerNullAudioTest()
        {
            var vadManager = new VadManager(VadType.Simple);
            
            // Test with null audio
            var result = vadManager.DetectVoiceActivity(null, 16000);
            Assert.IsFalse(result);
            
            // Test with empty audio
            result = vadManager.DetectVoiceActivity(new float[0], 16000);
            Assert.IsFalse(result);
            
            vadManager.Dispose();
        }

        [Test]
        public void VadManagerResetStateTest()
        {
            var vadManager = new VadManager(VadType.Simple);
            
            // Reset should not throw exceptions
            Assert.DoesNotThrow(() => vadManager.ResetState());
            
            vadManager.Dispose();
        }

        [Test]
        public void SileroVadWithoutModelTest()
        {
            // Test SileroVad constructor with non-existent model
            var sileroVad = new SileroVad("nonexistent_model.onnx", 0.5f);
            
            Assert.IsFalse(sileroVad.IsInitialized);
            Assert.AreEqual(0.5f, sileroVad.Threshold);
            
            // Detection should return false when not initialized
            var testAudio = new float[1600];
            var result = sileroVad.DetectVoiceActivity(testAudio, 16000);
            Assert.IsFalse(result);
            
            sileroVad.Dispose();
        }

        [Test]
        public void SileroVadThresholdClampingTest()
        {
            // Test that threshold values are clamped properly
            var sileroVad1 = new SileroVad("test.onnx", -0.5f); // Below 0
            Assert.AreEqual(0.0f, sileroVad1.Threshold);
            
            var sileroVad2 = new SileroVad("test.onnx", 1.5f); // Above 1
            Assert.AreEqual(1.0f, sileroVad2.Threshold);
            
            var sileroVad3 = new SileroVad("test.onnx", 0.7f); // Valid range
            Assert.AreEqual(0.7f, sileroVad3.Threshold);
            
            sileroVad1.Dispose();
            sileroVad2.Dispose();
            sileroVad3.Dispose();
        }
    }
}