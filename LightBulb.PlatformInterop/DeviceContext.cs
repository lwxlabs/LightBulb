using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using LightBulb.PlatformInterop.Internal;

namespace LightBulb.PlatformInterop;

public partial class DeviceContext(nint handle) : NativeResource(handle)
{
    private int _gammaChannelOffset;

    private void SetGammaRamp(GammaRamp ramp)
    {
        if (!NativeMethods.SetDeviceGammaRamp(Handle, ref ramp))
        {
            Debug.WriteLine(
                $"Failed to set gamma ramp on device context #{Handle}). "
                    + $"Error {Marshal.GetLastWin32Error()}."
            );
        }
    }

    public void SetGamma(double redMultiplier, double greenMultiplier, double blueMultiplier)
    {
        var ramp = CreateEmptyRamp();

        // Create linear ramps for each color. Keep the original 255*255 scale so the tone-off path
        // remains byte-for-byte compatible with the proven gamma behavior of the fork.
        for (var i = 0; i < 256; i++)
        {
            ramp.Red[i] = (ushort)(i * 255 * redMultiplier);
            ramp.Green[i] = (ushort)(i * 255 * greenMultiplier);
            ramp.Blue[i] = (ushort)(i * 255 * blueMultiplier);
        }

        MakeRampUnique(ref ramp);
        SetGammaRamp(ramp);
    }

    /// <summary>
    /// Applies fully shaped per-channel curves. Values are normalized output intensities in [0,1]
    /// for each of the 256 input levels. This keeps tonal shaping inside the same GDI gamma-ramp
    /// engine instead of introducing a second renderer.
    /// </summary>
    public void SetGammaCurve(
        IReadOnlyList<double> red,
        IReadOnlyList<double> green,
        IReadOnlyList<double> blue
    )
    {
        if (red.Count != 256 || green.Count != 256 || blue.Count != 256)
            throw new ArgumentException("Gamma curves must contain exactly 256 entries per channel.");

        var ramp = CreateEmptyRamp();
        const double rampScale = 255d * 255d;

        for (var i = 0; i < 256; i++)
        {
            ramp.Red[i] = (ushort)Math.Round(Math.Clamp(red[i], 0, 1) * rampScale);
            ramp.Green[i] = (ushort)Math.Round(Math.Clamp(green[i], 0, 1) * rampScale);
            ramp.Blue[i] = (ushort)Math.Round(Math.Clamp(blue[i], 0, 1) * rampScale);
        }

        MakeRampUnique(ref ramp);
        SetGammaRamp(ramp);
    }

    private static GammaRamp CreateEmptyRamp() =>
        new()
        {
            Red = new ushort[256],
            Green = new ushort[256],
            Blue = new ushort[256],
        };

    private void MakeRampUnique(ref GammaRamp ramp)
    {
        // Some drivers ignore a ramp that is identical to the last request even when another program
        // or a display power transition changed the real hardware state in-between. Keep the existing
        // tiny last-entry perturbation for both linear and shaped curves.
        _gammaChannelOffset = ++_gammaChannelOffset % 5;
        ramp.Red[255] = (ushort)(ramp.Red[255] + _gammaChannelOffset);
        ramp.Green[255] = (ushort)(ramp.Green[255] + _gammaChannelOffset);
        ramp.Blue[255] = (ushort)(ramp.Blue[255] + _gammaChannelOffset);
    }

    public void ResetGamma() => SetGamma(1, 1, 1);

    protected override void Dispose(bool disposing)
    {
        // Don't reset gamma during dispose because this method is also called whenever
        // the device context gets invalidated.
        // Resetting gamma in such cases will cause unwanted flickering.
        if (!NativeMethods.DeleteDC(Handle))
        {
            Debug.WriteLine(
                $"Failed to dispose device context #{Handle}. "
                    + $"Error {Marshal.GetLastWin32Error()}."
            );
        }
    }
}

public partial class DeviceContext
{
    public static DeviceContext? TryCreate(string deviceName)
    {
        var handle = NativeMethods.CreateDC(deviceName, deviceName, null, 0);
        if (handle == 0)
        {
            Debug.WriteLine(
                $"Failed to retrieve device context for '{deviceName}'. "
                    + $"Error {Marshal.GetLastWin32Error()}."
            );
            return null;
        }

        return new DeviceContext(handle);
    }
}
