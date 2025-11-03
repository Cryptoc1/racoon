using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;
using GitCredentialManager;

namespace Racoon.Tool.Internal;

internal static class ICredentialStoreExtensions
{
    private const string ServiceName = "rcon://rcon.tool";

    public static IEnumerable<RacoonAccount> GetRacoonAccounts( this ICredentialStore store )
    {
        ArgumentNullException.ThrowIfNull( store );

        foreach( var host in store.GetAccounts( ServiceName ) )
        {
            if( !TryParseCredential( store, host, out var credential ) )
            {
                continue;
            }

            yield return credential;
        }
    }

    public static bool RemoveRacoon( this ICredentialStore store, string host )
    {
        ArgumentNullException.ThrowIfNull( store );
        ArgumentException.ThrowIfNullOrWhiteSpace( host );

        return store.Remove( ServiceName, host );
    }

    public static void UpdateRacoonPassword( this ICredentialStore store, string host, string password )
    {
        ArgumentNullException.ThrowIfNull( store );
        ArgumentException.ThrowIfNullOrEmpty( host );
        ArgumentException.ThrowIfNullOrEmpty( password );

        if( TryParseCredential( store, host, out var credential ) )
        {
            store.AddOrUpdate(
                ServiceName,
                host,
                JsonSerializer.Serialize( credential with
                {
                    Password = password,
                    UpdatedAt = DateTimeOffset.UtcNow,
                }, CredentialJsonContext.Default.RacoonCredential ) );

            return;
        }

        store.AddOrUpdate(
            ServiceName,
            host,
            JsonSerializer.Serialize( new RacoonCredential( DateTimeOffset.UtcNow, host, password ), CredentialJsonContext.Default.RacoonCredential ) );
    }

    public static bool TryGetRacoonPassword( this ICredentialStore store, string host, [NotNullWhen( true )] out string? password )
    {
        ArgumentNullException.ThrowIfNull( store );
        ArgumentException.ThrowIfNullOrWhiteSpace( host );

        if( !TryParseCredential( store, host, out var credential ) )
        {
            password = default;
            return false;
        }

        password = credential.Password;
        return true;
    }

    private static bool TryParseCredential( ICredentialStore store, string host, [NotNullWhen( true )] out RacoonCredential? credential )
    {
        ArgumentNullException.ThrowIfNull( store );
        ArgumentException.ThrowIfNullOrWhiteSpace( host );

        var value = store.Get( ServiceName, host );
        if( value is null )
        {
            credential = default;
            return false;
        }

        try
        {
            if( (credential = JsonSerializer.Deserialize( value.Password, CredentialJsonContext.Default.RacoonCredential )) is null )
            {
                throw new JsonException();
            }

            return true;
        }
        catch( JsonException )
        {
            if( value is not null )
            {
                store.Remove( ServiceName, host );
            }

            credential = default;
            return false;
        }
    }
}

[JsonSerializable( typeof( RacoonCredential ) )]
[JsonSourceGenerationOptions( DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault | JsonIgnoreCondition.WhenWritingNull, WriteIndented = false )]
internal sealed partial class CredentialJsonContext : JsonSerializerContext;

internal sealed record RacoonAccount( DateTimeOffset CreatedAt, string Host, DateTimeOffset? UpdatedAt = default );

internal sealed record RacoonCredential( DateTimeOffset CreatedAt, string Host, string Password = "" )
{
    public DateTimeOffset? UpdatedAt { get; init; }

    public static implicit operator RacoonAccount( RacoonCredential credential ) => new( credential.CreatedAt, credential.Host )
    {
        UpdatedAt = credential.UpdatedAt,
    };
}