using System;

namespace LightBulb.Core;

public readonly record struct ToneBandAdjustment(
    double TemperatureOffset,
    double BrightnessOffset,
    double Intensity = 1
)
{
    public static ToneBandAdjustment None { get; } = new(0, 0, 1);

    public bool IsDefault =>
        Intensity <= 0.0001
        || (Math.Abs(TemperatureOffset) < 0.01 && Math.Abs(BrightnessOffset) < 0.0001);

    public ToneBandAdjustment WithOffset(double temperatureOffset, double brightnessOffset) =>
        new(TemperatureOffset + temperatureOffset, BrightnessOffset + brightnessOffset, Intensity);

    public ToneBandAdjustment WithIntensity(double intensity) =>
        new(TemperatureOffset, BrightnessOffset, intensity);
}

public readonly record struct ToneAdjustmentConfiguration(
    ToneBandAdjustment DarkTones,
    ToneBandAdjustment Midtones,
    ToneBandAdjustment Highlights
)
{
    public static ToneAdjustmentConfiguration Off { get; } =
        new(ToneBandAdjustment.None, ToneBandAdjustment.None, ToneBandAdjustment.None);

    public bool IsOff => DarkTones.IsDefault && Midtones.IsDefault && Highlights.IsDefault;

    public ToneBandAdjustment GetBand(ToneBand band) =>
        band switch
        {
            ToneBand.DarkTones => DarkTones,
            ToneBand.Midtones => Midtones,
            ToneBand.Highlights => Highlights,
            _ => ToneBandAdjustment.None,
        };

    public ToneAdjustmentConfiguration WithBand(ToneBand band, ToneBandAdjustment value) =>
        band switch
        {
            ToneBand.DarkTones => this with { DarkTones = value },
            ToneBand.Midtones => this with { Midtones = value },
            ToneBand.Highlights => this with { Highlights = value },
            _ => this,
        };

    public ToneAdjustmentConfiguration AddToBand(
        ToneBand band,
        double temperatureOffset,
        double brightnessOffset
    ) =>
        band switch
        {
            ToneBand.DarkTones => this with
            {
                DarkTones = DarkTones.WithOffset(temperatureOffset, brightnessOffset),
            },
            ToneBand.Midtones => this with
            {
                Midtones = Midtones.WithOffset(temperatureOffset, brightnessOffset),
            },
            ToneBand.Highlights => this with
            {
                Highlights = Highlights.WithOffset(temperatureOffset, brightnessOffset),
            },
            ToneBand.AllToneBands => new ToneAdjustmentConfiguration(
                DarkTones.WithOffset(temperatureOffset, brightnessOffset),
                Midtones.WithOffset(temperatureOffset, brightnessOffset),
                Highlights.WithOffset(temperatureOffset, brightnessOffset)
            ),
            _ => this,
        };

    public ToneAdjustmentConfiguration AddIntensity(ToneBand band, double amount) =>
        band switch
        {
            ToneBand.DarkTones => this with
            {
                DarkTones = DarkTones.WithIntensity(DarkTones.Intensity + amount),
            },
            ToneBand.Midtones => this with
            {
                Midtones = Midtones.WithIntensity(Midtones.Intensity + amount),
            },
            ToneBand.Highlights => this with
            {
                Highlights = Highlights.WithIntensity(Highlights.Intensity + amount),
            },
            ToneBand.AllToneBands => new ToneAdjustmentConfiguration(
                DarkTones.WithIntensity(DarkTones.Intensity + amount),
                Midtones.WithIntensity(Midtones.Intensity + amount),
                Highlights.WithIntensity(Highlights.Intensity + amount)
            ),
            _ => this,
        };
}
