using RemoteCR;
using RemoteCR.Components;
using RemoteCR.Services.Can;
using RemoteCR.Services.Modbus;
using RemoteCR.Services.SocketCanv1;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
 builder.Services.AddSingleton<ModbusBackgroundService>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<ModbusBackgroundService>());
builder.Services.AddSignalR();
builder.Services.AddSingleton(new TadaService());

builder.Services.AddSingleton<SocketCan>(sp =>
{
    // có thể đọc từ config nếu muốn
    return new SocketCan("can0");
});
builder.Services.AddSingleton<CanSocketReaderService>();
builder.Services.AddSingleton<CanSocketWriterService>();

builder.Services.AddHostedService<CanReaderServicev1>();


builder.Services.AddSingleton<CanStateContainer>();
builder.Services.AddSingleton<DeltaDecoder>();
builder.Services.AddSingleton<DeltaChargerCommandService>();

builder.Services.AddHostedService<CanReaderService>();


var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();


app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
