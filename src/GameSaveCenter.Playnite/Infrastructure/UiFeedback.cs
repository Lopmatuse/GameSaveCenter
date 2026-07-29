using System;
using System.Threading.Tasks;

namespace GameSaveCenter.Playnite.Infrastructure
{
    public enum UiNotificationKind
    {
        Information,
        Success,
        Warning,
        Error
    }

    public sealed class UiNotificationEventArgs : EventArgs
    {
        public UiNotificationEventArgs(string title, string message, UiNotificationKind kind)
        {
            Title = title ?? string.Empty;
            Message = message ?? string.Empty;
            Kind = kind;
        }

        public string Title { get; }
        public string Message { get; }
        public UiNotificationKind Kind { get; }
        public bool Handled { get; set; }
    }

    public sealed class UiConfirmationEventArgs : EventArgs
    {
        public UiConfirmationEventArgs(string title, string message, string confirmText, string cancelText, bool isDangerous)
        {
            Title = title ?? string.Empty;
            Message = message ?? string.Empty;
            ConfirmText = string.IsNullOrWhiteSpace(confirmText) ? "确认" : confirmText;
            CancelText = string.IsNullOrWhiteSpace(cancelText) ? "取消" : cancelText;
            IsDangerous = isDangerous;
            Completion = new TaskCompletionSource<bool>();
        }

        public string Title { get; }
        public string Message { get; }
        public string ConfirmText { get; }
        public string CancelText { get; }
        public bool IsDangerous { get; }
        public bool Handled { get; set; }
        public TaskCompletionSource<bool> Completion { get; }
    }
}
