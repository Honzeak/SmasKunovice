using SmasKunovice.BrokerService;

var builder = Host.CreateApplicationBuilder(args);
builder.Configuration.SetBasePath(AppContext.BaseDirectory);
builder.Configuration.AddJsonFile("appsettings.json", optional: false);
builder.Services.AddWindowsService(options => options.ServiceName = "SmasKunovice.BrokerService");
builder.Services.AddHostedService<MqttBrokerService>();
var host = builder.Build();
await host.RunAsync();