using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Xunit;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using WalletApi.Infrastructure.Data;
using WalletApi.IntegrationTests.Commons;

namespace WalletApi.IntegrationTests.Controllers;

public class GetWalletTests : WalletTestBase
{
    public GetWalletTests(WalletApiFactory factory) : base(factory) { }

    [Fact]
    public async Task GetWallet_AuthenticatedUser_ReturnsWallet()
    {
        await ResetDatabaseAsync();
        var (token, _) = await RegisterAndLoginAsync("getwallet@example.com");

        var response = await AuthenticatedClient(token).GetAsync("/api/v1/wallets");

        response.EnsureSuccessStatusCode();
        var wallet = await response.Content.ReadFromJsonAsync<WalletResponse>(JsonOptions);
        Assert.NotNull(wallet);
        Assert.Equal(0m, wallet!.Balance);
        Assert.True(wallet.IsActive);
    }

    [Fact]
    public async Task GetWallet_Unauthenticated_Returns401()
    {
        await ResetDatabaseAsync();

        var response = await Client.GetAsync("/api/v1/wallets");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetWallet_InvalidToken_Returns401()
    {
        await ResetDatabaseAsync();
        var client = Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", "this.is.not.a.valid.jwt");

        var response = await client.GetAsync("/api/v1/wallets");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}

public class TopUpTests : WalletTestBase
{
    public TopUpTests(WalletApiFactory factory) : base(factory) { }

    [Fact]
    public async Task TopUp_ValidAmount_IncrementsBalance()
    {
        await ResetDatabaseAsync();
        var (token, _) = await RegisterAndLoginAsync("topup@example.com");
        var http       = AuthenticatedClient(token);

        var response = await http.PostAsJsonAsync(
            "/api/v1/wallets/topup",
            new { Amount = 100m, Description = "Initial deposit" });

        response.EnsureSuccessStatusCode();
        var wallet = await response.Content.ReadFromJsonAsync<WalletResponse>(JsonOptions);
        Assert.Equal(100m, wallet!.Balance);
    }

    [Fact]
    public async Task TopUp_MultipleTopUps_AccumulatesBalance()
    {
        await ResetDatabaseAsync();
        var (token, _) = await RegisterAndLoginAsync("multitopup@example.com");
        var http       = AuthenticatedClient(token);

        await http.PostAsJsonAsync("/api/v1/wallets/topup", new { Amount = 50m });
        var response = await http.PostAsJsonAsync("/api/v1/wallets/topup", new { Amount = 75m });

        response.EnsureSuccessStatusCode();
        var wallet = await response.Content.ReadFromJsonAsync<WalletResponse>(JsonOptions);
        Assert.Equal(125m, wallet!.Balance);
    }

    [Fact]
    public async Task TopUp_ZeroAmount_ReturnsBadRequest()
    {
        await ResetDatabaseAsync();
        var (token, _) = await RegisterAndLoginAsync("topupzero@example.com");
        var http       = AuthenticatedClient(token);

        var response = await http.PostAsJsonAsync(
            "/api/v1/wallets/topup",
            new { Amount = 0m });
        
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task TopUp_NegativeAmount_ReturnsBadRequest()
    {
        await ResetDatabaseAsync();
        var (token, _) = await RegisterAndLoginAsync("topupneg@example.com");
        var http       = AuthenticatedClient(token);

        var response = await http.PostAsJsonAsync(
            "/api/v1/wallets/topup",
            new { Amount = -50m });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task TopUp_Unauthenticated_Returns401()
    {
        await ResetDatabaseAsync();

        var response = await Client.PostAsJsonAsync(
            "/api/v1/wallets/topup",
            new { Amount = 100m });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task TopUp_ReturnsUpdatedWalletResponse()
    {
        await ResetDatabaseAsync();
        var (token, _) = await RegisterAndLoginAsync("topupresp@example.com");
        var http       = AuthenticatedClient(token);

        var response = await http.PostAsJsonAsync(
            "/api/v1/wallets/topup",
            new { Amount = 200m, Description = "Salary" });

        response.EnsureSuccessStatusCode();
        var wallet = await response.Content.ReadFromJsonAsync<WalletResponse>(JsonOptions);
        Assert.NotNull(wallet);
        Assert.NotEqual(Guid.Empty, wallet!.Id);
        Assert.Equal(200m, wallet.Balance);
        Assert.True(wallet.IsActive);
    }
}

public class TransferTests : WalletTestBase
{
    public TransferTests(WalletApiFactory factory) : base(factory) { }

    [Fact]
    public async Task Transfer_ValidRequest_DebitsSenderCreditRecipient()
    {
        await ResetDatabaseAsync();
        
        var (senderToken, _)    = await RegisterAndLoginAsync("sender@example.com");
        var senderHttp          = AuthenticatedClient(senderToken);
        await senderHttp.PostAsJsonAsync("/api/v1/wallets/topup", new { Amount = 500m });
        var senderWallet        = await (await senderHttp.GetAsync("/api/v1/wallets"))
                                        .Content.ReadFromJsonAsync<WalletResponse>(JsonOptions);
        
        var (recipientToken, _) = await RegisterAndLoginAsync("recipient@example.com");
        var recipientHttp       = AuthenticatedClient(recipientToken);
        var recipientWallet     = await (await recipientHttp.GetAsync("/api/v1/wallets"))
                                        .Content.ReadFromJsonAsync<WalletResponse>(JsonOptions);
        
        var response = await senderHttp.PostAsJsonAsync("/api/v1/wallets/transfer", new
        {
            RecipientWalletId = recipientWallet!.Id,
            Amount            = 200m,
            Description       = "Test transfer"
        });

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        
        var updatedSender = await (await senderHttp.GetAsync("/api/v1/wallets"))
                                   .Content.ReadFromJsonAsync<WalletResponse>(JsonOptions);
        Assert.Equal(300m, updatedSender!.Balance);
        
        var updatedRecipient = await (await recipientHttp.GetAsync("/api/v1/wallets"))
                                      .Content.ReadFromJsonAsync<WalletResponse>(JsonOptions);
        Assert.Equal(200m, updatedRecipient!.Balance);
    }

    [Fact]
    public async Task Transfer_InsufficientFunds_ReturnsBadRequest()
    {
        await ResetDatabaseAsync();

        var (senderToken, _)    = await RegisterAndLoginAsync("broke@example.com");
        var senderHttp          = AuthenticatedClient(senderToken);
        await senderHttp.PostAsJsonAsync("/api/v1/wallets/topup", new { Amount = 50m });

        var (recipientToken, _) = await RegisterAndLoginAsync("recvbrk@example.com");
        var recipientHttp       = AuthenticatedClient(recipientToken);
        var recipientWallet     = await (await recipientHttp.GetAsync("/api/v1/wallets"))
                                        .Content.ReadFromJsonAsync<WalletResponse>(JsonOptions);

        var response = await senderHttp.PostAsJsonAsync("/api/v1/wallets/transfer", new
        {
            RecipientWalletId = recipientWallet!.Id,
            Amount            = 100m   // more than the 50 available
        });
        
        Assert.True(
            response.StatusCode is HttpStatusCode.BadRequest
                                 or HttpStatusCode.UnprocessableEntity
                                 or HttpStatusCode.Conflict,
            $"Expected a 4xx status but got {(int)response.StatusCode}");
    }

    [Fact]
    public async Task Transfer_ZeroAmount_ReturnsBadRequest()
    {
        await ResetDatabaseAsync();

        var (senderToken, _)    = await RegisterAndLoginAsync("sndrzero@example.com");
        var senderHttp          = AuthenticatedClient(senderToken);
        await senderHttp.PostAsJsonAsync("/api/v1/wallets/topup", new { Amount = 100m });

        var (recipientToken, _) = await RegisterAndLoginAsync("recvzero@example.com");
        var recipientHttp       = AuthenticatedClient(recipientToken);
        var recipientWallet     = await (await recipientHttp.GetAsync("/api/v1/wallets"))
                                        .Content.ReadFromJsonAsync<WalletResponse>(JsonOptions);

        var response = await senderHttp.PostAsJsonAsync("/api/v1/wallets/transfer", new
        {
            RecipientWalletId = recipientWallet!.Id,
            Amount            = 0m
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Transfer_NonExistentRecipient_Returns404()
    {
        await ResetDatabaseAsync();

        var (senderToken, _) = await RegisterAndLoginAsync("sndr404@example.com");
        var senderHttp       = AuthenticatedClient(senderToken);
        await senderHttp.PostAsJsonAsync("/api/v1/wallets/topup", new { Amount = 100m });

        var response = await senderHttp.PostAsJsonAsync("/api/v1/wallets/transfer", new
        {
            RecipientWalletId = Guid.NewGuid(),   // wallet that doesn't exist
            Amount            = 50m
        });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Transfer_ToSelf_ReturnsBadRequest()
    {
        await ResetDatabaseAsync();

        var (token, _)   = await RegisterAndLoginAsync("selfxfer@example.com");
        var http         = AuthenticatedClient(token);
        await http.PostAsJsonAsync("/api/v1/wallets/topup", new { Amount = 200m });
        var wallet       = await (await http.GetAsync("/api/v1/wallets"))
                                  .Content.ReadFromJsonAsync<WalletResponse>(JsonOptions);

        var response = await http.PostAsJsonAsync("/api/v1/wallets/transfer", new
        {
            RecipientWalletId = wallet!.Id,
            Amount            = 50m
        });
        
        Assert.True(
            (int)response.StatusCode >= 400 && (int)response.StatusCode < 500,
            $"Expected a client-error status but got {(int)response.StatusCode}");
    }

    [Fact]
    public async Task Transfer_Unauthenticated_Returns401()
    {
        await ResetDatabaseAsync();

        var response = await Client.PostAsJsonAsync("/api/v1/wallets/transfer", new
        {
            RecipientWalletId = Guid.NewGuid(),
            Amount            = 50m
        });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}

public class GetTransactionsTests : WalletTestBase
{
    public GetTransactionsTests(WalletApiFactory factory) : base(factory) { }

    [Fact]
    public async Task GetTransactions_NoActivity_ReturnsEmptyList()
    {
        await ResetDatabaseAsync();
        var (token, _) = await RegisterAndLoginAsync("notx@example.com");

        var response = await AuthenticatedClient(token)
                           .GetAsync("/api/v1/wallets/transactions");

        response.EnsureSuccessStatusCode();
        var txs = await response.Content
                                .ReadFromJsonAsync<List<TransactionResponse>>(JsonOptions);
        Assert.NotNull(txs);
        Assert.Empty(txs!);
    }

    [Fact]
    public async Task GetTransactions_AfterTopUp_ContainsTopUpEntry()
    {
        await ResetDatabaseAsync();
        var (token, _) = await RegisterAndLoginAsync("txafter@example.com");
        var http       = AuthenticatedClient(token);

        await http.PostAsJsonAsync("/api/v1/wallets/topup",
            new { Amount = 150m, Description = "Paycheck" });

        var response = await http.GetAsync("/api/v1/wallets/transactions");

        response.EnsureSuccessStatusCode();
        var txs = await response.Content
                                .ReadFromJsonAsync<List<TransactionResponse>>(JsonOptions);
        Assert.NotNull(txs);
        Assert.Single(txs!);

        var tx = txs![0];
        Assert.Equal("TopUp", tx.Type);
        Assert.Equal("Completed", tx.Status);
        Assert.Equal(150m, tx.Amount);
        Assert.Equal("Paycheck", tx.Description);
        Assert.False(string.IsNullOrWhiteSpace(tx.Reference));
    }

    [Fact]
    public async Task GetTransactions_AfterTransfer_ContainsDebitEntry()
    {
        await ResetDatabaseAsync();

        var (senderToken, _)    = await RegisterAndLoginAsync("txsnd@example.com");
        var senderHttp          = AuthenticatedClient(senderToken);
        await senderHttp.PostAsJsonAsync("/api/v1/wallets/topup", new { Amount = 300m });

        var (recipientToken, _) = await RegisterAndLoginAsync("txrcv@example.com");
        var recipientHttp       = AuthenticatedClient(recipientToken);
        var recipientWallet     = await (await recipientHttp.GetAsync("/api/v1/wallets"))
                                        .Content.ReadFromJsonAsync<WalletResponse>(JsonOptions);

        await senderHttp.PostAsJsonAsync("/api/v1/wallets/transfer", new
        {
            RecipientWalletId = recipientWallet!.Id,
            Amount            = 100m,
            Description       = "Rent"
        });

        var response = await senderHttp.GetAsync("/api/v1/wallets/transactions");
        response.EnsureSuccessStatusCode();
        var txs = await response.Content
                                .ReadFromJsonAsync<List<TransactionResponse>>(JsonOptions);

        Assert.NotNull(txs);
        Assert.Equal(2, txs!.Count);

        var transfer = txs.First(t => t.Type == "Transfer");
        Assert.Equal("Completed", transfer.Status);
        Assert.Equal(100m, transfer.Amount);
        Assert.False(string.IsNullOrWhiteSpace(transfer.TransactionGroupId));
    }

    [Fact]
    public async Task GetTransactions_RecipientSeesIncomingEntry()
    {
        await ResetDatabaseAsync();

        var (senderToken, _)    = await RegisterAndLoginAsync("txsndr2@example.com");
        var senderHttp          = AuthenticatedClient(senderToken);
        await senderHttp.PostAsJsonAsync("/api/v1/wallets/topup", new { Amount = 500m });

        var (recipientToken, _) = await RegisterAndLoginAsync("txrcvr2@example.com");
        var recipientHttp       = AuthenticatedClient(recipientToken);
        var recipientWallet     = await (await recipientHttp.GetAsync("/api/v1/wallets"))
                                        .Content.ReadFromJsonAsync<WalletResponse>(JsonOptions);

        await senderHttp.PostAsJsonAsync("/api/v1/wallets/transfer", new
        {
            RecipientWalletId = recipientWallet!.Id,
            Amount            = 250m
        });

        var response = await recipientHttp.GetAsync("/api/v1/wallets/transactions");
        response.EnsureSuccessStatusCode();
        var txs = await response.Content
                                .ReadFromJsonAsync<List<TransactionResponse>>(JsonOptions);

        Assert.NotNull(txs);
        Assert.Single(txs!);
        Assert.Equal("Transfer", txs![0].Type);
        Assert.Equal(250m, txs[0].Amount);
    }

    [Fact]
    public async Task GetTransactions_DefaultPagination_ReturnsAtMost20Items()
    {
        await ResetDatabaseAsync();
        var (token, _) = await RegisterAndLoginAsync("paged@example.com");
        var http       = AuthenticatedClient(token);
        
        for (var i = 0; i < 25; i++)
            await http.PostAsJsonAsync("/api/v1/wallets/topup", new { Amount = 1m });

        var response = await http.GetAsync("/api/v1/wallets/transactions");
        response.EnsureSuccessStatusCode();
        var txs = await response.Content
                                .ReadFromJsonAsync<List<TransactionResponse>>(JsonOptions);

        Assert.NotNull(txs);
        Assert.Equal(20, txs!.Count);
    }

    [Fact]
    public async Task GetTransactions_Page2_ReturnsNextBatch()
    {
        await ResetDatabaseAsync();
        var (token, _) = await RegisterAndLoginAsync("paged2@example.com");
        var http       = AuthenticatedClient(token);

        for (var i = 0; i < 25; i++)
            await http.PostAsJsonAsync("/api/v1/wallets/topup", new { Amount = 1m });

        var page1Response = await http.GetAsync("/api/v1/wallets/transactions?page=1&pageSize=10");
        var page2Response = await http.GetAsync("/api/v1/wallets/transactions?page=2&pageSize=10");

        page1Response.EnsureSuccessStatusCode();
        page2Response.EnsureSuccessStatusCode();

        var page1 = await page1Response.Content
                                       .ReadFromJsonAsync<List<TransactionResponse>>(JsonOptions);
        var page2 = await page2Response.Content
                                       .ReadFromJsonAsync<List<TransactionResponse>>(JsonOptions);

        Assert.Equal(10, page1!.Count);
        Assert.Equal(10, page2!.Count);

        // Pages must not overlap
        var page1Ids = page1.Select(t => t.Id).ToHashSet();
        Assert.DoesNotContain(page2, t => page1Ids.Contains(t.Id));
    }

    [Fact]
    public async Task GetTransactions_OrderedDescendingByDate()
    {
        await ResetDatabaseAsync();
        var (token, _) = await RegisterAndLoginAsync("ordered@example.com");
        var http       = AuthenticatedClient(token);

        await http.PostAsJsonAsync("/api/v1/wallets/topup", new { Amount = 10m });
        await http.PostAsJsonAsync("/api/v1/wallets/topup", new { Amount = 20m });
        await http.PostAsJsonAsync("/api/v1/wallets/topup", new { Amount = 30m });

        var response = await http.GetAsync("/api/v1/wallets/transactions");
        response.EnsureSuccessStatusCode();
        var txs = await response.Content
                                .ReadFromJsonAsync<List<TransactionResponse>>(JsonOptions);

        Assert.NotNull(txs);
        for (var i = 0; i < txs!.Count - 1; i++)
            Assert.True(txs[i].CreatedAt >= txs[i + 1].CreatedAt,
                "Transactions should be ordered from newest to oldest.");
    }

    [Fact]
    public async Task GetTransactions_Unauthenticated_Returns401()
    {
        await ResetDatabaseAsync();

        var response = await Client.GetAsync("/api/v1/wallets/transactions");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetTransactions_IsolatedPerUser()
    {
        await ResetDatabaseAsync();

        var (tokenA, _) = await RegisterAndLoginAsync("userA@example.com");
        var (tokenB, _) = await RegisterAndLoginAsync("userB@example.com");

        var httpA = AuthenticatedClient(tokenA);
        var httpB = AuthenticatedClient(tokenB);

        await httpA.PostAsJsonAsync("/api/v1/wallets/topup", new { Amount = 99m });

        var responseB = await httpB.GetAsync("/api/v1/wallets/transactions");
        responseB.EnsureSuccessStatusCode();
        var txsB = await responseB.Content
                                  .ReadFromJsonAsync<List<TransactionResponse>>(JsonOptions);

        Assert.NotNull(txsB);
        Assert.Empty(txsB!);
    }
}