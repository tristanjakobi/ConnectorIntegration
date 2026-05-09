using Sandbox;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Editor;

public enum ConnecterBrowserMode
{
	Files,
	Assets
}

public enum ConnecterBrowserFilter
{
	All,
	Models,
	Materials,
	Images,
	Audio,
	Sbox,
	Unsupported
}

public sealed record ConnecterScanResult( IReadOnlyList<ConnecterAssetRecord> Items, bool Truncated );

public static class ConnecterAssetScanner
{
	private const int MaxSearchResults = 1000;

	public static Task<ConnecterScanResult> ScanAsync( ConnecterRepository repository, string folderPath, string query, ConnecterBrowserFilter filter, CancellationToken token )
	{
		return Task.Run( () => Scan( repository, folderPath, query, filter, token ), token );
	}

	public static Task<ConnecterScanResult> ScanAssetsAsync( IReadOnlyList<ConnecterRepository> repositories, string query, ConnecterBrowserFilter filter, CancellationToken token )
	{
		return Task.Run( () => ScanAssets( repositories, query, filter, token ), token );
	}

	private static ConnecterScanResult Scan( ConnecterRepository repository, string folderPath, string query, ConnecterBrowserFilter filter, CancellationToken token )
	{
		var items = new List<ConnecterAssetRecord>();
		var recursive = !string.IsNullOrWhiteSpace( query );
		var truncated = false;

		if ( string.IsNullOrWhiteSpace( folderPath ) || !Directory.Exists( folderPath ) )
			return new ConnecterScanResult( items, false );

		if ( recursive )
		{
			foreach ( var directory in SafeEnumerateDirectories( folderPath, true ) )
			{
				token.ThrowIfCancellationRequested();
				TryAdd( repository, directory, true, query, filter, items );

				if ( items.Count >= MaxSearchResults )
				{
					truncated = true;
					break;
				}
			}
		}
		else
		{
			foreach ( var directory in SafeEnumerateDirectories( folderPath, false ) )
			{
				token.ThrowIfCancellationRequested();
				TryAdd( repository, directory, true, query, filter, items );
			}
		}

		if ( !truncated )
		{
			foreach ( var file in SafeEnumerateFiles( folderPath, recursive ) )
			{
				token.ThrowIfCancellationRequested();
				TryAdd( repository, file, false, query, filter, items );

				if ( recursive && items.Count >= MaxSearchResults )
				{
					truncated = true;
					break;
				}
			}
		}

		var ordered = items
			.OrderByDescending( x => x.IsDirectory )
			.ThenBy( x => x.Name, StringComparer.OrdinalIgnoreCase )
			.ToList();

		return new ConnecterScanResult( ordered, truncated );
	}

	private static ConnecterScanResult ScanAssets( IReadOnlyList<ConnecterRepository> repositories, string query, ConnecterBrowserFilter filter, CancellationToken token )
	{
		var items = new List<ConnecterAssetRecord>();
		var truncated = false;

		foreach ( var repository in repositories )
		{
			token.ThrowIfCancellationRequested();

			if ( string.IsNullOrWhiteSpace( repository.FullPath ) || !Directory.Exists( repository.FullPath ) )
				continue;

			foreach ( var file in SafeEnumerateFiles( repository.FullPath, true ) )
			{
				token.ThrowIfCancellationRequested();
				TryAddAsset( repository, file, query, filter, items );

				if ( items.Count >= MaxSearchResults )
				{
					truncated = true;
					break;
				}
			}

			if ( truncated )
				break;
		}

		var ordered = items
			.OrderBy( x => GetKindSortOrder( x.Kind ) )
			.ThenBy( x => x.Name, StringComparer.OrdinalIgnoreCase )
			.ThenBy( x => x.RepositoryName, StringComparer.OrdinalIgnoreCase )
			.ToList();

		return new ConnecterScanResult( ordered, truncated );
	}

	private static void TryAdd( ConnecterRepository repository, string path, bool isDirectory, string query, ConnecterBrowserFilter filter, List<ConnecterAssetRecord> items )
	{
		if ( ShouldSkipPath( path ) )
			return;

		var record = ConnecterAssetRecord.FromPath( repository, path, isDirectory );

		if ( !MatchesQuery( record, query ) )
			return;

		if ( !MatchesFilter( record, filter ) )
			return;

		items.Add( record );
	}

