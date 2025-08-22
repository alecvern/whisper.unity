using UnityEngine;
using UnityEngine.UI;
using Whisper.Utils;

namespace Whisper.Samples
{
    /// <summary>
    /// Example script demonstrating how to use different VAD types.
    /// Add this to a GameObject with MicrophoneRecord component.
    /// </summary>
    public class VadTypeDemo : MonoBehaviour
    {
        [Header("UI Components")]
        public MicrophoneRecord microphoneRecord;
        public Dropdown vadTypeDropdown;
        public Text statusText;
        public Button switchButton;
        
        private void Awake()
        {
            if (vadTypeDropdown != null)
            {
                // Setup dropdown with VAD type options
                vadTypeDropdown.options.Clear();
                vadTypeDropdown.options.Add(new Dropdown.OptionData("Simple VAD"));
                vadTypeDropdown.options.Add(new Dropdown.OptionData("Silero VAD"));
                
                // Set initial value based on current VAD type
                vadTypeDropdown.value = (int)microphoneRecord.vadType;
                vadTypeDropdown.onValueChanged.AddListener(OnVadTypeChanged);
            }
            
            if (switchButton != null)
            {
                switchButton.onClick.AddListener(OnSwitchButtonClicked);
            }
            
            UpdateStatusText();
        }
        
        private void OnVadTypeChanged(int index)
        {
            var newVadType = (VadType)index;
            microphoneRecord.vadType = newVadType;
            
            UpdateStatusText();
            
            LogUtils.Log($"VAD type changed to: {newVadType}");
        }
        
        private void OnSwitchButtonClicked()
        {
            // Toggle between Simple and Silero VAD
            var currentType = microphoneRecord.vadType;
            var newType = currentType == VadType.Simple ? VadType.Silero : VadType.Simple;
            
            microphoneRecord.vadType = newType;
            
            if (vadTypeDropdown != null)
            {
                vadTypeDropdown.value = (int)newType;
            }
            
            UpdateStatusText();
            
            LogUtils.Log($"VAD type switched from {currentType} to {newType}");
        }
        
        private void UpdateStatusText()
        {
            if (statusText == null) return;
            
            var currentType = microphoneRecord.vadType;
            var status = "";
            
            switch (currentType)
            {
                case VadType.Simple:
                    status = "Using Simple VAD (Energy-based detection)";
                    break;
                    
                case VadType.Silero:
                    status = "Using Silero VAD (Neural network-based detection)";
                    
                    // Check if Silero model is available
                    if (!SileroVadSetup.IsModelAvailable())
                    {
                        status += "\n⚠️ Silero model not found - will fallback to Simple VAD";
                    }
                    else
                    {
                        status += "\n✓ Silero model available";
                    }
                    break;
            }
            
            statusText.text = status;
        }
        
        private void Start()
        {
            // Show setup instructions if Silero model is not available
            if (!SileroVadSetup.IsModelAvailable())
            {
                LogUtils.Warning("Silero VAD model not found. To use Silero VAD:");
                LogUtils.Log(SileroVadSetup.GetSetupInstructions());
            }
        }
    }
}