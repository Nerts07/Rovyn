using System;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace Rovyn.UI
{
    public class LoginScreen : MonoBehaviour
    {
        public AppRoot root;
        public FormField username;
        public FormField password;
        public BusyButton submit;
        public Button registerLink;

        bool _busy;

        void Awake()
        {
            submit.button.onClick.AddListener(Submit);
            registerLink.onClick.AddListener(() => root.ShowRegister());
            username.input.onSubmit.AddListener(_ => Submit());
            password.input.onSubmit.AddListener(_ => Submit());
        }

        public void Open()
        {
            password.input.text = "";
            username.SetError(null);
            password.SetError(null);
            SetBusy(false);
            username.input.Select();
            username.input.ActivateInputField();
        }

        public void ShowError(string message)
        {
            username.SetError(message);
            password.SetError(message);
        }

        void Submit() => SubmitAsync().Forget();

        async UniTaskVoid SubmitAsync()
        {
            if (_busy) return;
            username.SetError(null);
            password.SetError(null);
            bool filled = username.CheckRequired() & password.CheckRequired();
            if (!filled) return;

            SetBusy(true);
            try
            {
                await App.Auth.SignInAsync(username.Value, password.Value);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                ShowError(AuthService.Describe(e));
                SetBusy(false);
                return;
            }
            SetBusy(false);
            await root.EnterMainAsync();
        }

        void SetBusy(bool busy)
        {
            _busy = busy;
            submit.Busy = busy;
            username.input.interactable = !busy;
            password.input.interactable = !busy;
            registerLink.interactable = !busy;
        }
    }
}
