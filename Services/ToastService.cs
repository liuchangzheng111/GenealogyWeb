namespace GenealogyWeb.Services;

/// <summary>全电路范围内派发右下角轻提示（成功/错误），供各页在操作完成后调用。</summary>
public sealed class ToastService
{
    public event Action<ToastNotification>? NotificationRaised;

    public void ShowSuccess(string message) =>
        Raise(new ToastNotification(message, ToastKind.Success));

    public void ShowError(string message) =>
        Raise(new ToastNotification(message, ToastKind.Danger));

    public void ShowInfo(string message) =>
        Raise(new ToastNotification(message, ToastKind.Info));

    private void Raise(ToastNotification n) => NotificationRaised?.Invoke(n);
}

public readonly record struct ToastNotification(string Message, ToastKind Kind);

public enum ToastKind
{
    Success,
    Danger,
    Info
}
