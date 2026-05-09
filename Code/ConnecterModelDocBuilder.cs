namespace Sandbox;

public static class ConnecterModelDocBuilder
{
	public static string CreateModelDoc( string sourceAssetRelativePath, string meshName )
	{
		sourceAssetRelativePath = ConnecterPathUtility.NormalizeAssetPath( sourceAssetRelativePath );
		meshName = ConnecterPathUtility.GetFileNameWithoutExtension( meshName ?? "model" );

		return $@"<!-- kv3 encoding:text:version{{e21c7f3c-8a33-41c5-9977-a76d3a32aa0d}} format:modeldoc29:version{{3cec427c-1b0e-4d48-a90a-0436f33a6041}} -->
{{
	rootNode =
	{{
		_class = ""RootNode""
		children =
		[
			{{
				_class = ""RenderMeshList""
				children =
				[
					{{
						_class = ""RenderMeshFile""
						name = ""{Escape( meshName )}""
						filename = ""{Escape( sourceAssetRelativePath )}""
						import_translation = [ 0.0, 0.0, 0.0 ]
						import_rotation = [ 0.0, 0.0, 0.0 ]
						import_scale = 1.0
						align_origin_x_type = ""None""
						align_origin_y_type = ""None""
						align_origin_z_type = ""None""
						parent_bone = """"
						import_filter =
						{{
							exclude_by_default = false
							exception_list = [  ]
						}}
					}},
				]
			}},
			{{
				_class = ""AnimationList""
				children =
				[
					{{
						_class = ""AnimBindPose""
						name = ""bindPose""
						activity_name = """"
						activity_weight = 1
						weight_list_name = """"
						fade_in_time = 0.2
						fade_out_time = 0.2
						looping = false
						delta = false
						worldSpace = false
						hidden = false
					}},
				]
			}},
		]
		model_archetype = ""static_prop_model""
		primary_associated_entity = """"
		anim_graph_name = """"
	}}
}}
";
	}

	private static string Escape( string value )
	{
		return (value ?? string.Empty).Replace( "\\", "\\\\" ).Replace( "\"", "\\\"" );
	}
}
