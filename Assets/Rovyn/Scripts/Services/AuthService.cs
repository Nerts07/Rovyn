using System;
using System.Linq;
using Cysharp.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.Core;

namespace Rovyn
{
    // Username/password accounts through UGS Authentication.
    // UGS has no email or password-recovery flow for this provider, so neither exists here.
    public sealed class AuthService
    {
        // Raised when the player is signed out for any reason (logout or an expired session).
        public event Action SignedOut;
        public event Action Expired;
        public event Action NameChanged;

        static IAuthenticationService Service => AuthenticationService.Instance;

        public bool IsSignedIn => Service.IsSignedIn;
        public bool HasSavedSession => Service.SessionTokenExists;
        public string PlayerId => Service.PlayerId;

        // UGS player names look like "Name#1234". The suffix keeps names unique.
        public string FullName => Service.PlayerName ?? "";
        public string DisplayName => Names.Display(FullName);

        bool _subscribed;

        public async UniTask InitializeAsync()
        {
            if (UnityServices.State != ServicesInitializationState.Initialized)
                await UnityServices.InitializeAsync();

            if (_subscribed) return;
            _subscribed = true;
            Service.SignedOut += () => SignedOut?.Invoke();
            Service.Expired += () => Expired?.Invoke();
            Service.PlayerNameChanged += _ => NameChanged?.Invoke();
        }

        // Signs back in with the session token saved by a previous login.
        // Returns false when there is no usable session and the user has to log in.
        public async UniTask<bool> TryRestoreSessionAsync()
        {
            if (Service.IsSignedIn) return true;
            if (!Service.SessionTokenExists) return false;

            try
            {
                // With a stored session token this signs back into that same account,
                // whatever provider it was created with. It does not create a new anonymous player.
                await Service.SignInAnonymouslyAsync();
            }
            catch (AuthenticationException e) when (e.ErrorCode == AuthenticationErrorCodes.InvalidSessionToken)
            {
                Service.ClearSessionToken();
                return false;
            }

            await LoadNameAsync();
            return true;
        }

        public async UniTask SignInAsync(string username, string password)
        {
            await Service.SignInWithUsernamePasswordAsync(username.Trim(), ServerPassword(username, password));
            await LoadNameAsync();
        }

        public async UniTask SignUpAsync(string username, string password, string displayName)
        {
            await Service.SignUpWithUsernamePasswordAsync(username.Trim(), ServerPassword(username, password));
            var name = string.IsNullOrWhiteSpace(displayName) ? username : displayName;
            await Service.UpdatePlayerNameAsync(name.Trim());
        }

        public async UniTask SetDisplayNameAsync(string displayName)
        {
            await Service.UpdatePlayerNameAsync(displayName.Trim());
        }

        public async UniTask ChangePasswordAsync(string current, string next)
        {
            var username = Service.PlayerInfo?.Username ?? "";
            await Service.UpdatePasswordAsync(ServerPassword(username, current), ServerPassword(username, next));
        }

        // UGS only accepts 8-30 characters with upper, lower, digit and symbol. Players can type any
        // password, so the server gets a derived one that always meets those rules.
        static string ServerPassword(string username, string password)
        {
            using var sha = System.Security.Cryptography.SHA256.Create();
            var hash = sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes($"rovyn:{username.Trim().ToLowerInvariant()}:{password}"));
            var text = Convert.ToBase64String(hash).Replace('+', 'a').Replace('/', 'b').TrimEnd('=');
            return "Rv1!" + text.Substring(0, 26);
        }

        // clearCredentials removes the saved session token, so the next launch shows the login screen.
        public void SignOut() => Service.SignOut(true);

        async UniTask LoadNameAsync()
        {
            if (string.IsNullOrEmpty(Service.PlayerName))
                await Service.GetPlayerNameAsync();
        }

        // ---- Validation (rules from the UGS username/password docs) ----

        public static string ValidateUsername(string username)
        {
            username = username?.Trim() ?? "";
            if (username.Length < 3 || username.Length > 20)
                return "Username must be 3-20 characters.";
            if (!username.All(c => (char.IsLetterOrDigit(c) && c < 128) || c == '.' || c == '-' || c == '@' || c == '_'))
                return "Use only letters, numbers and . - @ _";
            return null;
        }

        public static string ValidatePassword(string password)
        {
            password ??= "";
            return password.Length == 0 ? "Enter a password." : null;
        }

        public static string ValidateDisplayName(string name)
        {
            name = name?.Trim() ?? "";
            if (name.Length < 2 || name.Length > 32)
                return "Display name must be 2-32 characters.";
            if (name.Any(char.IsWhiteSpace) || name.Contains('#'))
                return "Display names can't contain spaces or #.";
            return null;
        }

        // Turns service exceptions into short messages for the UI.
        public static string Describe(Exception e, bool signingUp = false)
        {
            if (e is RequestFailedException r)
            {
                if (r.ErrorCode == AuthenticationErrorCodes.BannedUser) return "This account has been banned.";
                if (signingUp && (r.ErrorCode == AuthenticationErrorCodes.AccountAlreadyLinked || r.ErrorCode == CommonErrorCodes.Conflict))
                    return "That username is already taken.";

                switch (r.ErrorCode)
                {
                    case CommonErrorCodes.TransportError:
                    case CommonErrorCodes.Timeout:
                    case CommonErrorCodes.ServiceUnavailable:
                        return "Can't reach the server. Check your connection and try again.";
                    case CommonErrorCodes.TooManyRequests:
                        return "Too many attempts. Wait a moment and try again.";
                    case CommonErrorCodes.ProjectPolicyAccessDenied:
                    case CommonErrorCodes.PlayerPolicyAccessDenied:
                        return "Access denied by the project's access policy.";
                }

                if (!signingUp && (r.ErrorCode == AuthenticationErrorCodes.InvalidParameters
                    || r.ErrorCode == CommonErrorCodes.InvalidToken
                    || r.ErrorCode == CommonErrorCodes.InvalidRequest
                    || r.ErrorCode == CommonErrorCodes.NotFound
                    || r.ErrorCode == CommonErrorCodes.Forbidden))
                    return "Login or password is invalid.";

                if (r.ErrorCode == AuthenticationErrorCodes.InvalidParameters)
                    return $"The server rejected that: {r.Message}";
            }
            return e.Message;
        }
    }
}
