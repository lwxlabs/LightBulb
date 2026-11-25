using System;

namespace LightBulb.Models;

public sealed record DisplayProfile(
    Guid Id,
    string Name,
    double Temperature,
    double Brightness,
    HotKey HotKey,
    string Tooltip = ""
);
