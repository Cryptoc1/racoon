using GitCredentialManager;
using Microsoft.Extensions.DependencyInjection;
using Racoon.Tool.Commands;
using Racoon.Tool.Interceptors;
using Racoon.Tool.Internal;
using Spectre.Console.Cli;

var services = new ServiceCollection()
    .AddSingleton<ICommandInterceptor, NoLogoInterceptor>()
    .AddSingleton( _ => CredentialManager.Create( typeof( Program ).FullName ) );

var app = new CommandApp<ConnectCommand>( new TypeRegistrar( services ) )
    .WithDescription( "Connect to an RCON server." );

app.Configure( options =>
{
    options.AddBranch( "creds", creds =>
    {
        creds.AddCommand<ClearCredentialsCommand>( "clear" )
            .WithDescription( "Clear all saved credentials." );

        creds.AddCommand<ListCredentialsCommand>( "list" )
            .WithAlias( "ls" )
            .WithDescription( "List saved credentials." );

        creds.AddCommand<RemoveCredentialCommand>( "remove" )
            .WithAlias( "rm" )
            .WithDescription( "Remove a saved credential." );

        creds.SetDefaultCommand<ListCredentialsCommand>();
        creds.SetDescription( "Manage saved credentials." );
    } );

    options.SetApplicationName( "racoon" );
    options.SetApplicationVersion( ToolVersion.Current );

    options.PropagateExceptions();
} );

return await app.RunAsync( args );

sealed file class TypeRegistrar( IServiceCollection services ) : ITypeRegistrar
{
    public ITypeResolver Build( ) => new TypeResolver( services.BuildServiceProvider() );

#pragma warning disable IL2067
    public void Register( Type service, Type implementation ) => services.AddSingleton( service, implementation );
#pragma warning restore IL2067

    public void RegisterInstance( Type service, object implementation ) => services.AddSingleton( service, implementation );

    public void RegisterLazy( Type service, Func<object> func )
    {
        ArgumentNullException.ThrowIfNull( func );
        services.AddSingleton( service, _ => func() );
    }
}

sealed file partial class TypeResolver( IServiceProvider services ) : IAsyncDisposable, ITypeResolver
{
    public object? Resolve( Type? type )
    {
        if( type is null )
        {
            return null;
        }

        return services.GetService( type );
    }

    public async ValueTask DisposeAsync( )
    {
        if( services is IAsyncDisposable async )
        {
            await async.DisposeAsync();
        }
        else if( services is IDisposable disposable )
        {
            disposable.Dispose();
        }
    }
}