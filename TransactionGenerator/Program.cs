// Generate many csv files with transactions by this rules:
// 1. Every file contains equal number of transactions
// 2. Transaction is line in format "from,to,amount"
// 3. Transaction 'from' is guid address of sender
// 4. Transaction 'to' is guid address of receiver
// 5. Transaction 'amount' is amount of money to be received
// 6. User can be sender or receiver of transaction
// 7. User balance is calculated by sequentially applying previous transactions
// 8. User can be sender only if his balance is greater than zero
// 9. Transaction amount can be less than user actual balance
// 10. Users count and transactions count are provided from command line
// 11. After all transactions are written to file program should generate a new file with users final balances into excpected.csv in format 'user,balance'

using System.Diagnostics;

var st = new Stopwatch();
st.Start();

// from command line
var transactionCount = long.Parse(args[0]);
var userCount = long.Parse(args[1]);

// generate users
var users = new Guid[userCount];
for (int i = 0; i < users.Length; i++)
{
    users[i] = Guid.NewGuid();
}

// generate transactions
var balances = new Dictionary<Guid, int>();

// initialize balances
foreach (var user in users)
{
    balances[user] = 1000;
}

// generate transactions
var random = new Random();
var filename = $"transactions.csv";
using var transactionsWriter = new StreamWriter(filename);
for (var i = 0; i < transactionCount; i++) {
	// check if sender can send money
	// user can be sender only in he have balance > 0
	var sender = users[random.Next(0, users.Length)];
	while (!balances.ContainsKey(sender) || balances[sender] <= 0)
	{
		sender = users[random.Next(0, users.Length)];
	}

	// generate receiver
	var receiver = users[random.Next(0, users.Length)];
	while (receiver == sender)
	{
		receiver = users[random.Next(0, users.Length)];
	}

	// generate amount
	var amount = random.Next(1, balances[sender] + 1);

	// update balances
	balances[sender] -= amount;
	balances[receiver] = balances.ContainsKey(receiver) ? balances[receiver] + amount : amount;

	// add transaction
	transactionsWriter.WriteLine($"{sender},{receiver},{amount}");
	Console.WriteLine($"Transaction #{i} added {sender} to {receiver} amount {amount}");
}

// write final balances to file

using (var writer = new StreamWriter("expected.csv"))
{
    foreach (var balance in balances)
    {
        writer.WriteLine($"{balance.Key},{balance.Value}");
    }
}

Console.WriteLine($"Done in {st.ElapsedMilliseconds} ms");