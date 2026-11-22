using System;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Rovyn.UI
{
    internal sealed class AvatarSettings : MonoBehaviour
    {
        Avatar _avatar;
        UnityEngine.UI.Button _upload;
        UnityEngine.UI.Button _remove;
        TMP_Text _uploadText;
        TMP_Text _removeText;
        bool _busy;
        bool _confirmRemove;

        internal static void Install(Avatar avatar, TMP_Text accountName, Sprite shape)
        {
            if (!AvatarFilePicker.IsSupported) return;

            var row = ChatWidgets.Rect(accountName.transform.parent, "Avatar Actions");
            row.gameObject.SetActive(false);
            ChatWidgets.Size(row, -1, 30);
            var layout = row.gameObject.AddComponent<UnityEngine.UI.HorizontalLayoutGroup>();
            layout.spacing = 6;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = layout.childForceExpandHeight = false;

            var settings = row.gameObject.AddComponent<AvatarSettings>();
            settings._avatar = avatar;
            settings._upload = ChatWidgets.Button(row, accountName.font, shape, "Upload avatar", 116, 30,
                () => settings.UpdateAvatarAsync(upload: true).Forget(), out var uploadText);
            settings._uploadText = uploadText;
            settings._remove = ChatWidgets.Button(row, accountName.font, shape, "Remove avatar", 116, 30,
                settings.Remove, out var removeText);
            settings._removeText = removeText;
            row.gameObject.SetActive(true);
        }

        void OnEnable()
        {
            _avatar.PhotoChanged += Refresh;
            Refresh();
        }

        void OnDisable()
        {
            _avatar.PhotoChanged -= Refresh;
            _confirmRemove = false;
        }

        void Update()
        {
            if (!_confirmRemove || Keyboard.current?.escapeKey.wasPressedThisFrame != true) return;
            _confirmRemove = false;
            Refresh();
        }

        void Refresh()
        {
            _upload.interactable = _remove.interactable = !_busy;
            _uploadText.text = _busy ? "Please wait…" : _avatar.HasPhoto ? "Change avatar" : "Upload avatar";
            _remove.gameObject.SetActive(_avatar.HasPhoto);
            _removeText.text = _confirmRemove ? "Confirm removal" : "Remove avatar";
        }

        void Remove()
        {
            if (_confirmRemove) UpdateAvatarAsync(upload: false).Forget();
            else
            {
                _confirmRemove = true;
                Refresh();
            }
        }

        async UniTaskVoid UpdateAvatarAsync(bool upload)
        {
            if (_busy || !App.SessionActive) return;
            _busy = true;
            _confirmRemove = false;
            Refresh();
            var owner = App.Auth.PlayerId;
            try
            {
                var image = upload ? await AvatarFilePicker.PickAsync() : null;
                if (this == null || (upload && image == null) || !isActiveAndEnabled || owner != App.Auth.PlayerId) return;
                await App.Avatars.SaveAsync(image);
                App.Report(upload ? "Avatar updated." : "Avatar removed.");
            }
            catch (OperationCanceledException) { }
            catch (InvalidOperationException e) { App.Report(e.Message); }
            catch (Exception e)
            {
                Debug.LogException(e);
                App.Report("Couldn't change your avatar. Try again.");
            }
            finally
            {
                if (this != null)
                {
                    _busy = false;
                    Refresh();
                }
            }
        }
    }
}
