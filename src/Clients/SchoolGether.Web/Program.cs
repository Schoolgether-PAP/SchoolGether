using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using SchoolGether.Web;
using SchoolGether.Web.Identity;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");
builder.Services.AddMemoryIdentitySession(builder.Configuration["Api:BaseUrl"], builder.HostEnvironment.IsDevelopment());
await builder.Build().RunAsync();
