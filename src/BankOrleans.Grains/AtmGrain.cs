using BankOrleans.Interfaces;
using Orleans;
using Orleans.Concurrency;

namespace BankOrleans.Grains;

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
