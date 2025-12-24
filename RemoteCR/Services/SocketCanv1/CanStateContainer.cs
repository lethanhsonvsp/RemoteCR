using System;
using System.Collections.Generic;
using System.Linq;

namespace RemoteCR.Services.Can;

public class CanStateContainer
{
    // =====================================================
    // INTERNAL LOCK (RẤT QUAN TRỌNG)
    // =====================================================
    private readonly object _lock = new();

    // =====================================================
    // CAN FRAMES
    // =====================================================
    private readonly List<CanFrame> _rxFrames = new();
    private readonly List<CanFrame> _txFrames = new();

    public int MaxFrames { get; set; } = 200;

    // =====================================================
    // RX
    // =====================================================
    public void AddFrame(uint id, byte dlc, byte[] data)
    {
        lock (_lock)
        {
            _rxFrames.Add(new CanFrame
            {
                Id = id,
                Dlc = dlc,
                Data = data.ToArray()
            });

            if (_rxFrames.Count > MaxFrames)
                _rxFrames.RemoveAt(0);
        }

        NotifyChanged();
    }

    // =====================================================
    // TX
    // =====================================================
    public void AddTxFrame(uint id, byte[] data)
    {
        lock (_lock)
        {
            _txFrames.Add(new CanFrame
            {
                Id = id,
                Dlc = (byte)data.Length,
                Data = data.ToArray()
            });

            if (_txFrames.Count > MaxFrames)
                _txFrames.RemoveAt(0);
        }

        NotifyChanged();
    }

    // =====================================================
    // SNAPSHOT APIs (UI CHỈ ĐƯỢC ĐỌC QUA ĐÂY)
    // =====================================================
    public IReadOnlyList<CanFrame> GetRxSnapshot()
    {
        lock (_lock)
            return _rxFrames.ToList();
    }

    public IReadOnlyList<CanFrame> GetTxSnapshot()
    {
        lock (_lock)
            return _txFrames.ToList();
    }

    // =====================================================
    // CONNECTION
    // =====================================================
    public bool IsConnected { get; set; }

    // =====================================================
    // OUTPUT
    // =====================================================
    public double Voltage { get; set; }
    public double Current { get; set; }
    public double Power => Voltage * Current;

    // =====================================================
    // WIRELESS
    // =====================================================
    public int Gap { get; set; }
    public int Misalignment { get; set; }

    // =====================================================
    // TEMP
    // =====================================================
    public int PriTemp { get; set; }
    public int SecTemp { get; set; }

    // =====================================================
    // AC INPUT
    // =====================================================
    public double AcVoltage { get; set; }
    public double AcCurrent { get; set; }
    public double AcPower { get; set; }
    public double AcFreq { get; set; }

    // =====================================================
    // RF
    // =====================================================
    public double Coupling { get; set; }
    public double RfPower { get; set; }
    public int Rssi { get; set; }

    // =====================================================
    // FLAGS
    // =====================================================
    public ChargerStatus StatusFlags { get; set; }
    public ChargerFault FaultFlags { get; set; }

    // =====================================================
    // FW
    // =====================================================
    public string RevMCU1 { get; set; } = "";
    public string RevMCU2 { get; set; } = "";
    public string RevDSP { get; set; } = "";

    // =====================================================
    // GUI STATUS
    // =====================================================
    public bool GUI_WirelessComm
        => !StatusFlags.HasFlag(ChargerStatus.Comm_Fault);

    public bool GUI_StatusPower
        => StatusFlags.HasFlag(ChargerStatus.Input_OK);

    public bool GUI_PowerDerating
        => StatusFlags.HasFlag(ChargerStatus.Thermal_Derating);

    public bool GUI_Fault
        => FaultFlags != ChargerFault.None;

    public bool GUI_AlertAC
        => FaultFlags.HasFlag(ChargerFault.AC_UV)
        || FaultFlags.HasFlag(ChargerFault.AC_OV)
        || FaultFlags.HasFlag(ChargerFault.AC_Freq);

    public bool GUI_MemoryCorruption
        => StatusFlags.HasFlag(ChargerStatus.EEPROM_Fault);

    // =====================================================
    // EVENT
    // =====================================================
    public event Action? OnChange;

    public void NotifyChanged()
    {
        // ❗ chỉ phát signal, KHÔNG render
        OnChange?.Invoke();
    }
}
