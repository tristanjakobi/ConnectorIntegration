using Sandbox;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Editor;

public static class ConnecterWorkspaceReader
{
	public const string DefaultWorkspacePath = @"E:\Game Assets\Connecter";

	private static readonly Regex WindowsPathRegex = new( @"[A-Za-z]:\\[^""\x00-\x1F<>|?*]+", RegexOptions.Compiled );
	private static readonly Regex SettingsRepositoryRegex = new(
		@"<setting\b[^>]*\bkey\s*=\s*[""']2007[""'][^>]*>(?<value>.*?)</setting>",
		RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline );

	public static ConnecterWorkspace Read( string workspacePath = DefaultWorkspacePath )
	{
		workspacePath = NormalizeFullPath( workspacePath );

		var repositories = new List<ConnecterRepository>();
		repositories.AddRange( ReadRepositoriesFromDatabase( Path.Combine( workspacePath, "default.dcdb" ) ) );

		if ( repositories.Count == 0 )
		{
			repositories.AddRange( ReadRepositoriesFromSettingsXml( Path.Combine( workspacePath, "settings.xml" ) ) );
		}

		return new ConnecterWorkspace( workspacePath, DeduplicateRepositories( repositories ) );
	}

	public static IReadOnlyList<ConnecterRepository> ReadRepositoriesFromSettingsXml( string settingsPath )
	{
		if ( !File.Exists( settingsPath ) )
			return [];

		var text = File.ReadAllText( settingsPath );
		var match = SettingsRepositoryRegex.Match( text );

		if ( !match.Success )
			return [];

		var value = WebUtility.HtmlDecode( match.Groups["value"].Value.Trim() );
		if ( string.IsNullOrWhiteSpace( value ) )
			return [];

		try
		{
			var paths = JsonSerializer.Deserialize<List<string>>( value ) ?? [];
			return paths
				.Select( NormalizeFullPath )
				.Where( Directory.Exists )
				.Select( ConnecterRepository.FromPath )
				.ToList();
		}
		catch
		{
			return [];
		}
	}

	public static IReadOnlyList<ConnecterRepository> ReadRepositoriesFromDatabase( string databasePath )
	{
		if ( !File.Exists( databasePath ) )
			return [];

		// s&box does not ship a managed SQLite provider. Connecter stores repository
		// paths as plain text in default.dcdb, so this read-only scan is enough for
		// v1 root discovery without mutating or locking the workspace database.
		var bytes = File.ReadAllBytes( databasePath );
		var text = Encoding.UTF8.GetString( bytes );

		return WindowsPathRegex.Matches( text )
			.Select( x => NormalizeFullPath( x.Value.Trim().TrimEnd( '\\', '/', '\0' ) ) )
			.Where( Directory.Exists )
			.Select( ConnecterRepository.FromPath )
			.ToList();
	}

	private static IReadOnlyList<ConnecterRepository> DeduplicateRepositories( IEnumerable<ConnecterRepository> repositories )
	{
		var distinct = repositories
			.Where( x => !string.IsNullOrWhiteSpace( x.FullPath ) )
			.GroupBy( x => ConnecterPathUtility.NormalizeDirectoryPath( x.FullPath ), StringComparer.OrdinalIgnoreCase )
			.Select( x => x.First() )
			.OrderBy( x => x.Name, StringComparer.OrdinalIgnoreCase )
			.ToList();

		return distinct
			.Where( repository => !distinct.Any( candidate =>
				!ReferenceEquals( candidate, repository )
				&& ConnecterPathUtility.IsPathInside( candidate.FullPath, repository.FullPath ) ) )
			.ToList();
	}

	private static string NormalizeFullPath( string path )
	{
		if ( string.IsNullOrWhiteSpace( path ) )
			return string.Empty;

		return Path.GetFullPath( path.Trim().TrimEnd( Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar ) );
	}
}
