using System;
using System.Collections.Generic;
using System.Linq;

namespace Sandbox;

public enum ConnecterAssetKind
{
	Unknown,
	Folder,
	ModelSource,
	SboxModel,
	Image,
	Audio,
	Material,
	Unsupported
}

public static class ConnecterAssetClassifier
{
	private static readonly HashSet<string> ModelSourceExtensions = new( StringComparer.OrdinalIgnoreCase )
	{
		".fbx",
		".obj",
		".vox"
	};

	private static readonly HashSet<string> UnsupportedModelExtensions = new( StringComparer.OrdinalIgnoreCase )
	{
		".blend",
		".glb",
		".gltf",
		".dae",
		".usd",
		".usdz",
		".3ds",
		".max",
		".mb",
		".ma"
	};

	private static readonly HashSet<string> ImageExtensions = new( StringComparer.OrdinalIgnoreCase )
	{
		".png",
		".jpg",
		".jpeg",
		".tga",
		".webp",
		".bmp",
		".tif",
		".tiff",
		".exr",
		".hdr"
	};

	private static readonly HashSet<string> AudioExtensions = new( StringComparer.OrdinalIgnoreCase )
	{
		".wav",
		".mp3",
		".ogg",
		".flac"
	};

	public static bool IsTextureDependency( string path )
	{
		return ImageExtensions.Contains( GetExtension( path ) );
	}

	public static bool IsSupportedImport( ConnecterAssetKind kind )
	{
		return kind is ConnecterAssetKind.Folder
			or ConnecterAssetKind.ModelSource
			or ConnecterAssetKind.SboxModel
			or ConnecterAssetKind.Image
			or ConnecterAssetKind.Audio
			or ConnecterAssetKind.Material;
	}

	public static ConnecterAssetKind Classify( string path, bool isDirectory = false )
	{
		if ( isDirectory )
			return ConnecterAssetKind.Folder;

		var extension = GetExtension( path );

		if ( ModelSourceExtensions.Contains( extension ) )
			return ConnecterAssetKind.ModelSource;

		if ( UnsupportedModelExtensions.Contains( extension ) )
			return ConnecterAssetKind.Unsupported;

		if ( extension.Equals( ".vmdl", StringComparison.OrdinalIgnoreCase ) || extension.Equals( ".vmdl_c", StringComparison.OrdinalIgnoreCase ) )
			return ConnecterAssetKind.SboxModel;

		if ( extension.Equals( ".vmat", StringComparison.OrdinalIgnoreCase ) || extension.Equals( ".vmat_c", StringComparison.OrdinalIgnoreCase ) )
			return ConnecterAssetKind.Material;

		if ( ImageExtensions.Contains( extension ) )
			return ConnecterAssetKind.Image;

		if ( AudioExtensions.Contains( extension ) || extension.Equals( ".vsnd", StringComparison.OrdinalIgnoreCase ) || extension.Equals( ".sound", StringComparison.OrdinalIgnoreCase ) )
			return ConnecterAssetKind.Audio;

		return ConnecterAssetKind.Unknown;
	}

	public static string GetWarning( string path, ConnecterAssetKind kind )
	{
		if ( kind == ConnecterAssetKind.Unsupported )
			return $"{GetExtension( path )} is visible but v1 does not convert it. Export it to FBX/OBJ/VOX first.";

		if ( kind == ConnecterAssetKind.Unknown )
			return "This file type is visible for browsing, but v1 does not know how to import it as a s&box asset.";

		return null;
	}

	public static IEnumerable<string> GetKnownExtensions()
	{
		return ModelSourceExtensions
			.Concat( UnsupportedModelExtensions )
			.Concat( ImageExtensions )
			.Concat( AudioExtensions )
			.Concat( [".vmdl", ".vmdl_c", ".vmat", ".vmat_c", ".vsnd", ".sound"] )
			.Distinct( StringComparer.OrdinalIgnoreCase )
			.OrderBy( x => x );
	}

	private static string GetExtension( string path )
	{
		var name = ConnecterPathUtility.GetFileName( path );
		var index = name.LastIndexOf( '.' );

		return index >= 0 ? name[index..] : string.Empty;
	}
}
