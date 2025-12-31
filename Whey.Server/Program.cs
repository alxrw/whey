using Serilog;
using Whey.Infra.Extensions;
using Whey.Infra.Services;
using Whey.Server.Auth;
using Whey.Server.Grpc;

var builder = WebApplication.CreateBuilder(args);

// add logging
Log.Logger = new LoggerConfiguration()
	.MinimumLevel.Debug()
	// TODO: change this to write to file
	// or maybe do smth with azure vms?
	.WriteTo.Console()
	.CreateLogger();

builder.Services.AddLogging(builder =>
{
	builder.ClearProviders();
	builder.AddSerilog(dispose: true);
});

// add gRPC to DI
builder.Services.AddTransient<AuthenticationInterceptor>();
builder.Services.AddSingleton<PackageTrackerServiceImpl>();
builder.Services.AddGrpc(opts =>
{
	opts.Interceptors.Add<AuthenticationInterceptor>();
});

// add infra services
builder.Services.AddWheyInfra(builder.Configuration);
builder.Services.AddTransient<IApiSchedulingService, ApiSchedulingService>();
builder.Services.AddSingleton<IBinStorageService, BinStorageService>();
builder.Services.AddTransient<IPackageSyncService, PackageSyncService>();
builder.Services.AddTransient<IAssetMappingService, AssetMappingService>();

// TODO: initialize all jobs here.

var app = builder.Build();

// Configure the HTTP request pipeline.
app.MapGrpcService<RegistrationServiceImpl>();
app.MapGrpcService<PackageTrackerServiceImpl>().RequireAuthorization();
app.MapGet("/", () => "Communication with gRPC endpoints must be made through a gRPC client. To learn how to create a client, visit: https://go.microsoft.com/fwlink/?linkid=2086909");

app.Run();
