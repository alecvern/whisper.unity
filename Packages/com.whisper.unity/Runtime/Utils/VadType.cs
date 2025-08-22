namespace Whisper.Utils
{
    /// <summary>
    /// Voice Activity Detection (VAD) algorithm types.
    /// </summary>
    public enum VadType
    {
        /// <summary>
        /// Simple energy-based VAD using audio energy thresholding.
        /// </summary>
        Simple = 0,
        
        /// <summary>
        /// Silero VAD using ONNX neural network model for accurate voice detection.
        /// </summary>
        Silero = 1
    }
}