using Microsoft.Extensions.Hosting;
using System.Threading;
using System.Threading.Tasks;

namespace RemoteCR.Services.Can;

public class CanReaderService : BackgroundService
{
    private readonly SocketCan _can;
    private readonly DeltaDecoder _decoder;

    public CanReaderService(DeltaDecoder decoder)
    {
        _decoder = decoder;
        _can = new SocketCan("can0");

        _can.OnFrameReceived += frame =>
        {
            _decoder.Decode(frame.Id, frame.Dlc, frame.Data);
        };
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
        => Task.Run(() => _can.StartReading());
}
