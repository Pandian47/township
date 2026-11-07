using System;
using UnityEngine;
using UnityEngine.UI;

namespace TownBuilder
{
    /// <summary>Reusable yes/no dialog. The root blocks input to the world while open.</summary>
    public sealed class ConfirmDialog : MonoBehaviour
    {
        [SerializeField] private GameObject dialogRoot;
        [SerializeField] private Text messageText;
        [SerializeField] private Button confirmButton;
        [SerializeField] private Text confirmLabel;
        [SerializeField] private Button cancelButton;

        private Action _onConfirm;

        private void Awake()
        {
            confirmButton.onClick.AddListener(OnConfirm);
            cancelButton.onClick.AddListener(Hide);
            Hide();
        }

        public void Show(string message, string confirmText, Action onConfirm)
        {
            messageText.text = message;
            confirmLabel.text = confirmText;
            _onConfirm = onConfirm;
            dialogRoot.SetActive(true);
        }

        public void Hide()
        {
            _onConfirm = null;
            dialogRoot.SetActive(false);
        }

        private void OnConfirm()
        {
            var action = _onConfirm;
            Hide();
            action?.Invoke();
        }
    }
}
