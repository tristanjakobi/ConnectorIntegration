using System.IO;
using Sandbox;

[TestClass]
public sealed class ConnecterImportUtilityTests
{
	[TestMethod]
	public void MapsImportDestinationInsideProjectAssets()
	{
		var repository = ConnecterRepository.FromPath( @"E:\Game Assets\Basic Assets" );
		var record = new ConnecterAssetRecord(
			@"E:\Game Assets\Basic Assets\Props\Crate\crate.fbx",
			repository.FullPath,
			repository.Name,
			"Props/Crate/crate.fbx",
			false,
			ConnecterAssetKind.ModelSource,
			null );

		var options = new ConnecterImportOptions( @"C:\Project\Assets" );

		var destination = ConnecterPathUtility.GetImportDirectory( record, options );

		Assert.AreEqual(
			Path.Combine( @"C:\Project\Assets", "ConnecterImports", "Basic Assets", "Props", "Crate" ),
			destination );
		Assert.IsTrue( ConnecterPathUtility.IsPathInside( options.ProjectAssetsPath, destination ) );
	}

	[TestMethod]
	public void ClassifiesSupportedAndUnsupportedAssetTypes()
	{
		Assert.AreEqual( ConnecterAssetKind.ModelSource, ConnecterAssetClassifier.Classify( "chair.fbx" ) );
		Assert.AreEqual( ConnecterAssetKind.ModelSource, ConnecterAssetClassifier.Classify( "shape.obj" ) );
		Assert.AreEqual( ConnecterAssetKind.Image, ConnecterAssetClassifier.Classify( "albedo.png" ) );
		Assert.AreEqual( ConnecterAssetKind.Audio, ConnecterAssetClassifier.Classify( "hit.wav" ) );
		Assert.AreEqual( ConnecterAssetKind.SboxModel, ConnecterAssetClassifier.Classify( "chair.vmdl" ) );
		Assert.AreEqual( ConnecterAssetKind.Unsupported, ConnecterAssetClassifier.Classify( "chair.glb" ) );
		Assert.AreEqual( ConnecterAssetKind.Unsupported, ConnecterAssetClassifier.Classify( "chair.blend" ) );
		Assert.AreEqual( ConnecterAssetKind.Unknown, ConnecterAssetClassifier.Classify( "notes.txt" ) );
	}

	[TestMethod]
	public void GeneratesVmdlWithRenderMeshAndBindPose()
	{
		var vmdl = ConnecterModelDocBuilder.CreateModelDoc( "connecterimports/basic assets/props/chair.fbx", "chair.fbx" );

		StringAssert.Contains( vmdl, "_class = \"RenderMeshFile\"" );
		StringAssert.Contains( vmdl, "filename = \"connecterimports/basic assets/props/chair.fbx\"" );
		StringAssert.Contains( vmdl, "_class = \"AnimBindPose\"" );
		StringAssert.Contains( vmdl, "model_archetype = \"static_prop_model\"" );
	}
}
