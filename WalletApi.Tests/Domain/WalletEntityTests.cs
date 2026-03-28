using System;
using WalletApi.Domain.Entities;
using Xunit;

namespace WalletApi.Tests.Domain;

public class WalletEntityTests
{
    [Fact]
    public void Credit_WithPositiveAmount_IncreasesBalance()
    {
        var wallet = new Wallet();
        wallet.Credit(100m);
        Assert.Equal(100m, wallet.Balance);
    }

    [Fact]
    public void Credit_MultipleTimes_AccumulatesCorrectly()
    {
        var wallet = new Wallet();
        wallet.Credit(100m);
        wallet.Credit(50.75m);
        Assert.Equal(150.75m, wallet.Balance);
    }

    [Fact]
    public void Credit_WithZeroAmount_ThrowsArgumentException()
    {
        var wallet = new Wallet();
        Assert.Throws<ArgumentException>(() => wallet.Credit(0m));
    }

    [Fact]
    public void Credit_WithNegativeAmount_ThrowsArgumentException()
    {
        var wallet = new Wallet();
        Assert.Throws<ArgumentException>(() => wallet.Credit(-10m));
    }

    [Fact]
    public void Debit_WithSufficientFunds_DecreasesBalance()
    {
        var wallet = new Wallet();
        wallet.Credit(200m);
        wallet.Debit(80m);
        Assert.Equal(120m, wallet.Balance);
    }

    [Fact]
    public void Debit_ExactBalance_LeavesZeroBalance()
    {
        var wallet = new Wallet();
        wallet.Credit(100m);
        wallet.Debit(100m);
        Assert.Equal(0m, wallet.Balance);
    }

    [Fact]
    public void Debit_WithInsufficientFunds_ThrowsInvalidOperationException()
    {
        var wallet = new Wallet();
        wallet.Credit(50m);
        Assert.Throws<InvalidOperationException>(() => wallet.Debit(100m));
    }

    [Fact]
    public void Debit_WithZeroAmount_ThrowsArgumentException()
    {
        var wallet = new Wallet();
        wallet.Credit(100m);
        Assert.Throws<ArgumentException>(() => wallet.Debit(0m));
    }

    [Fact]
    public void Debit_WithNegativeAmount_ThrowsArgumentException()
    {
        var wallet = new Wallet();
        wallet.Credit(100m);
        Assert.Throws<ArgumentException>(() => wallet.Debit(-10m));
    }

    [Fact]
    public void Debit_OnEmptyWallet_ThrowsInvalidOperationException()
    {
        var wallet = new Wallet();
        Assert.Throws<InvalidOperationException>(() => wallet.Debit(1m));
    }
    
    [Fact]
    public void NewWallet_HasZeroBalance()
    {
        var wallet = new Wallet();
        Assert.Equal(0m, wallet.Balance);
    }

    [Fact]
    public void NewWallet_IsActiveByDefault()
    {
        var wallet = new Wallet();
        Assert.True(wallet.IsActive);
    }

    [Fact]
    public void NewWallet_HasDefaultUSDCurrency()
    {
        var wallet = new Wallet();
        Assert.Equal("USD", wallet.Currency);
    }

    [Fact]
    public void NewWallet_GetsUniqueId()
    {
        var w1 = new Wallet();
        var w2 = new Wallet();
        Assert.NotEqual(w1.Id, w2.Id);
    }
}