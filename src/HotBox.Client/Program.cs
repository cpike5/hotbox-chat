using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using HotBox.Client;
using HotBox.Client.DependencyInjection;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddClientServices();
builder.Services.AddApiClient(new Uri(builder.HostEnvironment.BaseAddress));

await builder.Build().RunAsync();
