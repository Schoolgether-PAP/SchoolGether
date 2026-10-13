using System.Net;
using SchoolGether.Contracts.Identity;

namespace SchoolGether.ApiClient.Identity;

public sealed class IdentitySession(IdentityApiClient api, IRefreshTokenStore store, TimeProvider timeProvider)
{
    // O bloqueio local protege armazenamento e estado; nunca abrange pedidos HTTP.
    private readonly SemaphoreSlim stateGate = new(1, 1);
    private readonly SemaphoreSlim refreshGate = new(1, 1);
    private long generation;
    private TokenResponse? tokens;
    private string? refreshToken;
    private AccountResponse? account;

    public AccountResponse? Account => Volatile.Read(ref account);
    public event Action? Changed;

    public async Task<AccountResponse> SignInAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        var (currentGeneration, _) = await ClearAsync(null, cancellationToken);
        TokenResponse response;
        try
        {
            response = await api.LoginAsync(request, cancellationToken);
        }
        catch (IdentityApiException exception) when (IsAuthenticationRejected(exception))
        {
            throw new SessionExpiredException();
        }
        await SaveTokensAsync(currentGeneration, response, cancellationToken);
        return await LoadProfileAsync(currentGeneration, cancellationToken);
    }

    public async Task<AccountResponse?> RestoreAsync(CancellationToken cancellationToken = default)
    {
        long currentGeneration;
        await stateGate.WaitAsync(cancellationToken);
        try
        {
            currentGeneration = generation;
            refreshToken ??= await store.ReadAsync(cancellationToken);
            if (refreshToken is null) return null;
        }
        finally
        {
            stateGate.Release();
        }

        return await LoadProfileAsync(currentGeneration, cancellationToken);
    }

    public async Task<AccountResponse> GetProfileAsync(CancellationToken cancellationToken = default)
    {
        var state = await ReadStateAsync(cancellationToken);
        return await LoadProfileAsync(state.Generation, cancellationToken);
    }

    public async Task<T> ExecuteAuthorizedAsync<T>(Func<string, CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);
        var state = await ReadStateAsync(cancellationToken);
        T result;
        try
        {
            var currentTokens = await EnsureTokensAsync(state.Generation, null, cancellationToken);
            try
            {
                result = await operation(currentTokens.AccessToken, cancellationToken);
            }
            catch (IdentityApiException exception) when (exception.StatusCode == HttpStatusCode.Unauthorized)
            {
                currentTokens = await EnsureTokensAsync(state.Generation, currentTokens, cancellationToken);
                try
                {
                    result = await operation(currentTokens.AccessToken, cancellationToken);
                }
                catch (IdentityApiException retryException) when (retryException.StatusCode == HttpStatusCode.Unauthorized)
                {
                    await ClearAsync(state.Generation, CancellationToken.None);
                    throw new SessionExpiredException();
                }
            }
        }
        catch
        {
            if ((await ReadStateAsync(CancellationToken.None)).Generation != state.Generation)
                throw new SessionExpiredException();
            throw;
        }

        await stateGate.WaitAsync(cancellationToken);
        try
        {
            EnsureGeneration(state.Generation);
            return result;
        }
        finally
        {
            stateGate.Release();
        }
    }

    public async Task<bool> SignOutAsync(CancellationToken cancellationToken = default)
    {
        // Sair limpa a sessão local mesmo quando o pedido remoto é cancelado.
        var (_, previousRefresh) = await ClearAsync(null, CancellationToken.None, readSavedToken: true);
        if (previousRefresh is null) return false;
        try
        {
            await api.LogoutAsync(new RefreshRequest(previousRefresh), cancellationToken);
            return true;
        }
        catch (HttpRequestException)
        {
            return false;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }

    private async Task<AccountResponse> LoadProfileAsync(long currentGeneration, CancellationToken cancellationToken)
    {
        var currentTokens = await EnsureTokensAsync(currentGeneration, null, cancellationToken);
        AccountResponse profile;
        try
        {
            profile = await api.GetMeAsync(currentTokens.AccessToken, cancellationToken);
        }
        catch (IdentityApiException exception) when (exception.StatusCode == HttpStatusCode.Unauthorized)
        {
            currentTokens = await EnsureTokensAsync(currentGeneration, currentTokens, cancellationToken);
            try
            {
                profile = await api.GetMeAsync(currentTokens.AccessToken, cancellationToken);
            }
            catch (IdentityApiException retryException) when (IsAuthenticationRejected(retryException))
            {
                await ClearAsync(currentGeneration, CancellationToken.None);
                throw new SessionExpiredException();
            }
        }
        catch (IdentityApiException exception) when (IsAuthenticationRejected(exception))
        {
            await ClearAsync(currentGeneration, CancellationToken.None);
            throw new SessionExpiredException();
        }

        await stateGate.WaitAsync(cancellationToken);
        try
        {
            EnsureGeneration(currentGeneration);
            Volatile.Write(ref account, profile);
        }
        finally
        {
            stateGate.Release();
        }
        Changed?.Invoke();
        return profile;
    }

    private async Task<TokenResponse> EnsureTokensAsync(long currentGeneration, TokenResponse? rejectedTokens,
        CancellationToken cancellationToken)
    {
        await refreshGate.WaitAsync(cancellationToken);
        try
        {
            var state = await ReadStateAsync(cancellationToken);
            if (state.Generation != currentGeneration) throw new SessionExpiredException();
            if (state.Tokens is not null && state.Tokens.AccessTokenExpiresAt > timeProvider.GetUtcNow().UtcDateTime
                && !ReferenceEquals(state.Tokens, rejectedTokens))
                return state.Tokens;
            if (state.RefreshToken is null || (state.Tokens is not null
                && state.Tokens.RefreshTokenExpiresAt <= timeProvider.GetUtcNow().UtcDateTime))
            {
                await ClearAsync(currentGeneration, CancellationToken.None);
                throw new SessionExpiredException();
            }

            TokenResponse response;
            try
            {
                response = await api.RefreshAsync(new RefreshRequest(state.RefreshToken), cancellationToken);
            }
            catch (IdentityApiException exception) when (IsAuthenticationRejected(exception))
            {
                await ClearAsync(currentGeneration, CancellationToken.None);
                throw new SessionExpiredException();
            }
            await SaveTokensAsync(currentGeneration, response, cancellationToken);
            return response;
        }
        finally
        {
            refreshGate.Release();
        }
    }

    private async Task SaveTokensAsync(long currentGeneration, TokenResponse response, CancellationToken cancellationToken)
    {
        var cleared = false;
        await stateGate.WaitAsync(cancellationToken);
        try
        {
            EnsureGeneration(currentGeneration);
            try
            {
                await store.WriteAsync(response.RefreshToken, cancellationToken);
                tokens = response;
                refreshToken = response.RefreshToken;
            }
            catch
            {
                generation++;
                tokens = null;
                refreshToken = null;
                Volatile.Write(ref account, null);
                cleared = true;
                await store.WriteAsync(null, CancellationToken.None);
                throw;
            }
        }
        finally
        {
            stateGate.Release();
            if (cleared) Changed?.Invoke();
        }
    }

    private async Task<(long Generation, string? RefreshToken)> ClearAsync(long? expectedGeneration,
        CancellationToken cancellationToken, bool readSavedToken = false)
    {
        var cleared = false;
        await stateGate.WaitAsync(cancellationToken);
        try
        {
            if (expectedGeneration is not null && generation != expectedGeneration) return (generation, null);
            generation++;
            var previousRefresh = refreshToken;
            tokens = null;
            refreshToken = null;
            Volatile.Write(ref account, null);
            cleared = true;
            try
            {
                if (readSavedToken) previousRefresh ??= await store.ReadAsync(CancellationToken.None);
            }
            finally
            {
                await store.WriteAsync(null, CancellationToken.None);
            }
            return (generation, previousRefresh);
        }
        finally
        {
            stateGate.Release();
            if (cleared) Changed?.Invoke();
        }
    }

    private async Task<(long Generation, TokenResponse? Tokens, string? RefreshToken)> ReadStateAsync(
        CancellationToken cancellationToken)
    {
        await stateGate.WaitAsync(cancellationToken);
        try
        {
            return (generation, tokens, refreshToken);
        }
        finally
        {
            stateGate.Release();
        }
    }

    private void EnsureGeneration(long currentGeneration)
    {
        if (generation != currentGeneration) throw new SessionExpiredException();
    }

    private static bool IsAuthenticationRejected(IdentityApiException exception)
        => exception.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden;
}
