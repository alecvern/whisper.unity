using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using JetBrains.Annotations;
using UnityEngine;
using UnityEngine.UI;
// ReSharper disable RedundantCast

namespace Whisper.Utils
{
    /// <summary>
    /// Portion of recorded audio clip.
    /// </summary>
    public struct AudioChunk
    {
        public float[] Data;
        public int Frequency;
        public int Channels;
        public float Length;
        public bool IsVoiceDetected;
    }
    
    public delegate void OnVadChangedDelegate(bool isSpeechDetected);
    public delegate void OnChunkReadyDelegate(AudioChunk chunk);
    public delegate void OnRecordStopDelegate(AudioChunk recordedAudio);
    
    /// <summary>
    /// Controls microphone input settings and recording. 
    /// </summary>
    public class MicrophoneRecord : MonoBehaviour
    {
        [Tooltip("Max length of recorded audio from microphone in seconds")]
        public int maxLengthSec = 60;
        [Tooltip("After reaching max length microphone record will continue")]
        public bool loop;
        [Tooltip("Microphone sample rate")]
        public int frequency = 16000;
        [Tooltip("Length of audio chunks in seconds, useful for streaming")]
        public float chunksLengthSec = 0.5f;
        [Tooltip("Should microphone play echo when recording is complete?")]
        public bool echo = true;
        
        [Header("Voice Activity Detection (VAD)")]
        [Tooltip("Should microphone check if audio input has speech?")]
        public bool useVad = true;
        [Tooltip("How often VAD checks if current audio chunk has speech")]
        public float vadUpdateRateSec = 0.1f;
        [Tooltip("Seconds of audio record that VAD uses to check if chunk has speech")]
        public float vadContextSec = 30f;
        [Tooltip("Window size where VAD tries to detect speech")]
        public float vadLastSec = 1.25f;
        
        [Header("VAD Algorithm Selection")]
        [Tooltip("Type of VAD algorithm to use")]
        public VadType vadType = VadType.Simple;
        
        [Header("Simple VAD Settings")]
        [Tooltip("Threshold of VAD energy activation")]
        public float vadThd = 1.0f;
        [Tooltip("Threshold of VAD filter frequency")]
        public float vadFreqThd = 100.0f;
        
        [Header("Silero VAD Settings")]
        [Tooltip("Path to Silero VAD ONNX model file (relative to StreamingAssets or absolute path)")]
        public string sileroModelPath = "silero_vad.onnx";
        [Tooltip("Silero VAD detection threshold (0.0 to 1.0, typically 0.5)")]
        [Range(0.0f, 1.0f)]
        public float sileroThreshold = 0.5f;
        
        [Header("VAD Visual Feedback")]
        [Tooltip("Optional indicator that changes color when speech detected")]
        [CanBeNull] public Image vadIndicatorImage;
        
        [Header("VAD Stop")]
        [Tooltip("If true microphone will stop record when no speech detected")]
        public bool vadStop;
        [Tooltip("If true whisper transcription will drop last audio where silence was detected")]
        public bool dropVadPart = true;
        [Tooltip("After how many seconds of silence microphone will stop record")]
        public float vadStopTime = 3f;

        [Header("Microphone selection (optional)")] 
        [Tooltip("Optional UI dropdown with all available microphone inputs")]
        [CanBeNull] public Dropdown microphoneDropdown;
        [Tooltip("The label of default microphone input in dropdown")]
        public string microphoneDefaultLabel = "Default microphone";

        /// <summary>
        /// Raised when VAD status changed.
        /// </summary>
        public event OnVadChangedDelegate OnVadChanged;
        /// <summary>
        /// Raised when new audio chunk from microphone is ready.
        /// </summary>
        public event OnChunkReadyDelegate OnChunkReady;
        /// <summary>
        /// Raised when microphone record stopped.
        /// Returns <see cref="maxLengthSec"/> or less of recorded audio.
        /// </summary>
        public event OnRecordStopDelegate OnRecordStop;
        
        private int _lastVadPos;
        private AudioClip _clip;
        private float _length;
        private int _lastChunkPos;
        private int _chunksLength;
        private float? _vadStopBegin;
        private int _lastMicPos;
        private bool _madeLoopLap;

        private string _selectedMicDevice;
        private VadManager _vadManager;

        public string SelectedMicDevice
        {
            get => _selectedMicDevice;
            set
            {
                if (value != null && !AvailableMicDevices.Contains(value))
                    throw new ArgumentException("Microphone device not found");
                _selectedMicDevice = value;
            }
        }

        public int ClipSamples => _clip.samples * _clip.channels;

        public string RecordStartMicDevice { get; private set; }
        public bool IsRecording { get; private set; }
        public bool IsVoiceDetected { get; private set; }

        public IEnumerable<string> AvailableMicDevices => Microphone.devices;

