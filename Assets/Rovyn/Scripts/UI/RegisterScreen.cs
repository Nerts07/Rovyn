using System;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace Rovyn.UI
{
    public class RegisterScreen : MonoBehaviour
    {
        public AppRoot root;
        public FormField displayName;
        public FormField username;
        public FormField password;
        public FormField confirmPassword;
        public BusyButton submit;
        public Button loginLink;

        bool _busy;

        void Awake()
        {
            submit.button.onClick.AddListener(Submit);
            loginLink.onClick.AddListener(() => root.ShowLogin());
            confirmPassword.input.onSubmit.AddListener(_ => Submit());
        }

        public void Open()
        {
            password.input.text = "";
            confirmPassword.input.text = "";
            foreach (var f in Fields) f.SetError(null);
            SetBusy(false);
            displayName.input.Select();
            displayName.input.ActivateInputField();
        }

        FormField[] Fields => new[] { displayName, username, password, confirmPassword };

        void Submit() => SubmitAsync().Forget();

        async UniTaskVoid SubmitAsync()
        {
            if (_busy || !Validate()) return;

            SetBusy(true);
            try
            {
                await App.Auth.SignUpAsync(username.Value, password.Value, displayName.Value);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                var message = AuthService.Describe(e, signingUp: true);
                if (App.Auth.IsSignedIn)
                {
                    // The account exists but setting the display name failed. Keep going;
                    // the name can be changed later in settings.
                    App.Report($"Account created, but the display name wasn't saved: {message}");
                }
                else
                {
                    username.SetError(message);
                    SetBusy(false);
                    return;
                }
            }
            SetBusy(false);
            await root.EnterMainAsync();
        }

        bool Validate()
        {
            foreach (var f in Fields) f.SetError(null);
            bool ok = true;

            // Display name is optional; the username is used when it's empty.
            if (!string.IsNullOrWhiteSpace(displayName.Value))
                ok &= SetIfError(displayName, AuthService.ValidateDisplayName(displayName.Value));

            if (username.CheckRequired()) ok &= SetIfError(username, AuthService.ValidateUsername(username.Value));
            else ok = false;

            if (password.CheckRequired()) ok &= SetIfError(password, AuthService.ValidatePassword(password.Value));
            else ok = false;

            if (!confirmPassword.CheckRequired()) ok = false;
            else if (confirmPassword.Value != password.Value) ok &= SetIfError(confirmPassword, "Passwords don't match");

            return ok;
        }

        static bool SetIfError(FormField field, string error)
        {
            field.SetError(error);
            return error == null;
        }

        void SetBusy(bool busy)
        {
            _busy = busy;
            submit.Busy = busy;
            foreach (var f in Fields) f.input.interactable = !busy;
            loginLink.interactable = !busy;
        }
    }
}
