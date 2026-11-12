using System;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace TownBuilder
{
    /// <summary>
    /// Android back button (Escape on a keyboard), v0.39.0:
    /// 1. the quit dialog is open → it closes (stay);
    /// 2. a window, bar or dialog is open → its top-most Back / Close / Cancel button is pressed (one step back);
    /// 3. nothing open → the quit dialog asks "Leaving already?". Quit saves first (<see cref="BeforeQuit"/>), then closes.
    /// One per scene, on the canvas.
    /// </summary>
    public sealed class BackButtonHandler : MonoBehaviour
    {
        private static readonly string[] BackNames = { "BackButton" };
        private static readonly string[] CloseNames = { "CloseButton", "CancelButton", "DeselectButton" };

        [SerializeField] private GameObject quitRoot;
        [SerializeField] private Button stayButton;
        [SerializeField] private Button quitButton;
        [SerializeField] private Button closeButton;
        [SerializeField] private TMP_Text messageText;

        /// <summary>Called right before the game closes (the town saves here).</summary>
        public Action BeforeQuit;

        /// <summary>How the game closes; tests replace it.</summary>
        public static Action QuitApplication = Application.Quit;

        public bool QuitDialogOpen => quitRoot.activeSelf;

        private void Awake()
        {
            stayButton.onClick.AddListener(CloseQuitDialog);
            closeButton.onClick.AddListener(CloseQuitDialog);
            quitButton.onClick.AddListener(Quit);
            quitRoot.SetActive(false);
        }

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame) HandleBack();
        }

        /// <summary>One press of the back button.</summary>
        public void HandleBack()
        {
            if (QuitDialogOpen)
            {
                CloseQuitDialog();
                return;
            }

            var button = TopBackButton();
            if (button != null) button.onClick.Invoke();
            else OpenQuitDialog();
        }

        public void OpenQuitDialog() => quitRoot.SetActive(true);

        public void CloseQuitDialog() => quitRoot.SetActive(false);

        private void Quit()
        {
            CloseQuitDialog();
            BeforeQuit?.Invoke();
            QuitApplication?.Invoke();
        }

        /// <summary>
        /// The Back/Close/Cancel button of whatever is drawn on top: buttons later in the canvas hierarchy are drawn
        /// later (on top). A Back button wins over Close in the same window (the shop steps back to its categories).
        /// </summary>
        private Button TopBackButton()
        {
            var canvas = GetComponentInParent<Canvas>();
            if (canvas == null) return null;
            var candidates = canvas.GetComponentsInChildren<Button>(false)
                .Where(b => b.interactable && b.isActiveAndEnabled && !b.transform.IsChildOf(quitRoot.transform) &&
                            (BackNames.Contains(b.name) || CloseNames.Contains(b.name)) && !IsDevButton(b))
                .ToList();
            if (candidates.Count == 0) return null;
            var top = candidates[candidates.Count - 1];
            var back = candidates.LastOrDefault(b => BackNames.Contains(b.name) && b.transform.parent == top.transform.parent);
            return back != null ? back : top;
        }

        private static bool IsDevButton(Button b)
        {
            for (var t = b.transform; t != null; t = t.parent)
                if (t.name == "DevPanel") return true;
            return false;
        }
    }
}
