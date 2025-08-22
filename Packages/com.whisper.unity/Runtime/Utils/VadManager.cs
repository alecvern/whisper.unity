using System;
using UnityEngine;

namespace Whisper.Utils
{
    /// <summary>
    /// Unified Voice Activity Detection manager that handles both Simple and Silero VAD implementations.
    /// </summary>
    public class VadManager : IDisposable
    {
        private readonly VadType _vadType;
        private SileroVad _sileroVad;
        
        // Simple VAD settings
        private readonly float _simpleVadThreshold;
        private readonly float _simpleFreqThreshold;
        
        // Silero VAD settings
        private readonly string _sileroModelPath;
        private readonly float _sileroThreshold;
        
        public VadType VadType => _vadType;
        public bool IsInitialized { get; private set; }

        /// <summary>
        /// Initialize VAD manager with specified type and settings.
        /// </summary>
        public VadManager(VadType vadType, 
            float simpleVadThreshold = 1.0f, 
            float simpleFreqThreshold = 100.0f,
            string sileroModelPath = "",
            float sileroThreshold = 0.5f)
        {
            _vadType = vadType;
            _simpleVadThreshold = simpleVadThreshold;
            _simpleFreqThreshold = simpleFreqThreshold;
            _sileroModelPath = sileroModelPath;
            _sileroThreshold = sileroThreshold;
            
            Initialize();
        }

        private void Initialize()
        {
            switch (_vadType)
            {
                case VadType.Simple:
                    IsInitialized = true;
                    LogUtils.Log("VAD Manager initialized with Simple VAD");
                    break;
                    
                case VadType.Silero:
                    if (string.IsNullOrEmpty(_sileroModelPath))
                    {
                        LogUtils.Error("Silero VAD model path not specified. Falling back to Simple VAD.");
                        IsInitialized = false;
                        return;
                    }
                    
                    _sileroVad = new SileroVad(_sileroModelPath, _sileroThreshold);
                    IsInitialized = _sileroVad.IsInitialized;
                    
                    if (IsInitialized)
                    {
                        LogUtils.Log("VAD Manager initialized with Silero VAD");
                    }
                    else
                    {
                        LogUtils.Warning("Failed to initialize Silero VAD, will fallback to Simple VAD");
                    }
                    break;
                    
                default:
                    LogUtils.Error($"Unknown VAD type: {_vadType}");
                    IsInitialized = false;
                    break;
            }
        }

        /// <summary>
        /// Detect voice activity in audio data using the configured VAD algorithm.
        /// </summary>
        /// <param name="audioData">Audio samples</param>
        /// <param name="sampleRate">Sample rate of audio data</param>
        /// <param name="lastSec">Window size for analysis (used by Simple VAD)</param>
        /// <returns>True if voice activity detected</returns>
        public bool DetectVoiceActivity(float[] audioData, int sampleRate, float lastSec = 1.25f)
        {
            if (audioData == null || audioData.Length == 0)
                return false;

            switch (_vadType)
            {
                case VadType.Simple:
                    return AudioUtils.SimpleVad(audioData, sampleRate, lastSec, _simpleVadThreshold, _simpleFreqThreshold);
                    
                case VadType.Silero:
                    if (_sileroVad != null && _sileroVad.IsInitialized)
                    {
                        return _sileroVad.DetectVoiceActivity(audioData, sampleRate);
                    }
                    else
                    {
                        // Fallback to Simple VAD if Silero VAD failed
                        LogUtils.Warning("Silero VAD not available, falling back to Simple VAD");
                        return AudioUtils.SimpleVad(audioData, sampleRate, lastSec, _simpleVadThreshold, _simpleFreqThreshold);
                    }
                    
                default:
                    LogUtils.Warning($"Unknown VAD type {_vadType}, using Simple VAD");
                    return AudioUtils.SimpleVad(audioData, sampleRate, lastSec, _simpleVadThreshold, _simpleFreqThreshold);
            }
        }

        /// <summary>
        /// Reset VAD internal state. Call when starting a new audio stream.
        /// </summary>
        public void ResetState()
        {
            _sileroVad?.ResetState();
        }

        public void Dispose()
        {
            _sileroVad?.Dispose();
            _sileroVad = null;
        }
    }
}