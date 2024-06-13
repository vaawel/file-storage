using Asp.Versioning;
using FileStorageAPI.MinIOFileApi.Configs;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi.Models;
using Minio;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxConcurrentConnections = 100000;
    options.Limits.MaxConcurrentUpgradedConnections = 100000;
    options.Limits.MaxRequestBodySize = 1073741824; // 1024 MB
    options.Limits.KeepAliveTimeout = TimeSpan.FromMinutes(2);
    options.Limits.RequestHeadersTimeout = TimeSpan.FromMinutes(1);
});

builder.Services.Configure<MinIOOptions>(builder.Configuration.GetSection("MinIO"));

builder.Services.AddSingleton<IMinioClient>(serviceProvider =>
{
    var options = serviceProvider.GetRequiredService<IOptions<MinIOOptions>>().Value;
    return new MinioClient()
        .WithEndpoint(options.Endpoint)
        .WithCredentials(options.AccessKey, options.SecretKey)
        .Build();
});

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
    options.Providers.Add<GzipCompressionProvider>();
    options.Providers.Add<BrotliCompressionProvider>();
});
builder.Services.Configure<RouteOptions>(options =>
{
    options.LowercaseUrls = true;
    options.LowercaseQueryStrings = true;
    options.AppendTrailingSlash = true;
});
builder.Services.AddApiVersioning(options =>
{
    options.DefaultApiVersion = new ApiVersion(1,0);
    options.ReportApiVersions = true;
    options.AssumeDefaultVersionWhenUnspecified = true;
}).AddApiExplorer(options =>
{
    options.GroupNameFormat = "'v'VVV";
    options.SubstituteApiVersionInUrl = true;
});

builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Version = "v1",
        Title = "Vaawel File Storage API",
        Description = "Vaawel File Storage is an API that can add, get, and delete files on a MinIO server."
    });
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
