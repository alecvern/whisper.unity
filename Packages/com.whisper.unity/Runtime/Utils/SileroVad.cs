using System;
using System.IO;
using UnityEngine;

#if UNITY_EDITOR || ONNX_RUNTIME_AVAILABLE
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
#endif

namespace Whisper.Utils
{
    /// <summary>
    /// Silero VAD implementation using ONNX runtime for accurate voice activity detection.
    /// Requires Microsoft.ML.OnnxRuntime package to be installed.
    /// </summary>
    public class SileroVad : IDisposable
    {
        private const int SAMPLE_RATE = 16000;
        private const int WINDOW_SIZE_SAMPLES = 1536; // 96ms at 16kHz (1536 = 16000 * 0.096)
        
        // Model expects specific input tensor shape and names
        private const string INPUT_NAME = "input";
        private const string STATE_H_INPUT = "h";
        private const string STATE_C_INPUT = "c";
        private const string OUTPUT_NAME = "output";
        private const string STATE_H_OUTPUT = "hn";
        private const string STATE_C_OUTPUT = "cn";

#if UNITY_EDITOR || ONNX_RUNTIME_AVAILABLE
        private InferenceSession _session;
        private float[] _h = new float[2 * 1 * 64]; // LSTM hidden state
        private float[] _c = new float[2 * 1 * 64]; // LSTM cell state
#endif
        
        private readonly float _threshold;
        private readonly bool _isInitialized;
        
        public bool IsInitialized => _isInitialized;
        public float Threshold => _threshold;

        /// <summary>
        /// Initialize Silero VAD with model path and threshold.
        /// </summary>
        /// <param name="modelPath">Path to the Silero VAD ONNX model file</param>
        /// <param name="threshold">Voice detection threshold (0.0 to 1.0, typically 0.5)</param>
        public SileroVad(string modelPath, float threshold = 0.5f)
        {
            _threshold = Mathf.Clamp01(threshold);
            
#if UNITY_EDITOR || ONNX_RUNTIME_AVAILABLE
            try
            {
                if (!File.Exists(modelPath))
                {
                    LogUtils.Error($"Silero VAD model not found at path: {modelPath}");
                    return;
                }

                // Initialize ONNX Runtime session
                var sessionOptions = new SessionOptions();
                sessionOptions.LogSeverityLevel = OrtLoggingLevel.ORT_LOGGING_LEVEL_WARNING;
                
                _session = new InferenceSession(modelPath, sessionOptions);
                _isInitialized = true;
                
                LogUtils.Log($"Silero VAD initialized successfully with model: {modelPath}");
            }
            catch (Exception e)
            {
                LogUtils.Error($"Failed to initialize Silero VAD: {e.Message}");
                _isInitialized = false;
            }
#else
            LogUtils.Error("Silero VAD requires ONNX Runtime. Please install Microsoft.ML.OnnxRuntime package or define ONNX_RUNTIME_AVAILABLE.");
            _isInitialized = false;
#endif
        }

        /// <summary>
        /// Detect voice activity in audio data.
        /// </summary>
        /// <param name="audioData">Audio samples (mono, 16kHz)</param>
        /// <param name="sampleRate">Sample rate of audio data</param>
        /// <returns>True if voice activity detected</returns>
        public bool DetectVoiceActivity(float[] audioData, int sampleRate = SAMPLE_RATE)
        {
            if (!_isInitialized)
            {
                LogUtils.Warning("Silero VAD not initialized, falling back to simple VAD");
                return false;
            }

#if UNITY_EDITOR || ONNX_RUNTIME_AVAILABLE
            try
            {
                // Resample if necessary (Silero VAD expects 16kHz)
                var processedAudio = audioData;
                if (sampleRate != SAMPLE_RATE)
                {
                    processedAudio = AudioUtils.ChangeSampleRate(audioData, sampleRate, SAMPLE_RATE);
                }

                // Process audio in chunks
                return ProcessAudioChunks(processedAudio);
            }
            catch (Exception e)
            {
                LogUtils.Error($"Error in Silero VAD detection: {e.Message}");
                return false;
            }
#else
            return false;
#endif
        }

#if UNITY_EDITOR || ONNX_RUNTIME_AVAILABLE
        private bool ProcessAudioChunks(float[] audioData)
        {
            if (audioData.Length < WINDOW_SIZE_SAMPLES)
            {
                // Not enough samples, pad with zeros
                var paddedAudio = new float[WINDOW_SIZE_SAMPLES];
                Array.Copy(audioData, paddedAudio, audioData.Length);
                return ProcessSingleChunk(paddedAudio);
            }

            // Process the last chunk of audio (most recent samples)
            var startIndex = Math.Max(0, audioData.Length - WINDOW_SIZE_SAMPLES);
            var chunk = new float[WINDOW_SIZE_SAMPLES];
            Array.Copy(audioData, startIndex, chunk, 0, WINDOW_SIZE_SAMPLES);
            
            return ProcessSingleChunk(chunk);
        }

        private bool ProcessSingleChunk(float[] audioChunk)
        {
            // Create input tensors
            var inputTensor = new DenseTensor<float>(audioChunk, new[] { 1, audioChunk.Length });
            var hTensor = new DenseTensor<float>(_h, new[] { 2, 1, 64 });
            var cTensor = new DenseTensor<float>(_c, new[] { 2, 1, 64 });

            var inputs = new[]
            {
                NamedOnnxValue.CreateFromTensor(INPUT_NAME, inputTensor),
                NamedOnnxValue.CreateFromTensor(STATE_H_INPUT, hTensor),
                NamedOnnxValue.CreateFromTensor(STATE_C_INPUT, cTensor)
            };

            // Run inference
            using var results = _session.Run(inputs);
            
            // Get output probability
            var outputTensor = results.First(r => r.Name == OUTPUT_NAME).AsTensor<float>();
            var voiceProbability = outputTensor[0];

            // Update LSTM states for next iteration
            var newHTensor = results.First(r => r.Name == STATE_H_OUTPUT).AsTensor<float>();
            var newCTensor = results.First(r => r.Name == STATE_C_OUTPUT).AsTensor<float>();
            
            newHTensor.ToArray().CopyTo(_h, 0);
            newCTensor.ToArray().CopyTo(_c, 0);

            return voiceProbability >= _threshold;
        }
#endif

        /// <summary>
        /// Reset the internal LSTM state. Call this when starting a new audio stream.
        /// </summary>
        public void ResetState()
        {
#if UNITY_EDITOR || ONNX_RUNTIME_AVAILABLE
            // Reset LSTM states to zeros
            Array.Clear(_h, 0, _h.Length);
            Array.Clear(_c, 0, _c.Length);
#endif
        }

        public void Dispose()
        {
#if UNITY_EDITOR || ONNX_RUNTIME_AVAILABLE
            _session?.Dispose();
#endif
        }
    }
}