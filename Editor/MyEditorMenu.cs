using Editor;
using static Sandbox.Internal.GlobalToolsNamespace;

public static class MyEditorMenu
{
	[Menu( "Editor", "View/Connecter Browser", "perm_media" )]
	public static void OpenConnecterBrowser()
	{
		var browser = EditorWindow.DockManager.Create<ConnecterBrowserDock>();
		EditorWindow.DockManager.RaiseDock( browser );
	}
}
