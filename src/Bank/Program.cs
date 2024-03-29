using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateSlimBuilder(args);

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddDbContext<BankContext>(options => options.UseNpgsql(builder.Configuration.GetConnectionString("PostgreSQL")));

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

var api = app.MapGroup("/api");
api.MapPost("/createDatabase", async (BankContext context) =>
{
    await context.Database.EnsureCreatedAsync();
    return Results.Ok();
});

api.MapPost("/deleteDatabase", async (BankContext context) =>
{
    await context.Database.EnsureDeletedAsync();
    return Results.Ok();
});

api.MapPost("/createAccount", async (CreateAccountCommand command, BankContext context) =>
{
    await context.Accounts.AddAsync(new Account { Id = command.Id, Balance = command.Balance });
    await context.SaveChangesAsync();
    return Results.Ok();
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
api.MapPost("/transfer", (TransferCommand command, BankContext context) =>
{
    using var transaction = context.Database.BeginTransaction(System.Data.IsolationLevel.Serializable);
    try
    {
        var account = context.Accounts.Find(command.From);
        app.Logger.LogInformation($"From Account {account.Id} has balance {account.Balance}");
        account.Balance -= command.Amount;

        var toAccount = context.Accounts.Find(command.To);
        app.Logger.LogInformation($"To Account {toAccount.Id} has balance {toAccount.Balance}");
        toAccount.Balance += command.Amount;
        context.SaveChanges();
        transaction.Commit();

        return Results.Ok();
    }
    catch (Exception ex)
    {
        app.Logger.LogError(ex, "Error transferring money");
        transaction.Rollback();
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
        base.OnModelCreating(modelBuilder);
    }
}