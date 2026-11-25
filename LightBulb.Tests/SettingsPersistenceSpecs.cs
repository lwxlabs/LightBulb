using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Avalonia.Input;
using LightBulb.Core;
using LightBulb.Models;
using LightBulb.Services;
using Xunit;

namespace LightBulb.Tests;

public sealed class SettingsPersistenceSpecs : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "LightBulb-tests", Guid.NewGuid().ToString("N"));
    private string LegacyPath => Path.Combine(_directory, "Settings.json");
    private string DatabasePath => Path.Combine(_directory, "Settings.db3");

    public SettingsPersistenceSpecs() => Directory.CreateDirectory(_directory);

    [Fact]
    public void Fresh_settings_are_seeded_once_and_persisted_in_sqlite()
    {
        using (var settings = new SettingsService(LegacyPath))
        {
            Assert.False(settings.Load());
            Assert.Equal(4, settings.Profiles.Count);
            Assert.Equal(8, settings.ToneProfiles.Count);
            settings.Profiles = [];
            settings.ToneProfiles = [];
            Assert.True(settings.TrySaveRuntimeState());
        }
        Assert.False(File.Exists(LegacyPath));
        Assert.Equal("SQLite format 3\0", System.Text.Encoding.ASCII.GetString(File.ReadAllBytes(DatabasePath), 0, 16));
        using var reopened = new SettingsService(LegacyPath);
        Assert.True(reopened.Load());
        Assert.Empty(reopened.Profiles);
        Assert.Empty(reopened.ToneProfiles);
    }

    [Fact]
    public void Legacy_settings_are_imported_once_and_original_file_is_preserved()
    {
        const string legacy = """
            {
              // Existing files may have comments and trailing commas.
              "ManualSunriseTime": "08:15:00",
              "ManualSunsetTime": "19:45:00",
              "DayConfiguration": {"Temperature": 5500, "Brightness": 0.8},
              "ManualTemperatureOffset": 125,
              "ManualBrightnessOffset": -0.1,
              "HasSeededDefaultProfiles": true,
              "HasSeededDefaultToneProfiles": true,
              "HasSeededToneProfilesV020": true,
              "HasMigratedProfileTooltipsV020": true,
              "HasMigratedToneIntensity": true,
              "HasMigratedToneEnableV020": true,
              "Profiles": [{"Id":"23d719bd-1ba8-4923-9a29-ff6c4477e9b1","Name":"Custom","Temperature":4100,"Brightness":0.75,"HotKey":{"Key":0,"Modifiers":0},"Tooltip":"Personal profile"}],
              "ToneProfiles": [],
              "ActiveProfileId":"23d719bd-1ba8-4923-9a29-ff6c4477e9b1",
            }
            """;
        File.WriteAllText(LegacyPath, legacy);
        using (var settings = new SettingsService(LegacyPath))
        {
            Assert.True(settings.Load());
            Assert.Equal(new TimeOnly(8, 15), settings.ManualSunrise);
            Assert.Equal(new TimeOnly(19, 45), settings.ManualSunset);
            Assert.Equal(new ColorConfiguration(5500, 0.8), settings.DayConfiguration);
            Assert.Equal(125, settings.ManualTemperatureOffset);
            Assert.Equal(-0.1, settings.ManualBrightnessOffset);
            Assert.Equal("Personal profile", Assert.Single(settings.Profiles).Tooltip);
            Assert.Equal(settings.Profiles[0].Id, settings.ActiveProfileId);
            settings.ManualTemperatureOffset = 350;
            Assert.True(settings.TrySaveRuntimeState());
        }
        Assert.Equal(legacy, File.ReadAllText(LegacyPath));
        using var reopened = new SettingsService(LegacyPath);
        Assert.True(reopened.Load());
        Assert.Equal(350, reopened.ManualTemperatureOffset);
        Assert.Equal("Custom", Assert.Single(reopened.Profiles).Name);
    }

    [Fact]
    public void Profile_tone_shortcut_and_whitelist_roundtrip_and_cancel()
    {
        Guid profileId, toneId;
        using (var settings = new SettingsService(LegacyPath))
        {
            settings.Load();
            profileId = settings.Profiles[0].Id;
            toneId = settings.ToneProfiles[0].Id;
            settings.ActiveProfileId = profileId;
            settings.ActiveToneProfileId = toneId;
            settings.IsToneLayerEnabled = true;
            settings.ToneAdjustments = settings.ToneProfiles[0].Adjustments;
            settings.SelectedToneBand = ToneBand.Highlights;
            settings.ToggleHotKey = new HotKey(PhysicalKey.A, KeyModifiers.Control | KeyModifiers.Shift);
            settings.WhitelistedApplications = [new ExternalApplication(Path.Combine(_directory, "Example.exe"))];
            settings.IsApplicationWhitelistEnabled = true;
            Assert.True(settings.TrySaveRuntimeState());
            settings.ToggleHotKey = HotKey.None;
            settings.Profiles = [];
            Assert.True(settings.Load());
            Assert.Equal(profileId, settings.ActiveProfileId);
            Assert.Equal(new HotKey(PhysicalKey.A, KeyModifiers.Control | KeyModifiers.Shift), settings.ToggleHotKey);
        }
        using var reopened = new SettingsService(LegacyPath);
        Assert.True(reopened.Load());
        Assert.Equal(toneId, reopened.ActiveToneProfileId);
        Assert.True(reopened.IsToneLayerEnabled);
        Assert.Equal(ToneBand.Highlights, reopened.SelectedToneBand);
        Assert.Equal(reopened.ToneProfiles.First(x => x.Id == toneId).Adjustments, reopened.ToneAdjustments);
        Assert.True(reopened.IsApplicationWhitelistEnabled);
        Assert.Equal("Example", Assert.Single(reopened.WhitelistedApplications!).Name);
        reopened.Reset();
        Assert.Equal(HotKey.None, reopened.ToggleHotKey);
        Assert.Null(reopened.ActiveProfileId);
        Assert.False(reopened.IsToneLayerEnabled);
        Assert.Equal(4, reopened.Profiles.Count);
        Assert.True(reopened.TrySaveRuntimeState());
    }

    [Fact]
    public void Invalid_legacy_json_is_not_overwritten_or_silently_discarded()
    {
        File.WriteAllText(LegacyPath, "{broken");
        using var settings = new SettingsService(LegacyPath);
        Assert.ThrowsAny<JsonException>(() => settings.Load());
        Assert.Equal("{broken", File.ReadAllText(LegacyPath));
        File.WriteAllText(LegacyPath, "{\"ManualTemperatureOffset\":250}");
        Assert.True(settings.Load());
        Assert.Equal(250, settings.ManualTemperatureOffset);
    }

    public void Dispose() => Directory.Delete(_directory, recursive: true);
}
