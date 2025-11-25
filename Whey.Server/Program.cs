using Whey.Infra.Extensions;
using Whey.Server.Auth;
using Whey.Server.Grpc;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddTransient<AuthenticationInterceptor>();
builder.Services.AddSingleton<PackageTrackerServiceImpl>();
builder.Services.AddGrpc(opts =>
{
	opts.Interceptors.Add<AuthenticationInterceptor>();
});
builder.Services.AddWheyInfra(builder.Configuration);

var app = builder.Build();

// Configure the HTTP request pipeline.
app.MapGrpcService<RegistrationServiceImpl>();
app.MapGrpcService<PackageTrackerServiceImpl>().RequireAuthorization();
app.MapGet("/", () => "Communication with gRPC endpoints must be made through a gRPC client. To learn how to create a client, visit: https://go.microsoft.com/fwlink/?linkid=2086909");

app.Run();
