using SubmissionProcessor.Worker;
using SubmissionProcessor.Worker.Services;
using SubmissionProcessor.Worker.Data;
using Microsoft.EntityFrameworkCore;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddHostedService<Worker>();

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
?? throw new InvalidOperationException("Connection string not found");

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseMySQL(connectionString));

builder.Services.AddScoped<IFileStorageService, LocalFileStorageService>();

var host = builder.Build();
host.Run();
