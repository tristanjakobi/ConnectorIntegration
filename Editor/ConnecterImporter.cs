using Sandbox;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace Editor;

public sealed record ConnecterEditorImportResult( ConnecterImportResult Result, Asset PrimaryAsset );

public static class ConnecterImporter
{
	private static readonly string[] TextureSuffixes =
	[
		"_basecolor", "_base_color", "_albedo", "_color", "_diffuse", "_diff",
		"_normal", "_normaldx", "_normalgl", "_nrm",
		"_roughness", "_rough", "_metallic", "_metal",
		"_ambientocclusion", "_ao", "_height", "_displacement",
		"_opacity", "_alpha", "_emissive", "_selfillum", "_mask", "_trans"
	];

	public static ConnecterEditorImportResult Import( ConnecterAssetRecord record, ConnecterImportOptions options )
	{
		if ( record is null )
			throw new ArgumentNullException( nameof( record ) );

		if ( options is null )
			throw new ArgumentNullException( nameof( options ) );

		if ( !record.CanImport )
			return new ConnecterEditorImportResult( new ConnecterImportResult( record.FullPath, null, null, false, record.Warning ), null );

		Directory.CreateDirectory( options.ProjectAssetsPath );

		if ( record.IsDirectory )
			return ImportDirectory( record, options );

		return ImportFile( record, options );
	}

	public static (int Count, long Bytes) GetDirectoryStats( string directory )
	{
		var count = 0;
		var bytes = 0L;

		foreach ( var file in SafeEnumerateFiles( directory, true ) )
		{
			try
			{
				var info = new FileInfo( file );
				count++;
				bytes += info.Length;
			}
			catch
			{
			}
		}

		return (count, bytes);
	}

	private static ConnecterEditorImportResult ImportDirectory( ConnecterAssetRecord record, ConnecterImportOptions options )
	{
		var parentDestination = ConnecterPathUtility.GetImportDirectory( record, options );
		var destinationDirectory = Path.Combine( parentDestination, ConnecterPathUtility.SanitizePathSegment( record.Name ) );

		EnsureDestinationInsideProject( options.ProjectAssetsPath, destinationDirectory );
		CopyDirectory( record.FullPath, destinationDirectory, options.OverwriteChangedFiles );

		Asset firstAsset = null;
		foreach ( var file in SafeEnumerateFiles( destinationDirectory, true ) )
		{
			var kind = ConnecterAssetClassifier.Classify( file );
			var primaryPath = file;

			if ( kind == ConnecterAssetKind.ModelSource )
			{
				primaryPath = CreateModelDoc( options.ProjectAssetsPath, file );
				AssetSystem.RegisterFile( file );
			}

			var asset = AssetSystem.RegisterFile( primaryPath );
			_ = asset?.CompileIfNeededAsync();
			firstAsset ??= asset;
		}

		return new ConnecterEditorImportResult(
			new ConnecterImportResult( record.FullPath, destinationDirectory, firstAsset?.AbsolutePath, true, $"Imported folder to {destinationDirectory}" ),
			firstAsset );
	}

	private static ConnecterEditorImportResult ImportFile( ConnecterAssetRecord record, ConnecterImportOptions options )
	{
		var destinationDirectory = ConnecterPathUtility.GetImportDirectory( record, options );
		EnsureDestinationInsideProject( options.ProjectAssetsPath, destinationDirectory );
		Directory.CreateDirectory( destinationDirectory );

		var copiedFiles = new List<string>();
		foreach ( var source in GetFileImportSet( record, options.CopyFolderDependencies ) )
		{
			var destination = Path.Combine( destinationDirectory, Path.GetFileName( source ) );
			CopyFileIfNeeded( source, destination, options.OverwriteChangedFiles );
			copiedFiles.Add( destination );
		}

		var importedSource = copiedFiles.FirstOrDefault( x => string.Equals( Path.GetFileName( x ), record.Name, StringComparison.OrdinalIgnoreCase ) )
			?? copiedFiles.FirstOrDefault();

		if ( importedSource is null )
		{
			return new ConnecterEditorImportResult(
				new ConnecterImportResult( record.FullPath, null, null, false, "No files were imported." ),
				null );
		}

		var primaryPath = importedSource;
		if ( record.Kind == ConnecterAssetKind.ModelSource )
		{
			primaryPath = CreateModelDoc( options.ProjectAssetsPath, importedSource );
		}

		var primaryAsset = AssetSystem.RegisterFile( primaryPath );
		if ( !string.Equals( primaryPath, importedSource, StringComparison.OrdinalIgnoreCase ) )
		{
			AssetSystem.RegisterFile( importedSource );
		}

		_ = primaryAsset?.CompileIfNeededAsync();

		return new ConnecterEditorImportResult(
			new ConnecterImportResult( record.FullPath, importedSource, primaryPath, true, $"Imported {record.Name}" ),
			primaryAsset );
	}

	private static string CreateModelDoc( string projectAssetsPath, string importedSourcePath )
	{
		var relativeSource = ConnecterPathUtility.GetAssetRelativePath( projectAssetsPath, importedSourcePath );
		var modelPath = Path.Combine( Path.GetDirectoryName( importedSourcePath )!, $"{Path.GetFileNameWithoutExtension( importedSourcePath )}.vmdl" );
		var modelDoc = ConnecterModelDocBuilder.CreateModelDoc( relativeSource, Path.GetFileNameWithoutExtension( importedSourcePath ) );

		File.WriteAllText( modelPath, modelDoc );
		return modelPath;
	}

