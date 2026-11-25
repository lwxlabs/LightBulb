using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using LightBulb.Core;
using LightBulb.PlatformInterop;
using PowerKit;
using PowerKit.Extensions;

namespace LightBulb.Services;

public partial class GammaService : IDisposable
{
    private readonly SettingsService _settingsService;
    private readonly IDisposable _eventSubscription;

    private bool _isUpdatingGamma;

    private IReadOnlyList<DeviceContext> _deviceContexts = [];
    private bool _areDeviceContextsValid;
    private DateTimeOffset _lastGammaInvalidationTimestamp = DateTimeOffset.MinValue;

    private ColorConfiguration? _lastConfiguration;
    private ToneAdjustmentConfiguration? _lastToneAdjustments;
    private DateTimeOffset _lastUpdateTimestamp = DateTimeOffset.MinValue;

    public GammaService(SettingsService settingsService)
    {
        _settingsService = settingsService;

        // Listen to all system events that may indicate that the device context or gamma was changed from the outside
        _eventSubscription = Disposable.Merge(
            SystemHook.TryRegister(SystemHook.Ids.ForegroundWindowChanged, InvalidateGamma)
                ?? Disposable.Null,
            PowerSettingNotification.TryRegister(
                PowerSettingNotification.Ids.ConsoleDisplayStateChanged,
                InvalidateGamma
            ) ?? Disposable.Null,
            PowerSettingNotification.TryRegister(
                PowerSettingNotification.Ids.PowerSavingStatusChanged,
                InvalidateGamma
            ) ?? Disposable.Null,
            PowerSettingNotification.TryRegister(
                PowerSettingNotification.Ids.SessionDisplayStatusChanged,
                InvalidateGamma
            ) ?? Disposable.Null,
            PowerSettingNotification.TryRegister(
                PowerSettingNotification.Ids.MonitorPowerStateChanged,
                InvalidateGamma
            ) ?? Disposable.Null,
            PowerSettingNotification.TryRegister(
                PowerSettingNotification.Ids.AwayModeChanged,
                InvalidateGamma
            ) ?? Disposable.Null,
            SystemEvent.Register(SystemEvent.Ids.DisplayChanged, InvalidateDeviceContexts),
            SystemEvent.Register(SystemEvent.Ids.PaletteChanged, InvalidateDeviceContexts),
            SystemEvent.Register(SystemEvent.Ids.SettingsChanged, InvalidateDeviceContexts),
            SystemEvent.Register(SystemEvent.Ids.SystemColorsChanged, InvalidateDeviceContexts)
        );
    }

    private void EnsureValidDeviceContexts()
    {
        if (_areDeviceContextsValid)
            return;

        _areDeviceContextsValid = true;

        Disposable.Merge(_deviceContexts).Dispose();
        _deviceContexts = Monitor
            .GetAll()
            .Select(m => m.TryCreateDeviceContext())
            .WhereNotNull()
            .ToArray();

        _lastConfiguration = null;
        _lastToneAdjustments = null;
    }

    private bool IsGammaStale()
    {
        var instant = DateTimeOffset.Now;

        // Assume gamma continues to be stale for some time after it has been invalidated
        if ((instant - _lastGammaInvalidationTimestamp).Duration() <= TimeSpan.FromSeconds(0.3))
        {
            return true;
        }

        // If polling is enabled, assume gamma is stale after some time has passed since the last update
        if (
            _settingsService.IsGammaPollingEnabled
            && (instant - _lastUpdateTimestamp).Duration() > TimeSpan.FromSeconds(1)
        )
        {
            return true;
        }

        return false;
    }

    private bool IsSignificantChange(
        ColorConfiguration configuration,
        ToneAdjustmentConfiguration toneAdjustments
    )
    {
        // Nothing to compare to.
        if (
            _lastConfiguration is not { } lastConfiguration
            || _lastToneAdjustments is not { } lastToneAdjustments
        )
        {
            return true;
        }

        return Math.Abs(configuration.Temperature - lastConfiguration.Temperature) > 15
            || Math.Abs(configuration.Brightness - lastConfiguration.Brightness) > 0.01
            || toneAdjustments != lastToneAdjustments;
    }

    public void InvalidateGamma()
    {
        // Don't invalidate gamma when we're in the process of changing it ourselves,
        // to avoid an infinite loop.
        if (_isUpdatingGamma)
            return;

        _lastGammaInvalidationTimestamp = DateTimeOffset.Now;
        Debug.WriteLine("Gamma invalidated.");
    }

    public void InvalidateDeviceContexts()
    {
        _areDeviceContextsValid = false;
        Debug.WriteLine("Device contexts invalidated.");

        InvalidateGamma();
    }

    public void SetGamma(ColorConfiguration configuration) =>
        SetGamma(configuration, ToneAdjustmentConfiguration.Off);

    public void SetGamma(
        ColorConfiguration configuration,
        ToneAdjustmentConfiguration toneAdjustments
    )
    {
        // Avoid unnecessary changes as updating too often will cause stuttering.
        if (!IsGammaStale() && !IsSignificantChange(configuration, toneAdjustments))
            return;

        EnsureValidDeviceContexts();

        _isUpdatingGamma = true;
        try
        {
            if (toneAdjustments.IsOff)
            {
                // Preserve the existing linear path exactly when tonal shaping is off.
                foreach (var deviceContext in _deviceContexts)
                {
                    deviceContext.SetGamma(
                        GetRed(configuration) * configuration.Brightness,
                        GetGreen(configuration) * configuration.Brightness,
                        GetBlue(configuration) * configuration.Brightness
                    );
                }
            }
            else
            {
                var (red, green, blue) = BuildToneCurves(configuration, toneAdjustments);
                foreach (var deviceContext in _deviceContexts)
                    deviceContext.SetGammaCurve(red, green, blue);
            }
        }
        finally
        {
            _isUpdatingGamma = false;
        }

        _lastConfiguration = configuration;
        _lastToneAdjustments = toneAdjustments;
        _lastUpdateTimestamp = DateTimeOffset.Now;
        Debug.WriteLine(
            toneAdjustments.IsOff
                ? $"Updated gamma to {configuration}."
                : $"Updated gamma to {configuration} with tone shaping."
        );
    }