        private void Awake()
        {
            if(microphoneDropdown != null)
            {
                microphoneDropdown.options = AvailableMicDevices
                    .Prepend(microphoneDefaultLabel)
                    .Select(text => new Dropdown.OptionData(text))
                    .ToList();
                microphoneDropdown.value = microphoneDropdown.options
                    .FindIndex(op => op.text == microphoneDefaultLabel);
                microphoneDropdown.onValueChanged.AddListener(OnMicrophoneChanged);
            }
        }

        private void Update()
        {
            if (!IsRecording)
                return;
            
            // lets check current mic position time
            var micPos = Microphone.GetPosition(RecordStartMicDevice);
            if (micPos < _lastMicPos)
            {
                // looks like mic started recording in loop
                // lets check if we even allow do that?
                _madeLoopLap = true;
                if (!loop)
                {
                    LogUtils.Verbose($"Stopping recording, mic pos returned back to {micPos}");
                    StopRecord();
                    return;
                }
                
                // all cool, we can work in loop
                LogUtils.Verbose($"Mic made a new loop lap, continue recording.");
            }
            _lastMicPos = micPos;

            // still recording - update chunks and vad
            UpdateChunks(micPos);
            UpdateVad(micPos);
        }
        
        private void UpdateChunks(int micPos)
        {
            // is anyone even subscribe to do this?
            if (OnChunkReady == null)
                return;

            // check if chunks length is valid
            if (_chunksLength <= 0)
                return;
            
            // get current chunk length
            var chunk = GetMicPosDist(_lastChunkPos, micPos);
            
            // send new chunks while there has valid size
            while (chunk > _chunksLength)
            {
                var origData = new float[_chunksLength];
                _clip.GetData(origData, _lastChunkPos);

                var chunkStruct = new AudioChunk()
                {
                    Data = origData,
                    Frequency = _clip.frequency,
                    Channels = _clip.channels,
                    Length = chunksLengthSec,
                    IsVoiceDetected = IsVoiceDetected
                };
                OnChunkReady(chunkStruct);

                _lastChunkPos = (_lastChunkPos + _chunksLength) % ClipSamples;
                chunk = GetMicPosDist(_lastChunkPos, micPos);
            }
        }
        
        private void UpdateVad(int micPos)
        {
            if (!useVad)
                return;
            
            // get current recorded clip length
            var samplesCount = GetMicBufferLength(micPos);
            if (samplesCount <= 0)
                return;

            // check if it's time to update
            var vadUpdateRateSamples = vadUpdateRateSec * _clip.frequency;
            var dt = GetMicPosDist(_lastVadPos, micPos);
            if (dt < vadUpdateRateSamples)
                return;
            _lastVadPos = samplesCount;
            
            // try to get sample for voice detection
            var data = GetMicBufferLast(micPos, vadContextSec);
            
            // Initialize VAD manager if needed
            if (_vadManager == null)
            {
                InitializeVadManager();
            }
            
            // Perform voice activity detection
            bool vad;
            if (_vadManager != null && _vadManager.IsInitialized)
            {
                vad = _vadManager.DetectVoiceActivity(data, _clip.frequency, vadLastSec);
            }
            else
            {
                // Fallback to simple VAD if manager initialization failed
                LogUtils.Warning("VAD Manager not initialized, falling back to Simple VAD");
                vad = AudioUtils.SimpleVad(data, _clip.frequency, vadLastSec, vadThd, vadFreqThd);
            }

            // raise event if vad has changed
            if (vad != IsVoiceDetected)
            {
                _vadStopBegin = !vad ? Time.realtimeSinceStartup : (float?) null;
                IsVoiceDetected = vad;
                OnVadChanged?.Invoke(vad);   
            }
            
            // update vad indicator
            if (vadIndicatorImage)
            {
                var color = vad ? Color.green : Color.red;
                vadIndicatorImage.color = color;
            }

            UpdateVadStop();
        }
        
        private void UpdateVadStop()
        {
            if (!vadStop || _vadStopBegin == null)
                return;

            var passedTime = Time.realtimeSinceStartup - _vadStopBegin;
            if (passedTime > vadStopTime)
            {
                var dropTime = dropVadPart ? vadStopTime : 0f;
                StopRecord(dropTime);
            }
        }

        private void OnMicrophoneChanged(int ind)
        {
            if (microphoneDropdown == null) return;
            var opt = microphoneDropdown.options[ind];
            SelectedMicDevice = opt.text == microphoneDefaultLabel ? null : opt.text;
        }

