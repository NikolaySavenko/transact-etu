using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateSlimBuilder(args);

builder.Services.AddApplicationInsightsTelemetry();
// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddDbContext<BankContext>(options => options.UseNpgsql(builder.Configuration.GetConnectionString("PostgreSQL")));

var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseSwagger();
app.UseSwaggerUI();

var api = app.MapGroup("/api");
api.MapPost("/createDatabase", async (BankContext context) =>
{
    await context.Database.EnsureCreatedAsync();
    return Results.Ok();
});

api.MapPost("/dropAccounts", async (BankContext context) =>
{
    //drop table accounts;
    var accounts = context.Accounts.ToArray();
    context.Accounts.RemoveRange(accounts);
    await context.SaveChangesAsync();
    return Results.Ok();
});

api.MapPost("/createAccount", async (CreateAccountCommand command, BankContext context) =>
{
    await context.Accounts.AddAsync(new Account { Id = command.Id, Balance = command.Balance });
    await context.SaveChangesAsync();
    return Results.Ok();
});

api.MapGet("/randomAccountId", async (BankContext context) =>
{
    var id = await context.Accounts
        .OrderBy(r => Guid.NewGuid())
        .Select(account => account.Id)
        .FirstAsync();
    return Results.Ok(id);
});

api.MapGet("/{id:guid}/balance", async (Guid id, BankContext context) =>
{
    var account = await context.Accounts.FindAsync(id);
    return Results.Ok(account.Balance);
});

api.MapPost("/deposit", async (DepositCommand command, BankContext context) =>
{
    var account = await context.Accounts.Where(a => a.Id == command.Id).FirstOrDefaultAsync();
    account.Balance += command.Amount;
    await context.SaveChangesAsync();
    return Results.Ok();
});

// transfer
api.MapPost("/transfer", async (TransferCommand command, BankContext context) =>
{
    using var transaction = await context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
    try
    {
        var account = await context.Accounts.FindAsync(command.From);
        app.Logger.LogInformation($"From Account {account.Id} has balance {account.Balance}");
        account.Balance -= command.Amount;

        var toAccount = await context.Accounts.FindAsync(command.To);
        app.Logger.LogInformation($"To Account {toAccount.Id} has balance {toAccount.Balance}");
        toAccount.Balance += command.Amount;
        await context.SaveChangesAsync();
        await transaction.CommitAsync();

        return Results.Ok();
    }
    catch (Exception ex)
    {
        app.Logger.LogError(ex, "Error transferring money");
        await transaction.RollbackAsync();
        return Results.BadRequest("Error transferring money");
    }
});

// optimistic transfer
api.MapPost("/optimisticTransfer", async (TransferCommand command, BankContext context) =>
{
    using var transaction = await context.Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted);
    try
    {
        var account = await context.Accounts.FindAsync(command.From);
        app.Logger.LogInformation($"From Account {account.Id} has balance {account.Balance}");
        account.Balance -= command.Amount;

        var toAccount = await context.Accounts.FindAsync(command.To);
        app.Logger.LogInformation($"To Account {toAccount.Id} has balance {toAccount.Balance}");
        toAccount.Balance += command.Amount;
        await context.SaveChangesAsync();
        await transaction.CommitAsync();

        return Results.Ok();
    }
    catch (Exception ex)
    {
        app.Logger.LogError(ex, "Error transferring money");
        await transaction.RollbackAsync();
        return Results.BadRequest("Error transferring money");
    }
});


app.Run();

record CreateAccountCommand(Guid Id, int Balance = 0);
record DepositCommand(Guid Id, int Amount);
record TransferCommand(Guid From, Guid To, int Amount);

public class Account {
    public Guid Id { get; set; }
    public int Balance { get; set; }
    public uint Version { get; set; }
}

public class BankContext : DbContext
{
    public BankContext(DbContextOptions<BankContext> options) : base(options) { }
    public DbSet<Account> Accounts { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder) {
        optionsBuilder.EnableSensitiveDataLogging();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Account>().ToTable("accounts", t => t.HasCheckConstraint("positive_balance", "balance >= 0"));
        modelBuilder.Entity<Account>().Property(a => a.Balance).HasDefaultValue(0).HasColumnName("balance");
        modelBuilder.Entity<Account>().Property(a => a.Id).HasColumnName("id");
        modelBuilder.Entity<Account>().HasKey(a => a.Id);
        // optimistic concurency
        modelBuilder.Entity<Account>().Property(b => b.Version).IsRowVersion();
        base.OnModelCreating(modelBuilder);
    }
}