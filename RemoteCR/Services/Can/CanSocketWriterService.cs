using System;
using System.Threading;
using RemoteCR.Services.Can;

namespace RemoteCR.Services.Can;

public sealed class CanSocketWriterService : IDisposable
{
    private readonly SocketCan _can;
    private readonly Timer _timer;
    private readonly object _lock = new();

    private readonly ControlModuleCommand _cmd = new();

    private TxState _state = TxState.Idle;

    private enum TxState
    {
        Idle,       // chưa gửi gì
        Active,     // gửi 0x191 định kỳ (có thể ON hoặc OFF tùy _cmd)
        Stopping    // đang trong giai đoạn gửi frame OFF cuối trước khi dừng
    }

    public CanSocketWriterService(SocketCan can)
    {
        _can = can;

        _timer = new Timer(_ => OnTick(), null, Timeout.Infinite, Timeout.Infinite);
    }

    /* ============================================================
     * PUBLIC API
     * ============================================================ */

    public void Update(Action<ControlModuleCommand> update)
    {
        lock (_lock)
        {
            update(_cmd);
        }
    }

    /// <summary>
    /// Bắt đầu gửi định kỳ 100ms
    /// </summary>
    public void StartTx()
    {
        if (!_can.IsConnected) return;

        lock (_lock)
        {
            _state = TxState.Active;
        }

        _timer.Change(0, 100);
    }

    /// <summary>
    /// Dừng gửi – sẽ tự động gửi vài frame OFF trước khi dừng hẳn
    /// </summary>
    public void StopTx()
    {
        lock (_lock)
        {
            if (_state == TxState.Active)
                _state = TxState.Stopping;
        }
    }

    public bool IsTxActive => _state == TxState.Active;

    /* ============================================================
     * CORE LOOP
     * ============================================================ */

    private void OnTick()
    {
        if (!_can.IsConnected)
        {
            _state = TxState.Idle;
            _timer.Change(Timeout.Infinite, Timeout.Infinite);
            return;
        }

        switch (_state)
        {
            case TxState.Idle:
                return;

            case TxState.Active:
                SendCurrentCommand();
                break;

            case TxState.Stopping:
                SendCurrentCommand(); // vẫn dùng hàm chung – lúc này _cmd đã là OFF
                // Sau ~500ms (5 frame) tự động dừng
                // Bạn có thể điều chỉnh thời gian nếu cần
                _timer.Change(500, Timeout.Infinite); // dừng sau 500ms
                _state = TxState.Idle;
                break;
        }
    }

    /* ============================================================
     * SEND HELPERS
     * ============================================================ */

    /// <summary>
    /// Gửi frame hiện tại – dùng chung cho cả ON và OFF
    /// </summary>
    private void SendCurrentCommand()
    {
        ControlModuleCommand snapshot;

        lock (_lock)
        {
            snapshot = Clone(_cmd);
        }

        // Không gửi nếu không có nhu cầu công suất (tránh spam frame rác)
        // Nhưng nếu đang tắt (Current = 0, Stage1 = false) thì VẪN PHẢI gửi để charger nhận lệnh tắt
        if (snapshot.Demand_Voltage <= 0 && snapshot.Demand_Current <= 0 && !snapshot.Demand_PowerStage1)
        {
            // Đây là frame OFF → vẫn gửi để đảm bảo tắt chắc chắn
        }
        else if (snapshot.Demand_Voltage <= 0 || snapshot.Demand_Current <= 0)
        {
            // Không có nhu cầu công suất và không phải frame tắt → không gửi
            return;
        }

        // ➜ Không ép buộc PowerStage1 = true nữa → giữ nguyên giá trị người dùng set
        _can.Send(0x191, ControlModuleEncoder.Encode(snapshot));
    }

    private static ControlModuleCommand Clone(ControlModuleCommand c)
    {
        var copy = new ControlModuleCommand
        {
            Demand_Voltage = c.Demand_Voltage,
            Demand_Current = c.Demand_Current,
            Demand_PowerStage1 = c.Demand_PowerStage1,
            Demand_ClearFaults = c.Demand_ClearFaults
        };

        Array.Copy(c.Demand_PowerStages, copy.Demand_PowerStages, c.Demand_PowerStages.Length);
        return copy;
    }

    public void Dispose()
    {
        _timer.Dispose();
    }
}