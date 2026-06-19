using Identity.Api.Startup;

var builder = WebApplication.CreateBuilder(args);
var config = builder.Configuration;

builder.Services.AddControllers();
builder.Services.AddAwsServices(config);
builder.Services.AddRedis(config);
builder.Services.AddIdentityServices(config);

var app = builder.Build();

app.UseAuthorization();

app.MapControllers();

app.Run();
