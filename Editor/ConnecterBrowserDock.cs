using Sandbox;
using Sandbox.UI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

namespace Editor;

[Dock( "Editor", "Connecter Browser", "perm_media" )]
public sealed class ConnecterBrowserDock : Widget
{
	private ConnecterWorkspace Workspace;
	private ConnecterRepository CurrentRepository;
	private string CurrentFolder;
	private ConnecterBrowserFilter CurrentFilter = ConnecterBrowserFilter.All;
	private AssetListViewMode CurrentViewMode = AssetListViewMode.List;

	private TreeView LocationTree;
	private ListView AssetList;
	private LineEdit SearchText;
	private Label StatusLabel;
	private Button ImportButton;
	private ToolButton ViewModeButton;
	private ToolButton FilterButton;

	private CancellationTokenSource ScanCancellation;
	private IReadOnlyList<ConnecterAssetRecord> CurrentItems = [];

	public ConnecterBrowserDock( Widget parent ) : base( parent )
	{
		WindowTitle = "Connecter Browser";
		SetWindowIcon( "perm_media" );
		MinimumSize = new Vector2( 300, 180 );

		Layout = Layout.Column();
		Layout.Spacing = 4;

		BuildToolbar();
		BuildBody();

		StatusLabel = Layout.Add( new Label( this ) { Text = "Loading Connecter workspace..." } );
		StatusLabel.MinimumHeight = Theme.RowHeight;

		LoadWorkspace();
	}

	private void BuildToolbar()
	{
		var toolbar = Layout.AddRow();
		toolbar.Spacing = 4;

		SearchText = toolbar.Add( new LineEdit(), 1 );
		SearchText.PlaceholderText = "Search Connecter assets";
		SearchText.TextChanged += _ => RefreshAssetList();

		toolbar.Add( new ToolButton( "Clear Search", "clear", this )
		{
			MouseLeftPress = () =>
			{
				SearchText.Text = string.Empty;
				RefreshAssetList();
			}
		} );

		toolbar.Add( new ToolButton( "Refresh", "refresh", this )
		{
			MouseLeftPress = LoadWorkspace
		} );

		FilterButton = toolbar.Add( new ToolButton( "Filter", "filter_list", this ) );
		FilterButton.MouseLeftPress = OpenFilterMenu;

		ViewModeButton = toolbar.Add( new ToolButton( "View Mode", "view_headline", this ) );
		ViewModeButton.MouseLeftPress = OpenViewModeMenu;

		ImportButton = toolbar.Add( new Button.Primary( "Import Selected", "download", this ) );
		ImportButton.Clicked = () => ImportSelected();
	}

	private void BuildBody()
	{
		var splitter = new Splitter( this )
		{
			IsHorizontal = true
		};

		LocationTree = new TreeView( splitter );
		LocationTree.ItemSelected += OnLocationSelected;

		AssetList = new ListView( splitter );
		AssetList.MultiSelect = true;
		AssetList.ItemSelected += _ => UpdateImportButton();
		AssetList.OnSelectionChanged += _ => UpdateImportButton();
		AssetList.ItemActivated += OnAssetActivated;
		AssetList.ItemContextMenu = OpenAssetContextMenu;
		AssetList.ItemDrag = StartAssetDrag;
		AssetList.Margin = new Margin( 4 );

		splitter.AddWidget( LocationTree );
		splitter.SetStretch( 0, 1 );
		splitter.AddWidget( AssetList );
		splitter.SetStretch( 1, 4 );

		Layout.Add( splitter, 1 );

		ApplyViewMode( AssetListViewMode.List );
	}

	private void LoadWorkspace()
	{
		ScanCancellation?.Cancel();

		Workspace = ConnecterWorkspaceReader.Read();
		LocationTree.Clear();

		if ( !Workspace.HasRepositories )
		{
			StatusLabel.Text = $"No Connecter roots found at {ConnecterWorkspaceReader.DefaultWorkspacePath}";
			CurrentRepository = null;
			CurrentFolder = null;
			AssetList.SetItems( [] );
			UpdateImportButton();
			return;
		}

		foreach ( var repository in Workspace.Repositories )
		{
			LocationTree.AddItem( new ConnecterLocationNode( repository, repository.FullPath, NavigateTo ) );
		}

		var first = Workspace.Repositories.First();
		CurrentRepository = first;
		CurrentFolder = first.FullPath;
		LocationTree.Open( first );
		LocationTree.SelectItem( first, skipEvents: true );
		RefreshAssetList();
	}

