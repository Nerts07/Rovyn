using System;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace Rovyn.UI
{
    // Fire-and-forget wrapper for button handlers: disables the button while the request runs
    // and reports failures as a toast instead of letting them disappear into the log.
    public static class UiActions
    {
        public static void Run(Func<UniTask> action, Selectable disableWhileRunning = null) =>
            RunAsync(action, disableWhileRunning).Forget();

        static async UniTaskVoid RunAsync(Func<UniTask> action, Selectable button)
        {
            if (button != null) button.interactable = false;
            try
            {
                await action();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                App.Report(FriendsStore.Describe(e));
            }
            finally
            {
                if (button != null) button.interactable = true;
            }
        }
    }
}
