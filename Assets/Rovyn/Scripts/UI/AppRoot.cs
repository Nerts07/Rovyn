using System;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Rovyn.UI
{
    // Boots the services and switches between the loading, login, register and main screens.
    public class AppRoot : MonoBehaviour
    {
        public GameObject loadingScreen;
        public TMP_Text loadingStatus;
        public Button retryButton;
        public LoginScreen login;
        public RegisterScreen register;
        public MainScreen main;

        bool _quitting;

        void Awake()
        {
            UiPresentation.ApplyPalette(gameObject);
            var loadingLogo = loadingStatus.transform.parent.Find("Logo");
            if (loadingLogo != null && loadingLogo.TryGetComponent<UnityEngine.UI.Image>(out var logo))
                logo.color = Theme.Brand;

            var composerImage = (UnityEngine.UI.Image)main.chatPage.composer.targetGraphic;
            UiPresentation.AddScrollbars(gameObject, composerImage.sprite);
            retryButton.onClick.AddListener(() => BootAsync().Forget());
            Application.wantsToQuit += OnWantsToQuit;
        }

        void Start() => BootAsync().Forget();

        void OnDestroy()
        {
            Application.wantsToQuit -= OnWantsToQuit;
            App.Auth.SignedOut -= OnSignedOut;
            App.Auth.Expired -= OnExpired;
        }

        async UniTaskVoid BootAsync()
        {
            ShowLoading("Starting Rovyn…");
            try
            {
                await App.InitializeAsync();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                ShowLoading("Couldn't start services. Check your connection and the project's Unity Services link.", retry: true);
                return;
            }

            App.Auth.SignedOut -= OnSignedOut;
            App.Auth.Expired -= OnExpired;
            App.Auth.SignedOut += OnSignedOut;
            App.Auth.Expired += OnExpired;

            await RestoreOrLoginAsync(null);
        }

        async UniTask RestoreOrLoginAsync(string messageIfFailed)
        {
            if (App.Auth.HasSavedSession)
            {
                ShowLoading("Signing in…");
                try
                {
                    if (await App.Auth.TryRestoreSessionAsync())
                    {
                        await EnterMainAsync();
                        return;
                    }
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                    messageIfFailed = AuthService.Describe(e);
                }
            }
            ShowLogin();
            if (messageIfFailed != null) login.ShowError(messageIfFailed);
        }

        public async UniTask EnterMainAsync()
        {
            ShowLoading("Connecting…");
            await App.StartSessionAsync();
            SetScreen(main.gameObject);
            main.Open();
        }

        public void ShowLogin()
        {
            SetScreen(login.gameObject);
            login.Open();
        }

        public void ShowRegister()
        {
            SetScreen(register.gameObject);
            register.Open();
        }

        void ShowLoading(string status, bool retry = false)
        {
            SetScreen(loadingScreen);
            loadingStatus.text = status;
            retryButton.gameObject.SetActive(retry);
        }

        void SetScreen(GameObject screen)
        {
            loadingScreen.SetActive(screen == loadingScreen);
            login.gameObject.SetActive(screen == login.gameObject);
            register.gameObject.SetActive(screen == register.gameObject);
            main.gameObject.SetActive(screen == main.gameObject);
        }

        void OnSignedOut()
        {
            if (_quitting) return;
            HandleSignedOutAsync().Forget();
        }

        async UniTaskVoid HandleSignedOutAsync()
        {
            await App.EndSessionAsync();
            main.Close();
            ShowLogin();
        }

        // The access token couldn't be refreshed. Try the saved session once before asking to log in.
        void OnExpired()
        {
            if (_quitting) return;
            ExpiredAsync().Forget();
        }

        async UniTaskVoid ExpiredAsync()
        {
            await App.EndSessionAsync();
            main.Close();
            await RestoreOrLoginAsync("Your session expired. Please log in again.");
        }

        // Give presence a moment to switch to offline before the window closes.
        bool OnWantsToQuit()
        {
            if (_quitting || !App.SessionActive) return true;
            _quitting = true;
            QuitAsync().Forget();
            return false;
        }

        async UniTaskVoid QuitAsync()
        {
            try { await App.EndSessionAsync().Timeout(TimeSpan.FromSeconds(3)); }
            catch (Exception e) { Debug.LogWarning($"Shutdown: {e.Message}"); }
            Application.Quit();
        }
    }
}
