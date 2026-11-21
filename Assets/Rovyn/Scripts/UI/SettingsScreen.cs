using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Rovyn.UI
{
    // Full-screen settings: account, voice & audio, appearance, and log out.
    public class SettingsScreen : MonoBehaviour
    {
        public enum Page { Account, Voice, Appearance }

        public FadePanel panel;
        public Button closeButton;
        public Button[] pageButtons;            // Account, Voice, Appearance
        public HoverHighlight[] pageHighlights;
        public GameObject[] pages;
        public Button logoutButton;

        [Header("My Account")]
        public Avatar avatar;
        public TMP_Text fullName;
        public TMP_Text playerId;
        public Button copyIdButton;
        public FormField displayName;
        public BusyButton saveName;
        public FormField currentPassword;
        public FormField newPassword;
        public BusyButton savePassword;

        [Header("Voice & Audio")]
        public TMP_Dropdown inputDevice;
        public TMP_Dropdown outputDevice;
        public Slider inputVolume;
        public TMP_Text inputVolumeValue;
        public Slider outputVolume;
        public TMP_Text outputVolumeValue;
        public BusyButton micTest;
        public Image micTestIndicator;
        public TMP_Text voiceNotice;

        [Header("Appearance")]
        public Slider zoom;
        public TMP_Text zoomValue;

        bool _binding;
        readonly ParticipantWatcher _micWatcher = new();

        void Awake()
        {
            closeButton.onClick.AddListener(Close);
            for (int i = 0; i < pageButtons.Length; i++)
            {
                var page = (Page)i;
                pageButtons[i].onClick.AddListener(() => ShowPage(page));
            }
            logoutButton.onClick.AddListener(() => UiActions.Run(async () =>
            {
                Close();
                await App.SignOutAsync();
            }, logoutButton));

            copyIdButton.onClick.AddListener(() =>
            {
                GUIUtility.systemCopyBuffer = App.Auth.PlayerId;
                App.Report("Player ID copied.");
            });
            saveName.button.onClick.AddListener(() => SaveNameAsync().Forget());
            savePassword.button.onClick.AddListener(() => SavePasswordAsync().Forget());

            inputDevice.onValueChanged.AddListener(OnInputDevice);
            outputDevice.onValueChanged.AddListener(OnOutputDevice);
            inputVolume.onValueChanged.AddListener(v => { inputVolumeValue.text = $"{v:0}%"; if (!_binding) App.Voice.SetInputVolume(PercentToVivox(v)); });
            outputVolume.onValueChanged.AddListener(v => { outputVolumeValue.text = $"{v:0}%"; if (!_binding) App.Voice.SetOutputVolume(PercentToVivox(v)); });
            micTest.button.onClick.AddListener(() => ToggleMicTestAsync().Forget());

            zoom.onValueChanged.AddListener(v => { zoomValue.text = $"{v:0}%"; });
            // Apply zoom on release, not while dragging, so the slider doesn't move under the cursor.
            var trigger = zoom.gameObject.AddComponent<UnityEngine.EventSystems.EventTrigger>();
            var entry = new UnityEngine.EventSystems.EventTrigger.Entry { eventID = UnityEngine.EventSystems.EventTriggerType.PointerUp };
            entry.callback.AddListener(_ => UiScale.Zoom = zoom.value / 100f);
            trigger.triggers.Add(entry);

            pageButtons[0].transform.parent.GetComponent<UnityEngine.UI.LayoutElement>().flexibleWidth = 0;
            var shape = inputDevice.GetComponent<UnityEngine.UI.Image>().sprite;
            VoiceProcessingView.Install((RectTransform)pages[(int)Page.Voice].transform, inputVolume,
                inputVolumeValue.font, shape);
        }

        void OnEnable()
        {
            App.Voice.DevicesChanged += BindVoice;
            App.Voice.StateChanged += BindVoice;
            App.Vivox.StateChanged += BindVoice;
            App.Auth.NameChanged += BindAccount;
        }

        void OnDisable()
        {
            App.Voice.DevicesChanged -= BindVoice;
            App.Voice.StateChanged -= BindVoice;
            App.Vivox.StateChanged -= BindVoice;
            App.Auth.NameChanged -= BindAccount;
            _micWatcher.Pause();
        }

        void Update()
        {
            if (panel.IsOpen && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) Close();
        }

        public void Open(Page page = Page.Account)
        {
            panel.Show();
            BindAccount();
            BindVoice();
            _binding = true;
            zoom.value = UiScale.Zoom * 100f;
            zoomValue.text = $"{zoom.value:0}%";
            _binding = false;
            ShowPage(page);
        }

        public void Close()
        {
            if (!panel.IsOpen) return;
            App.Voice.StopMicTestAsync().Forget();
            panel.Hide();
        }

        void ShowPage(Page page)
        {
            for (int i = 0; i < pages.Length; i++)
            {
                pages[i].SetActive(i == (int)page);
                pageHighlights[i].Selected = i == (int)page;
            }
        }

        // ---- Account ----

        void BindAccount()
        {
            if (!panel.IsOpen) return;
            avatar.Set(App.Auth.PlayerId, App.Auth.DisplayName);
            avatar.SetStatus(App.Friends.MyStatus);
            fullName.text = App.Auth.FullName;
            playerId.text = App.Auth.PlayerId;
            displayName.input.text = App.Auth.DisplayName;
            displayName.SetError(null);
            currentPassword.input.text = "";
            newPassword.input.text = "";
            currentPassword.SetError(null);
            newPassword.SetError(null);
        }

        async UniTaskVoid SaveNameAsync()
        {
            var error = AuthService.ValidateDisplayName(displayName.Value);
            displayName.SetError(error);
            if (error != null) return;

            saveName.Busy = true;
            try
            {
                await App.Auth.SetDisplayNameAsync(displayName.Value);
                App.Report("Display name updated.");
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                displayName.SetError(AuthService.Describe(e, signingUp: true));
            }
            finally { saveName.Busy = false; }
        }

        async UniTaskVoid SavePasswordAsync()
        {
            currentPassword.SetError(null);
            newPassword.SetError(null);
            if (!currentPassword.CheckRequired()) return;
            var error = AuthService.ValidatePassword(newPassword.Value);
            newPassword.SetError(error);
            if (error != null) return;

            savePassword.Busy = true;
            try
            {
                await App.Auth.ChangePasswordAsync(currentPassword.Value, newPassword.Value);
                currentPassword.input.text = "";
                newPassword.input.text = "";
                App.Report("Password changed.");
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                currentPassword.SetError(AuthService.Describe(e));
            }
            finally { savePassword.Busy = false; }
        }

        // ---- Voice & Audio ----

        void BindVoice()
        {
            if (!panel.IsOpen) return;
            _binding = true;

            bool available = App.Vivox.IsLoggedIn;
            voiceNotice.gameObject.SetActive(!available);
            voiceNotice.text = "Voice is not connected. Device settings will load once it is.";

            FillDropdown(inputDevice, App.Voice.InputDevices.Select(d => d.DeviceName).ToList(),
                App.Voice.InputDevices.ToList().FindIndex(d => d.DeviceID == App.Voice.ActiveInput?.DeviceID));
            FillDropdown(outputDevice, App.Voice.OutputDevices.Select(d => d.DeviceName).ToList(),
                App.Voice.OutputDevices.ToList().FindIndex(d => d.DeviceID == App.Voice.ActiveOutput?.DeviceID));
            inputDevice.interactable = available;
            outputDevice.interactable = available;

            inputVolume.value = VivoxToPercent(App.Voice.InputVolume);
            outputVolume.value = VivoxToPercent(App.Voice.OutputVolume);
            inputVolumeValue.text = $"{inputVolume.value:0}%";
            outputVolumeValue.text = $"{outputVolume.value:0}%";

            // The echo test can't run while connected to a voice channel.
            micTest.button.interactable = available && App.Voice.Channel == null;
            micTest.label.text = App.Voice.IsMicTesting ? "Stop Testing" : "Let's Check";
            var self = App.Voice.MicTestSelf;
            if (self != null) _micWatcher.Watch(self, UpdateMicIndicator);
            else { _micWatcher.Watch(null, null); micTestIndicator.color = Theme.Divider; }

            _binding = false;
        }

        static void FillDropdown(TMP_Dropdown dropdown, List<string> names, int selected)
        {
            dropdown.ClearOptions();
            dropdown.AddOptions(names.Count > 0 ? names : new List<string> { "No devices" });
            dropdown.SetValueWithoutNotify(Mathf.Max(0, selected));
        }

        void OnInputDevice(int index)
        {
            if (_binding || index >= App.Voice.InputDevices.Count) return;
            var device = App.Voice.InputDevices[index];
            UiActions.Run(() => App.Voice.SetInputDeviceAsync(device), inputDevice);
        }

        void OnOutputDevice(int index)
        {
            if (_binding || index >= App.Voice.OutputDevices.Count) return;
            var device = App.Voice.OutputDevices[index];
            UiActions.Run(() => App.Voice.SetOutputDeviceAsync(device), outputDevice);
        }

        async UniTaskVoid ToggleMicTestAsync()
        {
            micTest.Busy = true;
            try
            {
                if (App.Voice.IsMicTesting) await App.Voice.StopMicTestAsync();
                else await App.Voice.StartMicTestAsync();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                App.Report("Mic test failed to start.");
            }
            finally
            {
                micTest.Busy = false;
                BindVoice();
            }
        }

        void UpdateMicIndicator()
        {
            var self = _micWatcher.Participant;
            micTestIndicator.color = self != null && self.SpeechDetected ? Theme.Green : Theme.Divider;
        }

        // Vivox volume is -50..50 (0 = default). The sliders show 0..200 %.
        static float VivoxToPercent(int value) => (value + 50) * 2f;
        static int PercentToVivox(float percent) => Mathf.RoundToInt(percent / 2f) - 50;
    }
}