	private void OnLocationSelected( object item )
	{
		switch ( item )
		{
			case ConnecterRepository repository:
				NavigateTo( repository, repository.FullPath );
				break;
			case ConnecterLocation location:
				NavigateTo( location.Repository, location.FullPath );
				break;
		}
	}

	private void NavigateTo( ConnecterRepository repository, string folderPath )
	{
		if ( repository is null || string.IsNullOrWhiteSpace( folderPath ) || !Directory.Exists( folderPath ) )
			return;

		CurrentRepository = repository;
		CurrentFolder = folderPath;
		RefreshAssetList();
	}

	private async void RefreshAssetList()
	{
		if ( CurrentRepository is null || string.IsNullOrWhiteSpace( CurrentFolder ) )
			return;

		ScanCancellation?.Cancel();
		ScanCancellation = new CancellationTokenSource();
		var token = ScanCancellation.Token;

		StatusLabel.Text = $"Scanning {CurrentRepository.Name}...";
		AssetList.SetItems( [] );

		try
		{
			var result = await ConnecterAssetScanner.ScanAsync( CurrentRepository, CurrentFolder, SearchText.Text, CurrentFilter, token );

			MainThread.Queue( () =>
			{
				if ( token.IsCancellationRequested || !IsValid )
					return;

				CurrentItems = result.Items;
				AssetList.SetItems( CurrentItems.Cast<object>() );
				StatusLabel.Text = BuildStatusText( result );
				UpdateImportButton();
			} );
		}
		catch ( OperationCanceledException )
		{
		}
		catch ( Exception exception )
		{
			StatusLabel.Text = $"Connecter scan failed: {exception.Message}";
		}
	}

	private string BuildStatusText( ConnecterScanResult result )
	{
		var location = CurrentFolder is null ? "" : ConnecterPathUtility.GetRelativePath( CurrentRepository.FullPath, CurrentFolder );
		if ( string.IsNullOrWhiteSpace( location ) || location == "." )
			location = CurrentRepository.Name;

		var suffix = result.Truncated ? " (showing first 1,000 results)" : "";
		return $"{location}: {result.Items.Count:n0} item{(result.Items.Count == 1 ? "" : "s")}{suffix}";
	}

	private void UpdateImportButton()
	{
		ImportButton.Enabled = GetSelectedRecords().Any( x => x.CanImport );
	}

	private void OpenFilterMenu()
	{
		var menu = new ContextMenu( this );

		foreach ( var filter in Enum.GetValues<ConnecterBrowserFilter>() )
		{
			var option = menu.AddOption( filter.ToString(), GetFilterIcon( filter ), () =>
			{
				CurrentFilter = filter;
				FilterButton.Icon = GetFilterIcon( filter );
				RefreshAssetList();
			} );

			option.Checkable = true;
			option.Checked = filter == CurrentFilter;
		}

		menu.OpenAt( FilterButton.ScreenRect.BottomLeft, false );
	}

	private void OpenViewModeMenu()
	{
		var menu = new ContextMenu( this );

		AddViewOption( menu, "List View", "view_headline", AssetListViewMode.List );
		AddViewOption( menu, "Small Icons", "apps", AssetListViewMode.SmallIcons );
		AddViewOption( menu, "Medium Icons", "grid_on", AssetListViewMode.MediumIcons );
		AddViewOption( menu, "Large Icons", "grid_view", AssetListViewMode.LargeIcons );

		menu.OpenAt( ViewModeButton.ScreenRect.BottomLeft, false );
	}

	private void AddViewOption( ContextMenu menu, string title, string icon, AssetListViewMode viewMode )
	{
		var option = menu.AddOption( title, icon, () => ApplyViewMode( viewMode ) );
		option.Checkable = true;
		option.Checked = viewMode == CurrentViewMode;
	}

