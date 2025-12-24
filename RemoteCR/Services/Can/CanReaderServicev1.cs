namespace RemoteCR.Services.Can;

public class CanReaderServicev1 : BackgroundService
{
    private readonly SocketCan _can;

    public CanReaderServicev1(SocketCan can)
    {
        _can = can;
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        return Task.Run(() =>
        {
            if (!OperatingSystem.IsLinux())
                return;

            _can.StartReading(stoppingToken);
        }, stoppingToken);
    }
}