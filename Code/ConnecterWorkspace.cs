using System;
using System.Collections.Generic;

namespace Sandbox;

public sealed record ConnecterWorkspace( string WorkspacePath, IReadOnlyList<ConnecterRepository> Repositories )
{
	public bool HasRepositories => Repositories is { Count: > 0 };
}

public sealed record ConnecterRepository( string Name, string FullPath )
{
	public static ConnecterRepository FromPath( string path )
	{
		var normalized = ConnecterPathUtility.NormalizeDirectoryPath( path );
		var name = ConnecterPathUtility.GetFileName( normalized );

		if ( string.IsNullOrWhiteSpace( name ) )
			name = normalized;

		return new ConnecterRepository( name, normalized );
	}
}

public sealed record ConnecterAssetRecord(
	string FullPath,
	string RepositoryRoot,
	string RepositoryName,
	string RelativePath,
	bool IsDirectory,
	ConnecterAssetKind Kind,
	string Warning = null )
{
	public string Name => ConnecterPathUtility.GetFileName( FullPath );
	public bool CanImport => ConnecterAssetClassifier.IsSupportedImport( Kind );

	public static ConnecterAssetRecord FromPath( ConnecterRepository repository, string path, bool isDirectory )
	{
		var fullPath = ConnecterPathUtility.NormalizeDirectoryPath( path );
		var relativePath = ConnecterPathUtility.GetRelativePath( repository.FullPath, fullPath );
		var kind = ConnecterAssetClassifier.Classify( fullPath, isDirectory );
		var warning = ConnecterAssetClassifier.GetWarning( fullPath, kind );

		return new ConnecterAssetRecord( fullPath, repository.FullPath, repository.Name, relativePath, isDirectory, kind, warning );
	}
}

public sealed record ConnecterImportOptions(
	string ProjectAssetsPath,
	string ImportRootName = "ConnecterImports",
	bool CopyFolderDependencies = true,
	bool OverwriteChangedFiles = true );

public sealed record ConnecterImportResult(
	string SourcePath,
	string ImportedSourcePath,
	string PrimaryAssetPath,
	bool Imported,
	string Message );
