using System;
using Cysharp.Threading.Tasks;
using Unity.Services.Vivox;
using UnityEngine;

namespace Rovyn
{
    internal sealed class VoiceCaptureSettings
    {
        const string NoisePref = "rovyn.voice.noiseSuppression";
        const string EchoPref = "rovyn.voice.echoCancellation";
        const string AutomaticPref = "rovyn.voice.automaticSensitivity";
        const string ThresholdPref = "rovyn.voice.activationThreshold";
        const int DefaultThreshold = 1;
        internal const int MaxThreshold = 70;

        int _revision;
        bool _applying;

        internal event Action Changed;
        internal string Error { get; private set; }
        internal bool NoiseSuppression => PlayerPrefs.GetInt(NoisePref, 0) != 0;
        internal bool EchoCancellation => PlayerPrefs.GetInt(EchoPref, 0) != 0;
        internal bool AutomaticSensitivity => PlayerPrefs.GetInt(AutomaticPref, 0) != 0;
        internal int ActivationThreshold => Mathf.Clamp(PlayerPrefs.GetInt(ThresholdPref, DefaultThreshold), 0, MaxThreshold);

        static IVivoxService Service => VivoxService.Instance;
        static bool CanApply => App.SessionActive && App.Vivox.State == ConnectionState.Connected && Service.IsLoggedIn;

        internal void SetNoiseSuppression(bool value) => Save(NoisePref, value ? 1 : 0);
        internal void SetEchoCancellation(bool value) => Save(EchoPref, value ? 1 : 0);
        internal void SetAutomaticSensitivity(bool value) => Save(AutomaticPref, value ? 1 : 0);
        internal void SetActivationThreshold(int value) => Save(ThresholdPref, Mathf.Clamp(value, 0, MaxThreshold));

        void Save(string key, int value)
        {
            PlayerPrefs.SetInt(key, value);
            Error = null;
            QueueApply();
            Changed?.Invoke();
        }

        internal void QueueApply()
        {
            _revision++;
            if (!_applying && CanApply) ApplyLatestAsync().Forget();
        }

        async UniTaskVoid ApplyLatestAsync()
        {
            _applying = true;
            try
            {
                while (CanApply)
                {
                    int revision = _revision;
                    // Wait for the slider or toggle to settle before sending VAD requests.
                    await UniTask.Delay(160, ignoreTimeScale: true);
                    if (revision != _revision) continue;
                    if (!CanApply) break;

                    string error = ApplyNoiseAndEcho();
                    try
                    {
                        if (AutomaticSensitivity)
                        {
                            await Service.EnableAutoVoiceActivityDetectionAsync();
                        }
                        else
                        {
                            await Service.DisableAutoVoiceActivityDetectionAsync();
                            if (revision != _revision || !CanApply) continue;
                            await Service.SetVoiceActivityDetectionPropertiesAsync(sensitivity: ActivationThreshold);
                        }
                    }
                    catch (Exception exception)
                    {
                        Debug.LogWarning($"Couldn't apply voice activation settings: {exception.Message}");
                        error = error == null ? "Couldn't apply input sensitivity." : "Couldn't apply microphone settings.";
                    }

                    if (revision != _revision) continue;
                    if (CanApply) Error = error;
                    break;
                }
            }
            finally
            {
                _applying = false;
                Changed?.Invoke();
            }
        }

        string ApplyNoiseAndEcho()
        {
            try
            {
                var settings = Service.VivoxGlobalAudioSettings;
                if (settings == null) return "Audio processing is unavailable.";

                bool noise = NoiseSuppression;
                bool echo = EchoCancellation;
                if (settings.NoiseSuppressionEnabled != noise) settings.NoiseSuppressionEnabled = noise;
                if (settings.VivoxAcousticEchoCancellationEnabled != echo)
                    settings.VivoxAcousticEchoCancellationEnabled = echo;
                if (Application.platform is RuntimePlatform.Android or RuntimePlatform.IPhonePlayer)
                    settings.PlatformAcousticEchoCancellationEnabled = echo;

                return settings.NoiseSuppressionEnabled == noise && settings.VivoxAcousticEchoCancellationEnabled == echo
                    ? null : "Couldn't apply noise suppression or echo cancellation.";
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"Couldn't apply noise suppression or echo cancellation: {exception.Message}");
                return "Couldn't apply noise suppression or echo cancellation.";
            }
        }
    }
}
