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
	private const string SavedWorkspaceFileName = "workspace.txt";

	private static readonly Regex WindowsPathRegex = new( @"[A-Za-z]:\\[^""\x00-\x1F<>|?*]+", RegexOptions.Compiled );
	private static readonly Regex SettingsRepositoryRegex = new(
		@"<setting\b[^>]*\bkey\s*=\s*[""']2007[""'][^>]*>(?<value>.*?)</setting>",
		RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline );

	public static ConnecterWorkspace Read( string workspacePath = null, bool allowAutoDiscover = true )
	{
		workspacePath = ResolveWorkspacePath( workspacePath, allowAutoDiscover );
		if ( string.IsNullOrWhiteSpace( workspacePath ) )
			return new ConnecterWorkspace( string.Empty, [] );

		var repositories = new List<ConnecterRepository>();
		repositories.AddRange( ReadRepositoriesFromDatabase( Path.Combine( workspacePath, "default.dcdb" ) ) );

		if ( repositories.Count == 0 )
		{
			repositories.AddRange( ReadRepositoriesFromSettingsXml( Path.Combine( workspacePath, "settings.xml" ) ) );
		}

		return new ConnecterWorkspace( workspacePath, DeduplicateRepositories( repositories ) );
	}

	public static string GetSavedWorkspacePath()
	{
		try
		{
			var settingsPath = GetSavedWorkspaceFilePath();
			if ( File.Exists( settingsPath ) )
				return File.ReadAllText( settingsPath ).Trim();
		}
		catch
		{
		}

		return string.Empty;
	}

	public static void SetSavedWorkspacePath( string workspacePath )
	{
		try
		{
			var settingsPath = GetSavedWorkspaceFilePath();
			var settingsFolder = Path.GetDirectoryName( settingsPath );
			if ( !string.IsNullOrWhiteSpace( settingsFolder ) )
				Directory.CreateDirectory( settingsFolder );

			File.WriteAllText( settingsPath, NormalizeFullPath( workspacePath ) );
		}
		catch
		{
		}
	}

	public static void ClearSavedWorkspacePath()
	{
		try
		{
			var settingsPath = GetSavedWorkspaceFilePath();
			var settingsFolder = Path.GetDirectoryName( settingsPath );
			if ( !string.IsNullOrWhiteSpace( settingsFolder ) )
				Directory.CreateDirectory( settingsFolder );

			File.WriteAllText( settingsPath, string.Empty );
		}
		catch
		{
		}
	}

	public static IReadOnlyList<string> DiscoverWorkspacePaths( IEnumerable<string> candidatePaths = null )
	{
		return (candidatePaths ?? GetDefaultWorkspaceCandidates())
			.Select( NormalizeFullPath )
			.Where( IsConnecterWorkspacePath )
			.Distinct( StringComparer.OrdinalIgnoreCase )
			.ToList();
	}

	public static bool IsConnecterWorkspacePath( string workspacePath )
	{
		if ( string.IsNullOrWhiteSpace( workspacePath ) )
			return false;

		var normalized = NormalizeFullPath( workspacePath );
		return Directory.Exists( normalized )
			&& (File.Exists( Path.Combine( normalized, "default.dcdb" ) ) || File.Exists( Path.Combine( normalized, "settings.xml" ) ));
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

	private static string ResolveWorkspacePath( string requestedPath, bool allowAutoDiscover )
	{
		if ( !string.IsNullOrWhiteSpace( requestedPath ) )
			return NormalizeFullPath( requestedPath );

		var savedPath = GetSavedWorkspacePath();
		if ( IsConnecterWorkspacePath( savedPath ) )
			return NormalizeFullPath( savedPath );

		if ( allowAutoDiscover )
		{
			var discovered = DiscoverWorkspacePaths().FirstOrDefault();
			if ( !string.IsNullOrWhiteSpace( discovered ) )
				return discovered;
		}

		if ( !string.IsNullOrWhiteSpace( savedPath ) )
			return NormalizeFullPath( savedPath );

		return string.Empty;
	}

	private static IEnumerable<string> GetDefaultWorkspaceCandidates()
	{
		var candidates = new List<string>();

		AddIfNotEmpty( candidates, Environment.GetFolderPath( Environment.SpecialFolder.MyDocuments ), "Connecter" );
		AddIfNotEmpty( candidates, Environment.GetFolderPath( Environment.SpecialFolder.ApplicationData ), "Design Connected", "Connecter" );
		AddIfNotEmpty( candidates, Environment.GetFolderPath( Environment.SpecialFolder.LocalApplicationData ), "Design Connected", "Connecter" );
		AddIfNotEmpty( candidates, Environment.GetFolderPath( Environment.SpecialFolder.ApplicationData ), "Connecter" );
		AddIfNotEmpty( candidates, Environment.GetFolderPath( Environment.SpecialFolder.LocalApplicationData ), "Connecter" );

		foreach ( var drive in DriveInfo.GetDrives().Where( x => x.DriveType == DriveType.Fixed && x.IsReady ) )
		{
			candidates.Add( Path.Combine( drive.RootDirectory.FullName, "Game Assets", "Connecter" ) );
			candidates.Add( Path.Combine( drive.RootDirectory.FullName, "Connecter" ) );
		}

		return candidates;
	}

	private static void AddIfNotEmpty( List<string> candidates, string root, params string[] segments )
	{
		if ( string.IsNullOrWhiteSpace( root ) )
			return;

		var pathSegments = new string[segments.Length + 1];
		pathSegments[0] = root;
		Array.Copy( segments, 0, pathSegments, 1, segments.Length );
		candidates.Add( Path.Combine( pathSegments ) );
	}

	private static string GetSavedWorkspaceFilePath()
	{
		var appData = Environment.GetFolderPath( Environment.SpecialFolder.LocalApplicationData );
		if ( string.IsNullOrWhiteSpace( appData ) )
			appData = AppContext.BaseDirectory;

		return Path.Combine( appData, "sbox", "ConnecterBrowser", SavedWorkspaceFileName );
	}

	private static string NormalizeFullPath( string path )
	{
		if ( string.IsNullOrWhiteSpace( path ) )
			return string.Empty;

		var trimmed = path.Trim().TrimEnd( Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar );
		try
		{
			return Path.GetFullPath( trimmed );
		}
		catch
		{
			return trimmed;
		}
	}
}
