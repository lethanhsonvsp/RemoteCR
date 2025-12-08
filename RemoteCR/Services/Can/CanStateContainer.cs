using System;
using System.Collections.Generic;

namespace RemoteCR.Services.Can;

public class CanStateContainer
{
    public double Voltage { get; set; }
    public double Current { get; set; }
    public double Power => Voltage * Current;

    public int Gap { get; set; }
    public int PriTemp { get; set; }
    public int SecTemp { get; set; }

    public List<string> Faults { get; set; } = new();

    public event Action? OnChange;

    public void NotifyChanged() => OnChange?.Invoke();
}
