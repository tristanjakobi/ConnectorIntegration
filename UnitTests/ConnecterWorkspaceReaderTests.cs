using System;
using System.IO;
using Editor;
using Sandbox;

[TestClass]
public sealed class ConnecterWorkspaceReaderTests
{
	private string TempRoot;

	[TestInitialize]
	public void SetUp()
	{
		TempRoot = Path.Combine( Path.GetTempPath(), "ConnectorIntegrationTests", Guid.NewGuid().ToString( "N" ) );
		Directory.CreateDirectory( TempRoot );
	}

	[TestCleanup]
	public void TearDown()
	{
		if ( Directory.Exists( TempRoot ) )
		{
			Directory.Delete( TempRoot, true );
		}
	}

	[TestMethod]
	public void ReadsRepositoryRootsFromSettingsXml()
	{
		var firstRoot = Path.Combine( TempRoot, "Basic Assets" );
		var secondRoot = Path.Combine( TempRoot, "Game Assets" );
		Directory.CreateDirectory( firstRoot );
		Directory.CreateDirectory( secondRoot );

		var settingsPath = Path.Combine( TempRoot, "settings.xml" );
		File.WriteAllText( settingsPath, $"""
<?xml version="1.0" encoding="utf-8"?>
<settings>
	<setting key="2007">["{EscapeJsonPath( firstRoot )}","{EscapeJsonPath( secondRoot )}","{EscapeJsonPath( Path.Combine( TempRoot, "Missing" ) )}"]</setting>
</settings>
""" );

		var repositories = ConnecterWorkspaceReader.ReadRepositoriesFromSettingsXml( settingsPath );

		Assert.AreEqual( 2, repositories.Count );
		Assert.AreEqual( "Basic Assets", repositories[0].Name );
		Assert.AreEqual( "Game Assets", repositories[1].Name );
	}

	[TestMethod]
	public void ReadsRepositoryRootsFromReadOnlyDatabaseBytes()
	{
		var repositoryRoot = Path.Combine( TempRoot, "Assetpack Assets" );
		Directory.CreateDirectory( repositoryRoot );

		var databasePath = Path.Combine( TempRoot, "default.dcdb" );
		File.WriteAllText( databasePath, $"sqlite header\0Repositories\0{repositoryRoot}\0other data" );

		var repositories = ConnecterWorkspaceReader.ReadRepositoriesFromDatabase( databasePath );

		Assert.AreEqual( 1, repositories.Count );
		Assert.AreEqual( "Assetpack Assets", repositories[0].Name );
		Assert.AreEqual( ConnecterPathUtility.NormalizeDirectoryPath( repositoryRoot ), repositories[0].FullPath );
	}

	private static string EscapeJsonPath( string path )
	{
		return path.Replace( "\\", "\\\\" );
	}
}
