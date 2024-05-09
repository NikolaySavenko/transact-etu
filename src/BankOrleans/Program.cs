using Npgsql;
using Orleans;
using Orleans.Concurrency;
using Orleans.Configuration;
using Orleans.Hosting;
using Orleans.Transactions.Abstractions;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddApplicationInsightsTelemetry();
// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
var connectionString = builder.Configuration.GetConnectionString("PostgreSQL");
builder.Services.AddNpgsqlDataSource(connectionString);
var invariant = "Npgsql";
var azureTableConnectionString = builder.Configuration.GetConnectionString("AzureTable");

builder.UseOrleans(siloBuilder =>
    {
        siloBuilder
        .UseAdoNetClustering(options =>
        {
            options.Invariant = invariant;
            options.ConnectionString = connectionString;
        })
        .AddAdoNetGrainStorage("OrleansStorage", options =>
        {
            options.Invariant = invariant;
            options.ConnectionString = connectionString;
        })
        .AddAzureTableTransactionalStateStorageAsDefault(options => options.ConfigureTableServiceClient(azureTableConnectionString))
        // .AddAzureTableTransactionalStateStorage("OrleansTransactions", options => options.ConfigureTableServiceClient(azureTableConnectionString))
        .UseTransactions();
    });
var app = builder.Build();
app.UseSwagger();
app.UseSwaggerUI();

var api = app.MapGroup("/api");
api.MapPost("/createDatabase", (NpgsqlConnection connection) => Results.Ok());

api.MapPost("/createAccount", async (CreateAccountCommand command, IClusterClient client, ITransactionClient transactionClient) =>
{
    var account = client.GetGrain<IAccountGrain>(command.Id);
    var currentBalance = await account.GetBalance();
    await transactionClient.RunTransaction(TransactionOption.Create, async () =>
        {
            await account.Withdraw(currentBalance);
            await account.Deposit(command.Balance);
        });
    return Results.Ok(await account.GetBalance());
});

api.MapGet("/{id:guid}/balance", async (Guid id, IClusterClient client) =>
{
    var account = client.GetGrain<IAccountGrain>(id);
    return Results.Ok(await account.GetBalance());
});

//orleansTransfer
api.MapPost("/orleansTransfer", async (TransferCommand command, IClusterClient client, ITransactionClient transactionClient) =>
{
    var fromAccount = client.GetGrain<IAccountGrain>(command.From);
    var toAccount = client.GetGrain<IAccountGrain>(command.To);
    await transactionClient.RunTransaction(TransactionOption.Create, async () =>
        {
            await fromAccount.Withdraw(command.Amount);
            await toAccount.Deposit(command.Amount);
        });
    return Results.Ok();
});

app.Run();

record CreateAccountCommand(Guid Id, int Balance = 0);
record DepositCommand(Guid Id, int Amount);
record TransferCommand(Guid From, Guid To, int Amount);

public interface IAccountGrain : IGrainWithGuidKey
{
    [Transaction(TransactionOption.Join)]
    Task Withdraw(int amount);

    [Transaction(TransactionOption.Join)]
    Task Deposit(int amount);

    [Transaction(TransactionOption.CreateOrJoin)]
    Task<int> GetBalance();
}

public interface IAtmGrain : IGrainWithIntegerKey
{
    [Transaction(TransactionOption.Create)]
    Task Transfer(
        IAccountGrain fromAccount,
        IAccountGrain toAccount,
        int amountToTransfer);
}

[GenerateSerializer]
public record class Balance
{
    [Id(0)]
    public int Value { get; set; } = 0;
}

[Reentrant]
public sealed class AccountGrain : Grain, IAccountGrain
{
    private readonly ITransactionalState<Balance> _balance;

    public AccountGrain(
        [TransactionalState("balance")] ITransactionalState<Balance> balance) =>
        _balance = balance ?? throw new ArgumentNullException(nameof(balance));

    public Task Deposit(int amount) =>
        _balance.PerformUpdate(
            balance => balance.Value += amount);

    public Task Withdraw(int amount) =>
        _balance.PerformUpdate(balance =>
        {
            if (balance.Value < amount)
            {
                throw new InvalidOperationException(
                    $"Withdrawing {amount} credits from account " +
                    $"\"{this.GetPrimaryKeyString()}\" would overdraw it." +
                    $" This account has {balance.Value} credits.");
            }

            balance.Value -= amount;
        });

    public Task<int> GetBalance() =>
        _balance.PerformRead(balance => balance.Value);
}

[StatelessWorker]
public class AtmGrain : Grain, IAtmGrain
{
    public Task Transfer(
        IAccountGrain fromAccount,
        IAccountGrain toAccount,
        int amountToTransfer) =>
        Task.WhenAll(
            fromAccount.Withdraw(amountToTransfer),
            toAccount.Deposit(amountToTransfer));
}
