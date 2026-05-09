using System;
using System.Text;

namespace Sandbox;

public static class ConnecterPathUtility
{
	public static string NormalizeDirectoryPath( string path )
	{
		if ( string.IsNullOrWhiteSpace( path ) )
			return string.Empty;

		return path.Trim().TrimEnd( '\\', '/' );
	}

	public static string NormalizeAssetPath( string path )
	{
		return path?.Replace( '\\', '/' );
	}

	public static string GetFileName( string path )
	{
		if ( string.IsNullOrWhiteSpace( path ) )
			return string.Empty;

		var normalized = NormalizeDirectoryPath( path );
		var index = normalized.LastIndexOfAny( ['\\', '/'] );

		return index >= 0 ? normalized[(index + 1)..] : normalized;
	}

	public static string GetFileNameWithoutExtension( string path )
	{
		var name = GetFileName( path );
		var index = name.LastIndexOf( '.' );

		return index > 0 ? name[..index] : name;
	}

	public static string GetDirectoryName( string path )
	{
		if ( string.IsNullOrWhiteSpace( path ) )
			return string.Empty;

		var normalized = NormalizeDirectoryPath( path );
		var index = normalized.LastIndexOfAny( ['\\', '/'] );

		return index > 0 ? normalized[..index] : string.Empty;
	}

	public static string SanitizePathSegment( string value )
	{
		if ( string.IsNullOrWhiteSpace( value ) )
			return "Unknown";

		var builder = new StringBuilder( value.Length );
		foreach ( var ch in value )
		{
			builder.Append( IsInvalidPathSegmentCharacter( ch ) ? '_' : ch );
		}

		var result = builder.ToString().Trim();
		return string.IsNullOrWhiteSpace( result ) ? "Unknown" : result;
	}

	public static string GetRelativePath( string rootPath, string fullPath )
	{
		if ( string.IsNullOrWhiteSpace( rootPath ) || string.IsNullOrWhiteSpace( fullPath ) )
			return string.Empty;

		var root = NormalizeAssetPath( NormalizeDirectoryPath( rootPath ) )?.TrimEnd( '/' );
		var full = NormalizeAssetPath( NormalizeDirectoryPath( fullPath ) );

		if ( string.Equals( root, full, StringComparison.OrdinalIgnoreCase ) )
			return ".";

		if ( !string.IsNullOrWhiteSpace( root ) && full.StartsWith( root + "/", StringComparison.OrdinalIgnoreCase ) )
			return full[(root.Length + 1)..];

		return full;
	}

	public static bool IsPathInside( string rootPath, string fullPath )
	{
		if ( string.IsNullOrWhiteSpace( rootPath ) || string.IsNullOrWhiteSpace( fullPath ) )
			return false;

		var root = NormalizeAssetPath( NormalizeDirectoryPath( rootPath ) )?.TrimEnd( '/' );
		var full = NormalizeAssetPath( NormalizeDirectoryPath( fullPath ) );

		return string.Equals( root, full, StringComparison.OrdinalIgnoreCase )
			|| (!string.IsNullOrWhiteSpace( root ) && full.StartsWith( root + "/", StringComparison.OrdinalIgnoreCase ));
	}

	public static string GetImportDirectory( ConnecterAssetRecord record, ConnecterImportOptions options )
	{
		var projectAssetsPath = NormalizeDirectoryPath( options.ProjectAssetsPath );
		var importRoot = SanitizePathSegment( options.ImportRootName );
		var repositoryName = SanitizePathSegment( record.RepositoryName );

		var sourceDirectory = record.IsDirectory
			? GetDirectoryName( record.FullPath )
			: GetDirectoryName( record.FullPath );

		var relativeDirectory = GetRelativePath( record.RepositoryRoot, sourceDirectory ?? record.RepositoryRoot );
		var destination = JoinPath( projectAssetsPath, importRoot, repositoryName );

		if ( !string.IsNullOrWhiteSpace( relativeDirectory ) && relativeDirectory != "." )
		{
			foreach ( var segment in relativeDirectory.Split( '/', StringSplitOptions.RemoveEmptyEntries ) )
			{
				destination = JoinPath( destination, SanitizePathSegment( segment ) );
			}
		}

		return destination;
	}

	public static string GetAssetRelativePath( string projectAssetsPath, string absolutePath )
	{
		return GetRelativePath( projectAssetsPath, absolutePath );
	}

	private static string JoinPath( params string[] segments )
	{
		var builder = new StringBuilder();

		foreach ( var segment in segments )
		{
			if ( string.IsNullOrWhiteSpace( segment ) )
				continue;

			if ( builder.Length == 0 )
			{
				builder.Append( NormalizeDirectoryPath( segment ) );
				continue;
			}

			builder.Append( '\\' );
			builder.Append( segment.Trim( '\\', '/' ) );
		}

		return builder.ToString();
	}

	private static bool IsInvalidPathSegmentCharacter( char ch )
	{
		return ch < 32 || ch is '<' or '>' or ':' or '"' or '/' or '\\' or '|' or '?' or '*';
	}
}
