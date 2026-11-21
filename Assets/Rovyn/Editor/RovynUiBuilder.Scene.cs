using System.Linq;
using Rovyn.UI;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Rovyn.EditorTools
{
    public static partial class RovynUiBuilder
    {
        const string InputActionsPath = "Assets/InputSystem_Actions.inputactions";

        static void BuildScene(Prefabs prefabs, ServerCatalog catalog)
        {
            BuildCamera();
            BuildEventSystem();

            var canvasRt = Kit.Rect("Rovyn UI", null);
            var canvas = canvasRt.gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasRt.gameObject.AddComponent<CanvasScaler>();
            canvasRt.gameObject.AddComponent<UiScale>();
            canvasRt.gameObject.AddComponent<GraphicRaycaster>();
            var appRoot = canvasRt.gameObject.AddComponent<AppRoot>();

            var loading = BuildLoadingScreen(canvasRt, appRoot);
            var login = BuildLoginScreen(canvasRt, appRoot);
            var register = BuildRegisterScreen(canvasRt, appRoot);
            var main = BuildMainScreen(canvasRt, prefabs, catalog);
            BuildToast(canvasRt);

            appRoot.login = login;
            appRoot.register = register;
            appRoot.main = main;

            // Only the loading screen starts visible. AppRoot switches screens at runtime.
            login.gameObject.SetActive(false);
            register.gameObject.SetActive(false);
            main.gameObject.SetActive(false);
            loading.SetActive(true);
        }

        static void BuildCamera()
        {
            var go = new GameObject("Main Camera") { tag = "MainCamera" };
            var camera = go.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Theme.Rail;
            camera.orthographic = true;
            go.AddComponent<AudioListener>();
        }

        static void BuildEventSystem()
        {
            var go = new GameObject("EventSystem", typeof(EventSystem));
            var module = go.AddComponent<InputSystemUIInputModule>();

            // Use the project's input actions so the same references are serialized in the scene.
            var asset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputActionsPath);
            if (asset == null)
            {
                module.AssignDefaultActions();
                return;
            }

            var refs = AssetDatabase.LoadAllAssetsAtPath(InputActionsPath).OfType<InputActionReference>().ToList();
            InputActionReference Find(string action) =>
                refs.FirstOrDefault(r => r.action != null && r.action.actionMap != null &&
                                         r.action.actionMap.name == "UI" && r.action.name == action);

            module.actionsAsset = asset;
            module.point = Find("Point");
            module.move = Find("Navigate");
            module.submit = Find("Submit");
            module.cancel = Find("Cancel");
            module.leftClick = Find("Click");
            module.middleClick = Find("MiddleClick");
            module.rightClick = Find("RightClick");
            module.scrollWheel = Find("ScrollWheel");
            module.trackedDevicePosition = Find("TrackedDevicePosition");
            module.trackedDeviceOrientation = Find("TrackedDeviceOrientation");
        }

        // A full-window screen with a solid background.
        static RectTransform Screen(Transform parent, string name, Color background)
        {
            var bg = Kit.Box(parent, name, background);
            Kit.Stretch(bg.rectTransform);
            return bg.rectTransform;
        }

        // ---- Loading ----

        static GameObject BuildLoadingScreen(Transform parent, AppRoot root)
        {
            var screen = Screen(parent, "Loading Screen", Theme.Main);
            var column = Kit.Rect("Column", screen);
            Kit.Center(column, 420, 220);
            Kit.VBox(column.gameObject, 16, null, TextAnchor.MiddleCenter);

            var logo = Kit.Icon(column, "Logo", "logo", 64, Theme.Brand);
            var spinner = Kit.Icon(column, "Spinner", "spinner", 28, Theme.Interactive);
            spinner.gameObject.AddComponent<Spinner>();
            var status = Kit.Label(column, "Status", "Starting Rovyn…", 16, Theme.TextNormal, FontStyles.Normal,
                TextAlignmentOptions.Center, wrap: true);
            var retry = Kit.TextButton(column, "Retry", "Retry", Theme.Accent, Color.white, 40, 120);
            retry.gameObject.SetActive(false);

            root.loadingScreen = screen.gameObject;
            root.loadingStatus = status;
            root.retryButton = retry;
            logo.rectTransform.sizeDelta = new Vector2(64, 64);
            return screen.gameObject;
        }

        // ---- Login and register ----

        // Centered form card. Returns the card's content column.
        static RectTransform FormCard(RectTransform screen, float width, string title, string subtitle)
        {
            var card = Kit.Box(screen, "Card", Theme.Main, 8);
            card.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            card.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            card.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            card.rectTransform.sizeDelta = new Vector2(width, 0);
            Kit.VBox(card.gameObject, 20, Kit.Pad(32));
            Kit.Fit(card.gameObject);

            var titleText = Kit.Label(card.transform, "Title", title, 24, Theme.TextHeader, FontStyles.Bold, TextAlignmentOptions.Center);
            Kit.Elem(titleText.gameObject, h: 30);
            var subtitleText = Kit.Label(card.transform, "Subtitle", subtitle, 15, Theme.TextMuted, FontStyles.Normal, TextAlignmentOptions.Center);
            Kit.Elem(subtitleText.gameObject, h: 20);
            return card.rectTransform;
        }

        static LoginScreen BuildLoginScreen(Transform parent, AppRoot appRoot)
        {
            var screen = Screen(parent, "Login Screen", Theme.Rail);
            var card = FormCard(screen, 480, "Welcome back!", "We're so excited to see you again!");

            var username = Kit.Form(card, "Username", "USERNAME");
            var password = Kit.Form(card, "Password", "PASSWORD", password: true);
            var submit = Kit.Busy(card, "Log In", "Log In", Theme.Accent);
            var register = Kit.TextButton(card, "Register Link", "Need an account? Register", Theme.Clear, Theme.TextLink, 24, -1, 0, 14);

            var tab = screen.gameObject.AddComponent<TabFocus>();
            tab.fields = new[] { username.input, password.input };

            var login = screen.gameObject.AddComponent<LoginScreen>();
            login.root = appRoot;
            login.username = username;
            login.password = password;
            login.submit = submit;
            login.registerLink = register;
            return login;
        }

        static RegisterScreen BuildRegisterScreen(Transform parent, AppRoot appRoot)
        {
            var screen = Screen(parent, "Register Screen", Theme.Rail);
            var card = FormCard(screen, 480, "Create an account", "Pick a name your friends will recognize.");

            var displayName = Kit.Form(card, "Display Name", "DISPLAY NAME");
            var username = Kit.Form(card, "Username", "USERNAME");
            var password = Kit.Form(card, "Password", "PASSWORD", password: true);
            var confirm = Kit.Form(card, "Confirm Password", "CONFIRM PASSWORD", password: true);
            var submit = Kit.Busy(card, "Continue", "Continue", Theme.Accent);
            var loginLink = Kit.TextButton(card, "Login Link", "Already have an account? Log In", Theme.Clear, Theme.TextLink, 24, -1, 0, 14);

            var tab = screen.gameObject.AddComponent<TabFocus>();
            tab.fields = new[] { displayName.input, username.input, password.input, confirm.input };

            var register = screen.gameObject.AddComponent<RegisterScreen>();
            register.root = appRoot;
            register.displayName = displayName;
            register.username = username;
            register.password = password;
            register.confirmPassword = confirm;
            register.submit = submit;
            register.loginLink = loginLink;
            return register;
        }

        // ---- Toast ----

        // Lives at the canvas level so errors show on the login screens too.
        static void BuildToast(Transform parent)
        {
            var holder = Kit.Rect("Toast", parent);
            Kit.Stretch(holder);

            var panel = Kit.Box(holder, "Panel", Theme.Popup, 8, raycast: true);
            Kit.Place(panel.rectTransform, new Vector2(0.5f, 0), new Vector2(0.5f, 0), 0, 0, 0, 32);
            Kit.HBox(panel.gameObject, 0, Kit.Pad(20, 20, 12, 12), TextAnchor.MiddleCenter);
            Kit.Fit(panel.gameObject, horizontal: true);
            var label = Kit.Label(panel.transform, "Message", "", 15, Theme.TextHeader, FontStyles.Normal, TextAlignmentOptions.Center);
            Kit.Elem(label.gameObject, minW: 120);
            panel.gameObject.AddComponent<CanvasGroup>();
            var fade = panel.gameObject.AddComponent<FadePanel>();
            panel.gameObject.SetActive(false);

            var toast = holder.gameObject.AddComponent<Toast>();
            toast.panel = fade;
            toast.label = label;
        }
    }
}
