# LightBulb — Extended Edition

A customized Windows fork of [Tyrrrz/LightBulb](https://github.com/Tyrrrz/LightBulb), based on upstream [2.7.1](https://github.com/Tyrrrz/LightBulb/tree/2.7.1). LightBulb adjusts display gamma, brightness and color temperature to help reduce eyestrain.

## Extended features

- Display profiles with adjustable brightness and color temperature, shortcuts, cycling and persistent manual adjustments.
- Independent shadow, midtone and highlight adjustments, reusable tone profiles and highlight-dimming presets.
- Global keyboard shortcuts and programmable key/mouse combinations, with conflict warnings.
- Tray controls for brightness, color temperature, tone intensity and profiles.
- Touch-friendly quick-adjust window and configurable tray actions.
- Resizable settings window, dark theme, startup options and diagnostic log.
- Self-contained Windows x64 installer.

The original automatic day/night cycle, location settings and application whitelist are retained.

## Build

Requires the .NET 10 SDK and Windows.

    dotnet restore LightBulb.slnx
    dotnet build LightBulb.slnx -c Release --no-restore -p:CSharpier_Bypass=true
    dotnet test LightBulb.slnx -c Release --no-build

To create a self-contained installer, compile LightBulb-custom-setup.iss using Inno Setup 6. The script publishes the Windows x64 application and packages it locally in artifacts/Installer.

The installer retains the upstream application ID for upgrade compatibility. It is an alternative build of LightBulb, not a side-by-side installation. Review the installation choice if you already have the original application installed.

## Version

2.7.1-forked-0.20 (assembly/file version 2.7.1.20). See [fork notes](FORK-NOTES.md) for changes.

## Attribution

Original LightBulb by Oleksii Holub ([Tyrrrz](https://github.com/Tyrrrz)). Extended modifications by LwXLabs. This is an independent fork, not an official upstream release. Distributed under the original [MIT license](License.txt).