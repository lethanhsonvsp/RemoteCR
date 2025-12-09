using System;
using System.Collections.Generic;

namespace RemoteCR.Services.Can;

public class CanStateContainer
{
    public bool IsConnected { get; set; }

    // ---- Output values ----
    public double Voltage { get; set; }
    public double Current { get; set; }
    public double Power => Voltage * Current;

    // ---- AC Input ----
    public double AcVoltage { get; set; }
    public double AcCurrent { get; set; }
    public double AcPower { get; set; }
    public double AcFreq { get; set; }

    // ---- Wireless charging gap + temps ----
    public int Gap { get; set; }
    public int PriTemp { get; set; }
    public int SecTemp { get; set; }

    // ---- Faults ----
    public List<string> Faults { get; set; } = new();

    // ---- Status bits from message 0x77x ----
    public HashSet<string> Status { get; set; } = new();

    // ---- Firmware versions ----
    public string RevMCU1 { get; set; } = "";
    public string RevMCU2 { get; set; } = "";
    public string RevDSP { get; set; } = "";

    // ---- Event for Blazor UI ----
    public event Action? OnChange;
    public void NotifyChanged() => OnChange?.Invoke();
}
