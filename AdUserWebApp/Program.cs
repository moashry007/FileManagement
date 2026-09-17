using AdUserWebApp.Options;
using AdUserWebApp.Services;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi(options =>
{
    options.AddDocumentTransformer((document, context, ct) =>
    {
        document.Info = new() { Title = "AD User Web App", Version = "v1" };
        return Task.CompletedTask;
    });
});

builder.Services.Configure<LdapOptions>(builder.Configuration.GetSection(LdapOptions.SectionName));

#pragma warning disable CA1416 // AdUserDirectoryService is Windows-only by design; see its [SupportedOSPlatform("windows")].
builder.Services.AddSingleton<IAdUserDirectoryService, AdUserDirectoryService>();
#pragma warning restore CA1416

var app = builder.Build();

app.MapOpenApi();
app.MapScalarApiReference();

app.UseDefaultFiles();
app.UseStaticFiles();

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();