	private void ApplyViewMode( AssetListViewMode viewMode )
	{
		CurrentViewMode = viewMode;
		ViewModeButton.Icon = viewMode switch
		{
			AssetListViewMode.SmallIcons => "apps",
			AssetListViewMode.MediumIcons => "grid_on",
			AssetListViewMode.LargeIcons => "grid_view",
			_ => "view_headline"
		};

		switch ( viewMode )
		{
			case AssetListViewMode.SmallIcons:
				AssetList.ItemSize = new Vector2( 72, 104 );
				AssetList.ItemSpacing = 4;
				AssetList.ItemPaint = PaintIconItem;
				break;
			case AssetListViewMode.MediumIcons:
				AssetList.ItemSize = new Vector2( 104, 144 );
				AssetList.ItemSpacing = 4;
				AssetList.ItemPaint = PaintIconItem;
				break;
			case AssetListViewMode.LargeIcons:
				AssetList.ItemSize = new Vector2( 136, 184 );
				AssetList.ItemSpacing = 6;
				AssetList.ItemPaint = PaintIconItem;
				break;
			default:
				AssetList.ItemSize = new Vector2( 0, Theme.RowHeight );
				AssetList.ItemSpacing = 0;
				AssetList.ItemPaint = PaintListItem;
				break;
		}

		AssetList.Update();
	}

	private void PaintListItem( VirtualWidget item )
	{
		if ( item.Object is not ConnecterAssetRecord record )
			return;

		DrawItemBackground( item );

		var rect = item.Rect;
		var iconRect = rect.Shrink( 6, 4 );
		iconRect.Width = iconRect.Height = 18;

		Paint.SetPen( GetKindColor( record.Kind ) );
		Paint.DrawIcon( iconRect, GetKindIcon( record ), 18, TextFlag.Center );

		var nameRect = rect.Shrink( 32, 0, 260, 0 );
		Paint.SetPen( item.Selected ? Color.White : Theme.Text );
		Paint.DrawText( nameRect, record.Name, TextFlag.LeftCenter | TextFlag.SingleLine );

		var typeRect = rect;
		typeRect.Left = rect.Right - 250;
		typeRect.Width = 90;
		Paint.SetPen( Theme.TextLight );
		Paint.DrawText( typeRect, record.Kind.ToString(), TextFlag.LeftCenter | TextFlag.SingleLine );

		var pathRect = rect;
		pathRect.Left = rect.Right - 155;
		pathRect.Right -= 8;
		Paint.SetPen( record.Warning is null ? Theme.TextLight : Theme.Yellow );
		Paint.DrawText( pathRect, record.Warning ?? record.RelativePath, TextFlag.LeftCenter | TextFlag.SingleLine );
	}

	private void PaintIconItem( VirtualWidget item )
	{
		if ( item.Object is not ConnecterAssetRecord record )
			return;

		DrawItemBackground( item );

		var rect = item.Rect.Shrink( 4 );
		var iconRect = rect;
		iconRect.Height = iconRect.Width;

		Paint.SetBrush( Theme.ControlBackground );
		Paint.ClearPen();
		Paint.DrawRect( iconRect, Theme.ControlRadius );

		Paint.SetPen( GetKindColor( record.Kind ) );
		Paint.DrawIcon( iconRect.Shrink( 16 ), GetKindIcon( record ), Math.Min( 48, iconRect.Width - 20 ), TextFlag.Center );

		var textRect = rect;
		textRect.Top = iconRect.Bottom + 4;
		textRect.Height = 36;
		Paint.SetDefaultFont( 7 );
		Paint.SetPen( item.Selected ? Color.White : Theme.Text );
		Paint.DrawText( textRect, Paint.GetElidedText( record.Name, textRect.Width, ElideMode.Middle ), TextFlag.LeftTop );

		if ( record.Warning is not null )
		{
			Paint.SetPen( Theme.Yellow );
			Paint.DrawIcon( rect.Shrink( 4 ), "warning", 16, TextFlag.RightTop );
		}
	}

	private void DrawItemBackground( VirtualWidget item )
	{
		if ( item.Selected || item.Pressed )
		{
			Paint.SetBrush( Theme.Primary.WithAlpha( 0.38f ) );
			Paint.ClearPen();
			Paint.DrawRect( item.Rect.Shrink( 1 ), Theme.ControlRadius );
		}
		else if ( item.Hovered )
		{
			Paint.SetBrush( Theme.SurfaceLightBackground.WithAlpha( 0.35f ) );
			Paint.ClearPen();
			Paint.DrawRect( item.Rect.Shrink( 1 ), Theme.ControlRadius );
		}
	}

