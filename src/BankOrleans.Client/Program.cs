using BankOrleans.Interfaces;
using Orleans;
using Orleans.Hosting;

var builder = WebApplication.CreateSlimBuilder(args);

builder.Services.AddApplicationInsightsTelemetry();
// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.UseOrleansClient(client =>
    {
        client.UseLocalhostClustering()
            .UseTransactions();
    });
var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseSwagger();
app.UseSwaggerUI();

var api = app.MapGroup("/api");

api.MapPost("/{id:guid}/balance", async (Guid id, IClusterClient client) =>
{
    var account = client.GetGrain<IAccountGrain>(id);
    await account.Deposit(100);
    return Results.Ok(await account.GetBalance());
});

api.MapGet("/{id:guid}/balance", async (Guid id, IClusterClient client) =>
{
    var account = client.GetGrain<IAccountGrain>(id);
    await account.Deposit(100);
    return Results.Ok(await account.GetBalance());
});

api.MapPost("/dropAccounts", (IClusterClient client, ITransactionClient transactionClient) =>
{
    return Results.Ok();
});

app.Run();
