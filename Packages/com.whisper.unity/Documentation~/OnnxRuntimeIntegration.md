# ONNX Runtime Integration Guide

This document explains how the Silero VAD implementation handles ONNX Runtime dependencies in a Unity package.

## Problem Statement

Unity packages cannot directly include NuGet packages or external dependencies. The Microsoft.ML.OnnxRuntime package is not available through Unity's Package Manager, making it challenging to include ONNX-based features in Unity packages.

## Solution Approach

We use **conditional compilation** to handle the ONNX Runtime dependency gracefully:

### 1. Conditional Compilation Directives

```csharp
#if UNITY_EDITOR || ONNX_RUNTIME_AVAILABLE
// ONNX Runtime code here
#else
// Fallback or warning code here
#endif
```

### 2. Graceful Degradation

- If ONNX Runtime is not available, Silero VAD automatically falls back to Simple VAD
- No compilation errors or runtime exceptions
- Users get clear warning messages about missing dependencies

### 3. User-Controlled Setup

Users who want Silero VAD must:
1. Install Microsoft.ML.OnnxRuntime manually
2. Add `ONNX_RUNTIME_AVAILABLE` to scripting define symbols
3. Download the Silero VAD ONNX model

## Implementation Details

### SileroVad.cs Structure

```csharp
public class SileroVad : IDisposable
{
#if UNITY_EDITOR || ONNX_RUNTIME_AVAILABLE
    private InferenceSession _session;
    // ONNX-specific implementation
#endif
    
    public SileroVad(string modelPath, float threshold)
    {
#if UNITY_EDITOR || ONNX_RUNTIME_AVAILABLE
        // Initialize ONNX session
        _session = new InferenceSession(modelPath);
        _isInitialized = true;
#else
        LogUtils.Error("ONNX Runtime not available");
        _isInitialized = false;
#endif
    }
    
    public bool DetectVoiceActivity(float[] audioData, int sampleRate)
    {
#if UNITY_EDITOR || ONNX_RUNTIME_AVAILABLE
        if (_isInitialized)
        {
            // Use ONNX inference
            return ProcessWithOnnx(audioData);
        }
#endif
        // Fallback behavior
        return false;
    }
}
```

### VadManager.cs Fallback Logic

```csharp
public bool DetectVoiceActivity(float[] audioData, int sampleRate, float lastSec)
{
    switch (_vadType)
    {
        case VadType.Silero:
            if (_sileroVad?.IsInitialized == true)
            {
                return _sileroVad.DetectVoiceActivity(audioData, sampleRate);
            }
            else
            {
                // Automatic fallback to Simple VAD
                LogUtils.Warning("Falling back to Simple VAD");
                return AudioUtils.SimpleVad(audioData, sampleRate, lastSec, _simpleVadThreshold, _simpleFreqThreshold);
            }
            
        case VadType.Simple:
        default:
            return AudioUtils.SimpleVad(audioData, sampleRate, lastSec, _simpleVadThreshold, _simpleFreqThreshold);
    }
}
```

## User Setup Instructions

### Option 1: Package Manager (if available)
```
1. Open Unity Package Manager
2. Click "+" and select "Add package by name"  
3. Enter: com.unity.nuget.onnxruntime
```

### Option 2: Manual Installation
```
1. Download Microsoft.ML.OnnxRuntime from NuGet
2. Extract Unity-compatible DLLs
3. Place in project's Plugins folder
4. Add ONNX_RUNTIME_AVAILABLE to scripting define symbols
```

### Option 3: Third-party Solutions
Some Unity asset store packages or community solutions may provide ONNX Runtime integration.

## Editor Integration

The `MicrophoneRecordEditor.cs` provides:
- Visual indicators for ONNX Runtime availability
- Buttons to add scripting define symbols
- Setup instruction windows
- Model file validation

## Benefits of This Approach

1. **No Breaking Changes**: Existing Simple VAD users unaffected
2. **Optional Enhancement**: Silero VAD is an opt-in feature
3. **Clear Setup Path**: Users know exactly what they need to install
4. **Graceful Fallback**: System continues working even with missing dependencies
5. **Package Compatibility**: Follows Unity package best practices

## Testing Without ONNX Runtime

The implementation includes comprehensive tests that work without ONNX Runtime:
- Tests verify fallback behavior
- Tests check initialization states
- Tests validate error handling
- Runtime tests can be included in scenes

## Future Considerations

1. **Unity Official ONNX Support**: If Unity adds official ONNX Runtime support, we can remove conditional compilation
2. **Alternative Implementations**: Could add other VAD implementations using similar patterns
3. **WebGL Support**: May need platform-specific implementations for different Unity targets