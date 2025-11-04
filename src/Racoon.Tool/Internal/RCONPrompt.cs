using System.Text;
using Spectre.Console;

using RCONStatus = Racoon.Parsers.Standard.Status;

namespace Racoon.Tool.Internal;

internal sealed class RCONPrompt( RCONStatus status, int capacity = 1024 ) : IDisposable, IPrompt<string>
{
    public bool Error { get; set; }

    private readonly List<string> history = new( capacity );

    public void Dispose( ) => history.Clear();

    public string Show( IAnsiConsole console ) => ShowAsync( console, CancellationToken.None ).GetAwaiter().GetResult();

    public async Task<string> ShowAsync( IAnsiConsole console, CancellationToken cancellation )
    {
        WritePrompt( console );
        var value = await console.RunExclusive( async ( ) =>
        {
            var position = history.Count;
            var text = new StringBuilder();
            while( true )
            {
                cancellation.ThrowIfCancellationRequested();

                var key = await console.Input.ReadKeyAsync( true, cancellation );
                if( !key.HasValue )
                {
                    continue;
                }

                if( key.Value.Key is ConsoleKey.Enter )
                {
                    if( text.Length is 0 )
                    {
                        continue;
                    }

                    console.WriteLine();
                    return text.ToString();
                }

                if( history.Count is not 0 && key.Value.Key is ConsoleKey.UpArrow )
                {
                    var value = history[ position = Math.Max( --position, 0 ) ];
                    console.Write( string.Concat(
                        Enumerable.Range( 0, text.Length )
                            .Select( _ => "\b \b" ) ) );

                    text = text.Clear().Append( value );
                    console.Write( value );

                    continue;
                }

                if( history.Count is not 0 && key.Value.Key is ConsoleKey.DownArrow )
                {
                    var value = (position = Math.Min( ++position, history.Count )) == history.Count
                        ? string.Empty
                        : history[ position ];

                    console.Write(
                        string.Concat( Enumerable.Range( 0, text.Length ).Select( _ => "\b \b" ) ) );

                    text = text.Clear().Append( value );
                    console.Write( value );
                }

                if( key.Value.Key is ConsoleKey.Backspace )
                {
                    if( text.Length > 0 )
                    {
                        text = text.Remove( text.Length - 1, 1 );
                        console.Write( "\b \b" );
                    }

                    continue;
                }

                var character = key.Value.KeyChar;
                if( !char.IsControl( character ) )
                {
                    text = text.Append( character );
                    console.Write( character.ToString() );

                    continue;
                }
            }
        } );

        if( string.IsNullOrWhiteSpace( value ) )
        {
            return await ShowAsync( console, cancellation );
        }

        return value.Trim();
    }

    public void AddHistory( string command )
    {
        ArgumentException.ThrowIfNullOrEmpty( command );

        if( history.Count == history.Capacity )
        {
            history.RemoveAt( 0 );
        }

        history.Add( command );
    }

    public void Reset( )
    {
        history.Clear();
        Error = false;
    }

    private void WritePrompt( IAnsiConsole console ) => console.Write( new Markup( $"[bold {(Error ? RCONColor.Error : RCONColor.Success)}]{(Error ? '!' : '@')}[/] [bold]{status.Hostname}[/]> " ) );
}