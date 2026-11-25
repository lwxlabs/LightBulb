using FluentAssertions;
using Xunit;

namespace LightBulb.Core.Tests;

public class ToneAdjustmentConfigurationSpecs
{
    [Fact]
    public void Tone_band_color_and_brightness_adjustments_preserve_intensity()
    {
        var configuration = new ToneAdjustmentConfiguration(
            new ToneBandAdjustment(-200, -0.03, 0.4),
            ToneBandAdjustment.None,
            ToneBandAdjustment.None
        );

        var adjusted = configuration.AddToBand(ToneBand.DarkTones, -100, -0.02);

        adjusted.DarkTones.TemperatureOffset.Should().Be(-300);
        adjusted.DarkTones.BrightnessOffset.Should().Be(-0.05);
        adjusted.DarkTones.Intensity.Should().Be(0.4);
        adjusted.Midtones.Should().Be(configuration.Midtones);
        adjusted.Highlights.Should().Be(configuration.Highlights);
    }

    [Fact]
    public void Tone_band_intensity_adjustments_do_not_change_color_or_brightness()
    {
        var configuration = new ToneAdjustmentConfiguration(
            ToneBandAdjustment.None,
            new ToneBandAdjustment(-400, -0.08, 0.5),
            ToneBandAdjustment.None
        );

        var adjusted = configuration.AddIntensity(ToneBand.Midtones, 0.1);

        adjusted.Midtones.TemperatureOffset.Should().Be(-400);
        adjusted.Midtones.BrightnessOffset.Should().Be(-0.08);
        adjusted.Midtones.Intensity.Should().BeApproximately(0.6, 0.000001);
        adjusted.DarkTones.Should().Be(configuration.DarkTones);
        adjusted.Highlights.Should().Be(configuration.Highlights);
    }

    [Fact]
    public void All_tone_band_intensity_adjustment_changes_every_band_without_changing_offsets()
    {
        var configuration = new ToneAdjustmentConfiguration(
            new ToneBandAdjustment(-100, -0.01, 0.2),
            new ToneBandAdjustment(-300, -0.05, 0.4),
            new ToneBandAdjustment(-900, -0.15, 0.8)
        );

        var adjusted = configuration.AddIntensity(ToneBand.AllToneBands, -0.1);

        adjusted.DarkTones.TemperatureOffset.Should().Be(-100);
        adjusted.DarkTones.BrightnessOffset.Should().Be(-0.01);
        adjusted.DarkTones.Intensity.Should().BeApproximately(0.1, 0.000001);
        adjusted.Midtones.TemperatureOffset.Should().Be(-300);
        adjusted.Midtones.BrightnessOffset.Should().Be(-0.05);
        adjusted.Midtones.Intensity.Should().BeApproximately(0.3, 0.000001);
        adjusted.Highlights.TemperatureOffset.Should().Be(-900);
        adjusted.Highlights.BrightnessOffset.Should().Be(-0.15);
        adjusted.Highlights.Intensity.Should().BeApproximately(0.7, 0.000001);
    }

    [Fact]
    public void Zero_intensity_makes_a_band_effectively_off_without_discarding_its_offsets()
    {
        var configuration = new ToneAdjustmentConfiguration(
            ToneBandAdjustment.None,
            ToneBandAdjustment.None,
            new ToneBandAdjustment(-1000, -0.2, 0)
        );

        configuration.IsOff.Should().BeTrue();
        configuration.Highlights.TemperatureOffset.Should().Be(-1000);
        configuration.Highlights.BrightnessOffset.Should().Be(-0.2);
    }
}
