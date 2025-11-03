using System.ComponentModel;
using System.Globalization;
using System.Reflection;

namespace Racoon.Tool.Internal;

[ImmutableObject( true )]
internal sealed record class ToolVersion : IComparable<ToolVersion>
{
    /// <summary> A reference to the current version of the application. </summary>
    public static readonly ToolVersion Current = FromAssembly( typeof( ToolVersion ).Assembly );

    /// <summary> The major version. </summary>
    public int Major { get; }

    /// <summary> The minor version. </summary>
    public int Minor { get; }

    /// <summary> The patch, or hotfix, version. </summary>
    public int Patch { get; }

    /// <summary> The height of a pre-release in relation to the version. </summary>
    public float? Candidate { get; }

    /// <summary> Additional versioning metadata for tracking. </summary>
    public string? Metadata { get; }

    public ToolVersion( int major, int minor, int patch, float? candidate = null, string? metadata = null )
    {
        Major = major;
        Minor = minor;
        Patch = patch;
        Candidate = candidate;
        Metadata = metadata;
    }

    /// <inheritdoc/>
    public int CompareTo( ToolVersion? other )
    {
        if( other is null )
        {
            return 1;
        }

        var major = Major.CompareTo( other.Major );
        if( major is not 0 )
        {
            return major;
        }

        var minor = Minor.CompareTo( other.Minor );
        if( minor is not 0 )
        {
            return minor;
        }

        var patch = Patch.CompareTo( other.Patch );
        if( patch is not 0 )
        {
            return patch;
        }

        if( Candidate.HasValue && other.Candidate.HasValue )
        {
            return Candidate.Value.CompareTo( other.Candidate.Value );
        }
        else if( Candidate.HasValue )
        {
            return -1;
        }
        else if( other.Candidate.HasValue )
        {
            return 1;
        }

        return 0;
    }

    public static ToolVersion FromAssembly( Assembly assembly )
    {
        ArgumentNullException.ThrowIfNull( assembly );

        float? candidate = default;
        string? metadata = default;

        var informational = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        if( !string.IsNullOrEmpty( informational ) )
        {
            var index = informational.IndexOf( '-' );
            if( index > 0 )
            {
                var end = informational.IndexOf( '+' );
                if( end < 0 )
                {
                    end = informational.Length;
                }

                index += 4;
                candidate = float.Parse( informational[ index..end ], CultureInfo.InvariantCulture );
            }

            index = informational.IndexOf( '+' );
            if( index > 0 )
            {
                metadata = informational[ (index + 1)..(index + 8) ];
            }
        }

        var version = assembly.GetName().Version!;
        return new( version.Major, version.Minor, version.Build, candidate, metadata );
    }

    public static ToolVersion Parse( string version )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace( version );

        var major = 0;
        var minor = 0;
        var patch = 0;
        float? candidate = default;
        string? metadata = default;

        var main = version;
        var index = version.IndexOf( '+' );
        if( index > 0 )
        {
            metadata = version[ (index + 1).. ];
            main = version[ ..index ];
        }

        index = main.IndexOf( '-' );
        if( index > 0 )
        {
            var end = main.IndexOf( '+' );
            if( end < 0 )
            {
                end = main.Length;
            }

            index += 4;
            candidate = float.Parse( main[ index..end ], CultureInfo.InvariantCulture );
            main = main[ ..(index - 4) ];
        }

        var segments = main.Split( '.' );
        if( segments.Length > 0 )
        {
            major = int.Parse( segments[ 0 ], CultureInfo.InvariantCulture );
        }

        if( segments.Length > 1 )
        {
            minor = int.Parse( segments[ 1 ], CultureInfo.InvariantCulture );
        }

        if( segments.Length > 2 )
        {
            patch = int.Parse( segments[ 2 ], CultureInfo.InvariantCulture );
        }

        return new( major, minor, patch, candidate, metadata );
    }

    /// <inheritdoc/>
    public override string ToString( )
    {
        var version = $"{Major}.{Minor}.{Patch}";
        if( Candidate.HasValue )
        {
            version += $"-rc.{Candidate:#.##}";
        }

        if( !string.IsNullOrEmpty( Metadata ) )
        {
            version += $"+{Metadata}";
        }

        return version;
    }

    /// <inheritdoc/>
    public static implicit operator string( ToolVersion version ) => version.ToString();

    /// <inheritdoc/>
    public static bool operator <( ToolVersion left, ToolVersion right ) => left.CompareTo( right ) < 0;

    /// <inheritdoc/>
    public static bool operator >( ToolVersion left, ToolVersion right ) => left.CompareTo( right ) > 0;

    /// <inheritdoc/>
    public static bool operator <=( ToolVersion left, ToolVersion right ) => left.CompareTo( right ) <= 0;

    /// <inheritdoc/>
    public static bool operator >=( ToolVersion left, ToolVersion right ) => left.CompareTo( right ) >= 0;
}