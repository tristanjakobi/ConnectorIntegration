using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Editor;
using Sandbox;

[TestClass]
public sealed class ConnecterAssetScannerTests
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
	public async Task AssetModeScansFilesRecursivelyWithoutFolders()
	{
		var props = Path.Combine( TempRoot, "Props" );
		Directory.CreateDirectory( props );
		File.WriteAllText( Path.Combine( props, "crate.fbx" ), "" );
		File.WriteAllText( Path.Combine( props, "crate.vmat" ), "" );
		File.WriteAllText( Path.Combine( props, "crate.png" ), "" );
		File.WriteAllText( Path.Combine( props, "notes.txt" ), "" );

		var repository = ConnecterRepository.FromPath( TempRoot );
		var result = await ConnecterAssetScanner.ScanAssetsAsync( [repository], "", ConnecterBrowserFilter.All, CancellationToken.None );

		Assert.AreEqual( 3, result.Items.Count );
		Assert.IsFalse( result.Items.Any( x => x.IsDirectory ) );
		Assert.IsTrue( result.Items.Any( x => x.Name == "crate.fbx" ) );
		Assert.IsTrue( result.Items.Any( x => x.Name == "crate.vmat" ) );
		Assert.IsTrue( result.Items.Any( x => x.Name == "crate.png" ) );
	}

	[TestMethod]
	public async Task AssetModeFiltersMaterials()
	{
		File.WriteAllText( Path.Combine( TempRoot, "crate.fbx" ), "" );
		File.WriteAllText( Path.Combine( TempRoot, "crate.vmat" ), "" );

		var repository = ConnecterRepository.FromPath( TempRoot );
		var result = await ConnecterAssetScanner.ScanAssetsAsync( [repository], "", ConnecterBrowserFilter.Materials, CancellationToken.None );

		Assert.AreEqual( 1, result.Items.Count );
		Assert.AreEqual( "crate.vmat", result.Items[0].Name );
		Assert.AreEqual( ConnecterAssetKind.Material, result.Items[0].Kind );
	}
}