        private void InitializeVadManager()
        {
            try
            {
                // Resolve Silero model path
                var modelPath = sileroModelPath;
                if (vadType == VadType.Silero && !string.IsNullOrEmpty(modelPath))
                {
                    // Try StreamingAssets folder first
                    var streamingAssetsPath = Path.Combine(Application.streamingAssetsPath, modelPath);
                    if (File.Exists(streamingAssetsPath))
                    {
                        modelPath = streamingAssetsPath;
                    }
                    else if (!Path.IsPathRooted(modelPath))
                    {
                        // If relative path, make it absolute from the project root
                        modelPath = Path.Combine(Application.dataPath, "..", modelPath);
                    }
                }

                _vadManager = new VadManager(
                    vadType,
                    vadThd,
                    vadFreqThd,
                    modelPath,
                    sileroThreshold
                );

                LogUtils.Log($"VAD Manager initialized with {vadType} VAD");
            }
            catch (Exception e)
            {
                LogUtils.Error($"Failed to initialize VAD Manager: {e.Message}");
                _vadManager = null;
            }
        }

        /// <summary>
        /// Start microphone recording
        /// </summary>
        public void StartRecord()
        {
            if (IsRecording)
                return;
            
            RecordStartMicDevice = SelectedMicDevice;
            _clip = Microphone.Start(RecordStartMicDevice, loop, maxLengthSec, frequency);
            IsRecording = true;

            _lastMicPos = 0;
            _madeLoopLap = false;
            _lastChunkPos = 0;
            _lastVadPos = 0;
            _vadStopBegin = null;
            _chunksLength = (int) (_clip.frequency * _clip.channels * chunksLengthSec);
            
            // Reset VAD state when starting new recording
            _vadManager?.ResetState();
        }

        /// <summary>
        /// Stop microphone record.
        /// </summary>
        /// <param name="dropTimeSec">How many last recording seconds to drop.</param>
        public void StopRecord(float dropTimeSec = 0f)
        {
            if (!IsRecording)
                return;
            
            // get all data from mic audio clip
            var data = GetMicBuffer(dropTimeSec);
            var finalAudio = new AudioChunk()
            {
                Data = data,
                Channels = _clip.channels,
                Frequency = _clip.frequency,
                IsVoiceDetected = IsVoiceDetected,
                Length = (float) data.Length / (_clip.frequency * _clip.channels)
            };
            
            // stop mic audio recording
            Microphone.End(RecordStartMicDevice);
            IsRecording = false;
            Destroy(_clip);
            LogUtils.Verbose($"Stopped microphone recording. Final audio length " +
                             $"{finalAudio.Length} ({finalAudio.Data.Length} samples)");

            // update VAD, no speech with disabled mic
            if (IsVoiceDetected)
            {
                IsVoiceDetected = false;
                OnVadChanged?.Invoke(false);   
            }
            
            // play echo sound
            if (echo)
            {
                var echoClip = AudioClip.Create("echo", data.Length,
                    _clip.channels, _clip.frequency, false);
                echoClip.SetData(data, 0);
                PlayAudioAndDestroy.Play(echoClip, Vector3.zero);
            }

            // finally, fire event
            OnRecordStop?.Invoke(finalAudio);
        }

        /// <summary>
        /// Get all recorded mic buffer.
        /// </summary>
        private float[] GetMicBuffer(float dropTimeSec = 0f)
        {
            var micPos = Microphone.GetPosition(RecordStartMicDevice);
            var len = GetMicBufferLength(micPos);
            if (len == 0) return Array.Empty<float>();
            
            // drop last samples from length if necessary
            var dropTimeSamples = (int) (_clip.frequency * dropTimeSec);
            len = Math.Max(0, len - dropTimeSamples);
            
            // get last len samples from recorded audio
            // offset used to get audio from previous circular buffer lap
            var data = new float[len];
            var offset = _madeLoopLap ? micPos : 0;
            _clip.GetData(data, offset);
            
            return data;
        }

        /// <summary>
        /// Get last sec of recorded mic buffer.
        /// </summary>
        private float[] GetMicBufferLast(int micPos, float lastSec)
        {
            var len = GetMicBufferLength(micPos);
            if (len == 0) 
                return Array.Empty<float>();
            
            var lastSamples = (int) (_clip.frequency * lastSec);
            var dataLength = Math.Min(lastSamples, len);
            var offset = micPos - dataLength;
            if (offset < 0) offset = len + offset;

            var data = new float[dataLength];
            _clip.GetData(data, offset);
            return data;
        }

        /// <summary>
        /// Get mic buffer length that was actually recorded.
        /// </summary>
        private int GetMicBufferLength(int micPos)
        {
            // looks like we just started recording and stopped it immediately
            // nothing was actually recorded
            if (micPos == 0 && !_madeLoopLap) 
                return 0;
            
            // get length of the mic buffer that we want to return
            // this need to account circular loop buffer
            var len = _madeLoopLap ? ClipSamples : micPos;
            return len;
        }

        /// <summary>
        /// Calculate distance between two mic positions.
        /// It takes circular buffer into account.
        /// </summary>
        private int GetMicPosDist(int prevPos, int newPos)
        {
            if (newPos >= prevPos)
                return newPos - prevPos;

            // circular buffer case
            return ClipSamples - prevPos + newPos;
        }

        private void OnDestroy()
        {
            _vadManager?.Dispose();
        }
    }
}