	private static IReadOnlyList<string> GetFileImportSet( ConnecterAssetRecord record, bool includeDependencies )
	{
		if ( !includeDependencies )
			return [record.FullPath];

		var result = new HashSet<string>( StringComparer.OrdinalIgnoreCase )
		{
			record.FullPath
		};

		var folder = Path.GetDirectoryName( record.FullPath );
		if ( string.IsNullOrWhiteSpace( folder ) || !Directory.Exists( folder ) )
			return result.ToList();

		var extension = Path.GetExtension( record.FullPath );
		var baseName = Path.GetFileNameWithoutExtension( record.FullPath );

		foreach ( var sibling in SafeEnumerateFiles( folder, false ) )
		{
			var siblingExtension = Path.GetExtension( sibling );
			var siblingBase = Path.GetFileNameWithoutExtension( sibling );

			if ( siblingExtension.Equals( ".mtl", StringComparison.OrdinalIgnoreCase )
				&& (siblingBase.Equals( baseName, StringComparison.OrdinalIgnoreCase ) || extension.Equals( ".obj", StringComparison.OrdinalIgnoreCase )) )
			{
				result.Add( sibling );
			}

			if ( ConnecterAssetClassifier.IsTextureDependency( sibling ) )
			{
				if ( GetTextureFamilyName( siblingBase ).Equals( GetTextureFamilyName( baseName ), StringComparison.OrdinalIgnoreCase )
					|| siblingBase.Contains( baseName, StringComparison.OrdinalIgnoreCase )
					|| baseName.Contains( siblingBase, StringComparison.OrdinalIgnoreCase ) )
				{
					result.Add( sibling );
				}
			}
		}

		foreach ( var mtl in result.Where( x => Path.GetExtension( x ).Equals( ".mtl", StringComparison.OrdinalIgnoreCase ) ).ToArray() )
		{
			foreach ( var texture in ReadMaterialTextureReferences( folder, mtl ) )
			{
				result.Add( texture );
			}
		}

		return result.ToList();
	}

	private static string GetTextureFamilyName( string name )
	{
		var lower = name.ToLowerInvariant();
		lower = Regex.Replace( lower, @"_\d+$", "" );

		foreach ( var suffix in TextureSuffixes.OrderByDescending( x => x.Length ) )
		{
			if ( lower.EndsWith( suffix ) )
				return lower[..^suffix.Length];
		}

		return lower;
	}

	private static IEnumerable<string> ReadMaterialTextureReferences( string sourceFolder, string materialPath )
	{
		foreach ( var line in File.ReadLines( materialPath ) )
		{
			var trimmed = line.Trim();
			if ( trimmed.Length == 0 || trimmed.StartsWith( "#" ) )
				continue;

			if ( !trimmed.StartsWith( "map_", StringComparison.OrdinalIgnoreCase ) && !trimmed.StartsWith( "bump", StringComparison.OrdinalIgnoreCase ) )
				continue;

			var parts = trimmed.Split( ' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries );
			var pathPart = parts.LastOrDefault();
			if ( string.IsNullOrWhiteSpace( pathPart ) )
				continue;

			var candidate = Path.GetFullPath( Path.Combine( sourceFolder, pathPart.Trim( '"' ) ) );
			if ( File.Exists( candidate ) )
				yield return candidate;
		}
	}

	private static void CopyDirectory( string sourceDirectory, string destinationDirectory, bool overwriteChangedFiles )
	{
		foreach ( var directory in SafeEnumerateDirectories( sourceDirectory, true ) )
		{
			var relative = Path.GetRelativePath( sourceDirectory, directory );
			Directory.CreateDirectory( Path.Combine( destinationDirectory, relative ) );
		}

		foreach ( var file in SafeEnumerateFiles( sourceDirectory, true ) )
		{
			var relative = Path.GetRelativePath( sourceDirectory, file );
			CopyFileIfNeeded( file, Path.Combine( destinationDirectory, relative ), overwriteChangedFiles );
		}
	}

	private static void CopyFileIfNeeded( string sourceFile, string destinationFile, bool overwriteChangedFiles )
	{
		Directory.CreateDirectory( Path.GetDirectoryName( destinationFile )! );

		if ( File.Exists( destinationFile ) )
		{
			var sourceInfo = new FileInfo( sourceFile );
			var destinationInfo = new FileInfo( destinationFile );

			if ( !overwriteChangedFiles || (sourceInfo.Length == destinationInfo.Length && sourceInfo.LastWriteTimeUtc <= destinationInfo.LastWriteTimeUtc) )
				return;
		}

		File.Copy( sourceFile, destinationFile, true );
		File.SetLastWriteTimeUtc( destinationFile, File.GetLastWriteTimeUtc( sourceFile ) );
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

	private static void EnsureDestinationInsideProject( string projectAssetsPath, string destination )
	{
		if ( !ConnecterPathUtility.IsPathInside( projectAssetsPath, destination ) )
			throw new InvalidOperationException( $"Refusing to import outside project assets: {destination}" );
	}
}
