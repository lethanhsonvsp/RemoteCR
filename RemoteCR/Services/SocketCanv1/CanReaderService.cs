using Microsoft.Extensions.Hosting;
using RemoteCR.Services.Can;
using System.Threading;
using System.Threading.Tasks;

namespace RemoteCR.Services.SocketCanv1;

public class CanReaderService : BackgroundService
{
    private readonly SocketCan _can;
    private readonly DeltaDecoder _decoder;
    private readonly CanStateContainer _state;

    public CanReaderService(DeltaDecoder decoder, CanStateContainer state)
    {
        _decoder = decoder;
        _state = state;

        _can = new SocketCan("can0");
        _state.IsConnected = _can.IsConnected;

        // thông báo UI ngay lần đầu
        _state.NotifyChanged();

        _can.OnFrameReceived += frame =>
        {
            if (!_state.IsConnected)
            {
                _state.IsConnected = true;
                _state.NotifyChanged();
            }

            _decoder.Decode(frame.Id, frame.Dlc, frame.Data);
        };
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        return Task.Run(() =>
        {
            try
            {
                _can.StartReading(stoppingToken);
            }
            catch
            {
                // mất kết nối CAN
                _state.IsConnected = false;
                _state.NotifyChanged();
            }
        }, stoppingToken);
    }
}
