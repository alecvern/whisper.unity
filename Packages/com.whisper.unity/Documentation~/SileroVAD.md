# Silero VAD Integration

This document describes the Silero VAD (Voice Activity Detection) integration in Whisper Unity package.

## Overview

Silero VAD is a neural network-based voice activity detection system that provides more accurate speech detection compared to the simple energy-based VAD. This implementation adds Silero VAD as an alternative option while maintaining full backward compatibility with the existing Simple VAD.

## Features

- **Dual VAD Support**: Choose between Simple VAD (energy-based) and Silero VAD (neural network-based)
- **Backward Compatibility**: Existing projects continue to work without changes
- **Automatic Fallback**: If Silero VAD fails to initialize, the system automatically falls back to Simple VAD
- **Unity Integration**: Seamless integration with Unity's inspector and audio system
- **State Management**: Proper LSTM state management for streaming audio

## Setup Instructions

### 1. Install ONNX Runtime

Silero VAD requires the Microsoft.ML.OnnxRuntime package. You have several options:

#### Option A: Package Manager (Recommended)
1. Open Unity Package Manager
2. Click "+" and select "Add package by name"
3. Enter: `com.unity.nuget.onnxruntime` (if available)

#### Option B: Manual Installation
1. Download the Microsoft.ML.OnnxRuntime NuGet package
2. Extract the appropriate Unity-compatible DLLs
3. Place them in your project's Plugins folder

#### Option C: Define Symbol
If you have ONNX Runtime installed through other means, add `ONNX_RUNTIME_AVAILABLE` to your project's scripting define symbols.

### 2. Download Silero VAD Model

1. Download the Silero VAD ONNX model:
   ```
   https://github.com/snakers4/silero-vad/raw/master/src/silero_vad/data/silero_vad.onnx
   ```

2. Create a `StreamingAssets` folder in your `Assets` directory (if it doesn't exist)

3. Place the `silero_vad.onnx` file in the `StreamingAssets` folder

Alternatively, you can use the Unity menu: `Tools/Whisper/Create StreamingAssets Folder`

### 3. Configure MicrophoneRecord Component

1. Select your GameObject with the `MicrophoneRecord` component
2. In the inspector, find the "VAD Algorithm Selection" section
3. Set "Vad Type" to "Silero"
4. Verify "Silero Model Path" points to your model file (default: "silero_vad.onnx")
5. Adjust "Silero Threshold" if needed (default: 0.5, range: 0.0-1.0)

## Configuration Options

### VAD Type Selection
- **Simple**: Energy-based VAD (default, no additional dependencies)
- **Silero**: Neural network-based VAD (requires ONNX Runtime and model file)

### Simple VAD Settings
- **Vad Thd**: Energy threshold for voice detection (default: 1.0)
- **Vad Freq Thd**: High-pass filter frequency threshold (default: 100.0)

### Silero VAD Settings
- **Silero Model Path**: Path to the ONNX model file (default: "silero_vad.onnx")
- **Silero Threshold**: Detection threshold, 0.0-1.0 (default: 0.5)

## API Usage

### VadManager Class

```csharp
// Create VAD manager with Simple VAD
var vadManager = new VadManager(VadType.Simple, vadThreshold: 1.0f, freqThreshold: 100.0f);

// Create VAD manager with Silero VAD
var vadManager = new VadManager(VadType.Silero, 
    sileroModelPath: "path/to/silero_vad.onnx",
    sileroThreshold: 0.5f);

// Detect voice activity
bool isVoiceDetected = vadManager.DetectVoiceActivity(audioData, sampleRate);

// Reset state (important for streaming audio)
vadManager.ResetState();

// Cleanup
vadManager.Dispose();
```

### SileroVad Class

```csharp
// Direct Silero VAD usage
var sileroVad = new SileroVad("path/to/silero_vad.onnx", threshold: 0.5f);

if (sileroVad.IsInitialized)
{
    bool isVoiceDetected = sileroVad.DetectVoiceActivity(audioData, sampleRate);
}

sileroVad.Dispose();
```

## Troubleshooting

### Common Issues

1. **"Silero VAD requires ONNX Runtime" Error**
   - Install Microsoft.ML.OnnxRuntime package
   - Add `ONNX_RUNTIME_AVAILABLE` to scripting define symbols

2. **"Silero VAD model not found" Error**
   - Verify the model file exists at the specified path
   - Check the path in StreamingAssets folder
   - Use absolute path if relative path doesn't work

3. **Automatic Fallback to Simple VAD**
   - This is normal behavior when Silero VAD can't initialize
   - Check console for specific error messages
   - Verify ONNX Runtime installation and model path

### Debug Information

Use Unity menu `Tools/Whisper/Silero VAD Setup Instructions` to show setup instructions in the console.

## Performance Considerations

- **Silero VAD**: More accurate but requires more computational resources
- **Simple VAD**: Faster but less accurate, good for basic use cases
- **Sample Rate**: Silero VAD expects 16kHz audio (automatic resampling is performed)
- **Window Size**: Silero VAD processes audio in 96ms windows (1536 samples at 16kHz)

## Backward Compatibility

- Existing projects using Simple VAD will continue to work unchanged
- Default VAD type is Simple VAD
- All existing MicrophoneRecord configurations remain valid
- No breaking changes to the public API