    private (double[] Red, double[] Green, double[] Blue) BuildToneCurves(
        ColorConfiguration configuration,
        ToneAdjustmentConfiguration toneAdjustments
    )
    {
        var red = new double[256];
        var green = new double[256];
        var blue = new double[256];

        for (var i = 0; i < 256; i++)
        {
            var input = i / 255d;
            GetToneWeights(input, out var darkWeight, out var midWeight, out var highlightWeight);

            var temperatureOffset =
                toneAdjustments.DarkTones.TemperatureOffset * toneAdjustments.DarkTones.Intensity * darkWeight
                + toneAdjustments.Midtones.TemperatureOffset * toneAdjustments.Midtones.Intensity * midWeight
                + toneAdjustments.Highlights.TemperatureOffset * toneAdjustments.Highlights.Intensity * highlightWeight;
            var brightnessOffset =
                toneAdjustments.DarkTones.BrightnessOffset * toneAdjustments.DarkTones.Intensity * darkWeight
                + toneAdjustments.Midtones.BrightnessOffset * toneAdjustments.Midtones.Intensity * midWeight
                + toneAdjustments.Highlights.BrightnessOffset * toneAdjustments.Highlights.Intensity * highlightWeight;

            var shapedConfiguration = new ColorConfiguration(
                Math.Clamp(
                    configuration.Temperature + temperatureOffset,
                    _settingsService.MinimumTemperature,
                    _settingsService.MaximumTemperature
                ),
                Math.Clamp(
                    configuration.Brightness + brightnessOffset,
                    _settingsService.MinimumBrightness,
                    _settingsService.MaximumBrightness
                )
            );

            red[i] = input * GetRed(shapedConfiguration) * shapedConfiguration.Brightness;
            green[i] = input * GetGreen(shapedConfiguration) * shapedConfiguration.Brightness;
            blue[i] = input * GetBlue(shapedConfiguration) * shapedConfiguration.Brightness;
        }

        EnsureMonotonic(red);
        EnsureMonotonic(green);
        EnsureMonotonic(blue);
        return (red, green, blue);
    }

    private static void EnsureMonotonic(double[] curve)
    {
        // SetDeviceGammaRamp is most reliable with non-decreasing channel ramps. Extreme user
        // combinations can otherwise make a brighter dark band cross a heavily dimmed mid/high
        // band. Preserve the requested shaping as far as possible while preventing reversals.
        for (var i = 1; i < curve.Length; i++)
            curve[i] = Math.Max(curve[i], curve[i - 1]);
    }

    private static void GetToneWeights(
        double input,
        out double darkWeight,
        out double midWeight,
        out double highlightWeight
    )
    {
        // Broad overlapping transitions keep the 256-entry GDI ramps smooth instead of creating
        // visible boundaries between dark tones, midtones, and highlights. The three weights always
        // sum to one, so an All tone bands adjustment remains uniform across the full curve.
        darkWeight = 1 - SmoothStep(0.18, 0.48, input);
        highlightWeight = SmoothStep(0.52, 0.82, input);
        midWeight = Math.Clamp(1 - darkWeight - highlightWeight, 0, 1);
    }

    private static double SmoothStep(double edge0, double edge1, double value)
    {
        if (edge0 >= edge1)
            return value < edge0 ? 0 : 1;

        var t = Math.Clamp((value - edge0) / (edge1 - edge0), 0, 1);
        return t * t * (3 - 2 * t);
    }

    public void Dispose()
    {
        // Reset gamma on all contexts
        foreach (var deviceContext in _deviceContexts)
            deviceContext.ResetGamma();

        _eventSubscription.Dispose();
        Disposable.Merge(_deviceContexts).Dispose();
    }
}

public partial class GammaService
{
    private static double GetRed(ColorConfiguration configuration)
    {
        // Algorithm taken from http://tannerhelland.com/4435/convert-temperature-rgb-algorithm-code

        if (configuration.Temperature > 6600)
        {
            return (
                Math.Pow(configuration.Temperature / 100 - 60, -0.1332047592) * 329.698727446 / 255
            ).Clamp(0, 1);
        }

        return 1;
    }

    private static double GetGreen(ColorConfiguration configuration)
    {
        // Algorithm taken from http://tannerhelland.com/4435/convert-temperature-rgb-algorithm-code

        if (configuration.Temperature > 6600)
        {
            return (
                Math.Pow(configuration.Temperature / 100 - 60, -0.0755148492) * 288.1221695283 / 255
            ).Clamp(0, 1);
        }

        return (
            (Math.Log(configuration.Temperature / 100) * 99.4708025861 - 161.1195681661) / 255
        ).Clamp(0, 1);
    }

    private static double GetBlue(ColorConfiguration configuration)
    {
        // Algorithm taken from http://tannerhelland.com/4435/convert-temperature-rgb-algorithm-code

        if (configuration.Temperature >= 6600)
            return 1;

        if (configuration.Temperature <= 1900)
            return 0;

        return (
            (Math.Log(configuration.Temperature / 100 - 10) * 138.5177312231 - 305.0447927307) / 255
        ).Clamp(0, 1);
    }
}