	private static void TryAddAsset( ConnecterRepository repository, string path, string query, ConnecterBrowserFilter filter, List<ConnecterAssetRecord> items )
	{
		if ( ShouldSkipPath( path ) )
			return;

		var record = ConnecterAssetRecord.FromPath( repository, path, false );

		if ( !MatchesAssetModeFilter( record, filter ) )
			return;

		if ( !MatchesQuery( record, query ) )
			return;

		items.Add( record );
	}

	private static bool MatchesQuery( ConnecterAssetRecord record, string query )
	{
		if ( string.IsNullOrWhiteSpace( query ) )
			return true;

		foreach ( var part in query.Split( ' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries ) )
		{
			if ( !record.Name.Contains( part, StringComparison.OrdinalIgnoreCase )
				&& !record.RelativePath.Contains( part, StringComparison.OrdinalIgnoreCase )
				&& !record.Kind.ToString().Contains( part, StringComparison.OrdinalIgnoreCase ) )
			{
				return false;
			}
		}

		return true;
	}

	private static bool MatchesFilter( ConnecterAssetRecord record, ConnecterBrowserFilter filter )
	{
		return filter switch
		{
			ConnecterBrowserFilter.Models => record.IsDirectory || record.Kind is ConnecterAssetKind.ModelSource or ConnecterAssetKind.SboxModel,
			ConnecterBrowserFilter.Materials => record.IsDirectory || record.Kind is ConnecterAssetKind.Material,
			ConnecterBrowserFilter.Images => record.IsDirectory || record.Kind is ConnecterAssetKind.Image,
			ConnecterBrowserFilter.Audio => record.IsDirectory || record.Kind is ConnecterAssetKind.Audio,
			ConnecterBrowserFilter.Sbox => record.IsDirectory || record.Kind is ConnecterAssetKind.SboxModel or ConnecterAssetKind.Material,
			ConnecterBrowserFilter.Unsupported => record.Kind is ConnecterAssetKind.Unsupported or ConnecterAssetKind.Unknown,
			_ => true
		};
	}

	private static bool MatchesAssetModeFilter( ConnecterAssetRecord record, ConnecterBrowserFilter filter )
	{
		return filter switch
		{
			ConnecterBrowserFilter.Models => record.Kind is ConnecterAssetKind.ModelSource or ConnecterAssetKind.SboxModel,
			ConnecterBrowserFilter.Materials => record.Kind is ConnecterAssetKind.Material,
			ConnecterBrowserFilter.Images => record.Kind is ConnecterAssetKind.Image,
			ConnecterBrowserFilter.Audio => record.Kind is ConnecterAssetKind.Audio,
			ConnecterBrowserFilter.Sbox => record.Kind is ConnecterAssetKind.SboxModel or ConnecterAssetKind.Material,
			ConnecterBrowserFilter.Unsupported => record.Kind is ConnecterAssetKind.Unsupported or ConnecterAssetKind.Unknown,
			_ => record.CanImport
		};
	}

	private static int GetKindSortOrder( ConnecterAssetKind kind )
	{
		return kind switch
		{
			ConnecterAssetKind.ModelSource or ConnecterAssetKind.SboxModel => 0,
			ConnecterAssetKind.Material => 1,
			ConnecterAssetKind.Image => 2,
			ConnecterAssetKind.Audio => 3,
			ConnecterAssetKind.Unsupported => 8,
			ConnecterAssetKind.Unknown => 9,
			_ => 7
		};
	}

	private static bool ShouldSkipPath( string path )
	{
		var name = Path.GetFileName( path );
		if ( string.IsNullOrWhiteSpace( name ) )
			return true;

		return name.StartsWith( "." ) || name.Equals( "Thumbs.db", StringComparison.OrdinalIgnoreCase );
	}

	private static IEnumerable<string> SafeEnumerateDirectories( string folderPath, bool recursive )
	{
		var options = new EnumerationOptions
		{
			IgnoreInaccessible = true,
			RecurseSubdirectories = recursive,
			AttributesToSkip = FileAttributes.Hidden | FileAttributes.System
		};

		try
		{
			return Directory.EnumerateDirectories( folderPath, "*", options );
		}
		catch
		{
			return [];
		}
	}

	private static IEnumerable<string> SafeEnumerateFiles( string folderPath, bool recursive )
	{
		var options = new EnumerationOptions
		{
			IgnoreInaccessible = true,
			RecurseSubdirectories = recursive,
			AttributesToSkip = FileAttributes.Hidden | FileAttributes.System
		};

		try
		{
			return Directory.EnumerateFiles( folderPath, "*", options );
		}
		catch
		{
			return [];
		}
	}
}
