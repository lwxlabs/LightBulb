namespace LightBulb.Models;

/// <summary>
/// Action performed when the notification-area icon is left-clicked on Windows.
/// Right-click always keeps the normal tray menu behavior.
/// </summary>
public enum TrayLeftClickAction
{
    ShowAppWindow = 0,
    ShowTrayMenu = 1,
    ToggleLightBulb = 2,
    CycleProfiles = 3,
}
