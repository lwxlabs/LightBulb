using System;
using LightBulb.Core;

namespace LightBulb.Models;

public sealed record ToneProfile(
    Guid Id,
    string Name,
    ToneAdjustmentConfiguration Adjustments,
    ToneBand? SelectedBand = null,
    string Tooltip = ""
);
