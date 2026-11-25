using System.Threading;

namespace LightBulb.Services;

/// <summary>
/// Shared flag used to pause global shortcut execution while a shortcut field is recording input.
/// </summary>
public static class HotKeyCaptureState
{
    private static int _captureCount;

    public static bool IsCapturing => Volatile.Read(ref _captureCount) > 0;

    public static void Enter() => Interlocked.Increment(ref _captureCount);

    public static void Leave()
    {
        while (true)
        {
            var current = Volatile.Read(ref _captureCount);
            if (current <= 0)
                return;

            if (Interlocked.CompareExchange(ref _captureCount, current - 1, current) == current)
                return;
        }
    }
}