	private void OnAssetActivated( object item )
	{
		if ( item is not ConnecterAssetRecord record )
			return;

		if ( record.IsDirectory )
		{
			NavigateTo( CurrentRepository, record.FullPath );
			return;
		}

		ImportRecords( [record] );
	}

	private void OpenAssetContextMenu( object item )
	{
		if ( item is not ConnecterAssetRecord record )
			return;

		var selected = GetSelectedRecords().ToList();
		if ( selected.Count == 0 || !selected.Contains( record ) )
			selected = [record];

		var menu = new ContextMenu( this ) { Searchable = true };

		menu.AddOption( selected.Count == 1 ? "Import" : $"Import {selected.Count} Items", "download", () => ImportRecords( selected ) )
			.Enabled = selected.Any( x => x.CanImport );

		menu.AddOption( "Reveal Source", "folder_open", () => EditorUtility.OpenFolder( record.FullPath ) );
		menu.AddOption( "Copy Source Path", "content_copy", () => EditorUtility.Clipboard.Copy( record.FullPath ) );

		if ( record.Warning is not null )
		{
			menu.AddSeparator();
			menu.AddOption( record.Warning, "warning" ).Enabled = false;
		}

		menu.OpenAtCursor();
	}

	private bool StartAssetDrag( object item )
	{
		if ( item is not ConnecterAssetRecord record || record.IsDirectory )
			return false;

		if ( !record.CanImport )
		{
			ShowWarning( record.Warning );
			return false;
		}

		try
		{
			StatusLabel.Text = $"Importing {record.Name}...";
			var imported = ImportRecord( record );
			if ( imported.PrimaryAsset is null )
				return false;

			var drag = new Drag( AssetList );
			drag.Data.Text = imported.PrimaryAsset.RelativePath;
			drag.Data.Url = new Uri( "file:///" + imported.PrimaryAsset.AbsolutePath );
			drag.Execute();

			StatusLabel.Text = imported.Result.Message;
			return true;
		}
		catch ( Exception exception )
		{
			StatusLabel.Text = $"Import failed: {exception.Message}";
			return false;
		}
	}

	private void ImportSelected()
	{
		ImportRecords( GetSelectedRecords().ToList() );
	}

	private void ImportRecords( IReadOnlyList<ConnecterAssetRecord> records )
	{
		records = records.Where( x => x.CanImport ).ToList();
		if ( records.Count == 0 )
			return;

		var folder = records.FirstOrDefault( x => x.IsDirectory );
		if ( folder is not null )
		{
			var stats = ConnecterImporter.GetDirectoryStats( folder.FullPath );
			var confirm = new PopupWindow(
				"Import folder?",
				$"This will copy {stats.Count:n0} files ({stats.Bytes.SizeFormat()}) into the current project's Assets/ConnecterImports folder.",
				"Cancel",
				new Dictionary<string, Action>
				{
					["Import"] = () => ImportRecordsConfirmed( records )
				} );

			confirm.Show();
			return;
		}

		ImportRecordsConfirmed( records );
	}

	private void ImportRecordsConfirmed( IReadOnlyList<ConnecterAssetRecord> records )
	{
		Asset lastAsset = null;

		foreach ( var record in records )
		{
			try
			{
				StatusLabel.Text = $"Importing {record.Name}...";
				var imported = ImportRecord( record );
				lastAsset = imported.PrimaryAsset ?? lastAsset;
				StatusLabel.Text = imported.Result.Message;
			}
			catch ( Exception exception )
			{
				StatusLabel.Text = $"Import failed: {exception.Message}";
				ShowWarning( exception.Message );
				break;
			}
		}

		MainAssetBrowser.Instance?.Local.UpdateAssetList();

		if ( lastAsset is not null )
		{
			MainAssetBrowser.Instance?.Local.FocusOnAsset( lastAsset, true );
			EditorUtility.InspectorObject = lastAsset;
		}
	}

	private ConnecterEditorImportResult ImportRecord( ConnecterAssetRecord record )
	{
		var options = new ConnecterImportOptions( Sandbox.Project.Current.GetAssetsPath() );
		return ConnecterImporter.Import( record, options );
	}

	private IEnumerable<ConnecterAssetRecord> GetSelectedRecords()
	{
		return AssetList.SelectedItems.OfType<ConnecterAssetRecord>();
	}

