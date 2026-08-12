using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SocialBlocker.Service;

IHost host = Host.CreateDefaultBuilder(args)
    .UseWindowsService(options =>
    {
        options.ServiceName = "SocialBlockerService";
    })
    .ConfigureServices(services =>
    {
        services.AddHostedService<Worker>();
    })
    .Build();

await host.RunAsync();