	private void ShowWarning( string text )
	{
		var popup = new PopupWindow( "Connecter Browser", text ?? "Unsupported asset.", "OK" );
		popup.Show();
	}

	private static string GetKindIcon( ConnecterAssetRecord record )
	{
		if ( record.IsDirectory )
			return "folder";

		return record.Kind switch
		{
			ConnecterAssetKind.ModelSource => "view_in_ar",
			ConnecterAssetKind.SboxModel => "deployed_code",
			ConnecterAssetKind.Image => "image",
			ConnecterAssetKind.Audio => "volume_up",
			ConnecterAssetKind.Material => "texture",
			ConnecterAssetKind.Unsupported => "warning",
			_ => "insert_drive_file"
		};
	}

	private static string GetFilterIcon( ConnecterBrowserFilter filter )
	{
		return filter switch
		{
			ConnecterBrowserFilter.Models => "view_in_ar",
			ConnecterBrowserFilter.Images => "image",
			ConnecterBrowserFilter.Audio => "volume_up",
			ConnecterBrowserFilter.Sbox => "deployed_code",
			ConnecterBrowserFilter.Unsupported => "warning",
			_ => "filter_list"
		};
	}

	private static Color GetKindColor( ConnecterAssetKind kind )
	{
		return kind switch
		{
			ConnecterAssetKind.ModelSource or ConnecterAssetKind.SboxModel => Color.Cyan,
			ConnecterAssetKind.Image or ConnecterAssetKind.Material => Color.Green,
			ConnecterAssetKind.Audio => Color.Orange,
			ConnecterAssetKind.Unsupported => Theme.Yellow,
			ConnecterAssetKind.Unknown => Color.Gray,
			_ => Theme.Text
		};
	}
}

public sealed record ConnecterLocation( ConnecterRepository Repository, string FullPath );

file sealed class ConnecterLocationNode : TreeNode
{
	private readonly ConnecterRepository Repository;
	private readonly string FullPath;
	private readonly Action<ConnecterRepository, string> Navigate;

	public ConnecterLocationNode( ConnecterRepository repository, string fullPath, Action<ConnecterRepository, string> navigate )
	{
		Repository = repository;
		FullPath = fullPath;
		Navigate = navigate;
		Value = string.Equals( repository.FullPath, fullPath, StringComparison.OrdinalIgnoreCase )
			? repository
			: new ConnecterLocation( repository, fullPath );
	}

	public override bool HasChildren => SafeEnumerateDirectories( FullPath ).Any();

	protected override void BuildChildren()
	{
		if ( !Directory.Exists( FullPath ) )
		{
			ClearChildren();
			return;
		}

		var directories = SafeEnumerateDirectories( FullPath )
			.Where( x => !Path.GetFileName( x ).StartsWith( "." ) )
			.OrderBy( Path.GetFileName, StringComparer.OrdinalIgnoreCase )
			.Select( x => new ConnecterLocation( Repository, x ) );

		SetChildren( directories, x => new ConnecterLocationNode( x.Repository, x.FullPath, Navigate ) );
	}

	public override void OnSelectionChanged( bool state )
	{
		if ( state )
			Navigate( Repository, FullPath );
	}

	public override void OnActivated()
	{
		Navigate( Repository, FullPath );
	}

	public override string GetTooltip()
	{
		return FullPath;
	}

	public override void OnPaint( VirtualWidget item )
	{
		PaintSelection( item );

		var rect = item.Rect;
		var name = Value is ConnecterRepository repository ? repository.Name : Path.GetFileName( FullPath );

		Paint.SetPen( item.Selected ? Color.White : Theme.Text );
		Paint.DrawIcon( rect.Shrink( 4, 2 ), Value is ConnecterRepository ? "perm_media" : "folder", 18, TextFlag.LeftCenter );

		rect.Left += 26;
		Paint.DrawText( rect, name, TextFlag.LeftCenter | TextFlag.SingleLine );
	}

	private static IEnumerable<string> SafeEnumerateDirectories( string folderPath )
	{
		if ( !Directory.Exists( folderPath ) )
			return [];

		try
		{
			return Directory.EnumerateDirectories( folderPath );
		}
		catch
		{
			return [];
		}
	}
}
