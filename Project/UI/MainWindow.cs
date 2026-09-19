using System.Collections.Generic;
using Gtk;
using UrnWrapper.Models;
using UrnWrapper.Persistence;

namespace UrnWrapper.UI
{
	internal static class MainWindow
	{
		private const string WindowTitle = "UrnWrapper";
		private const string SearchPlaceholder = "Search...";
		private const int MaxDrainIterations = 1000;

		internal static Window Build()
		{
			var window = new Window(WindowTitle);
			window.SetSizeRequest(600, 400);
			window.SetPosition(WindowPosition.Center);

			var layout = new Box(Orientation.Vertical, 6);
			layout.Margin = 6;

			var searchEntry = new SearchEntry();
			searchEntry.PlaceholderText = SearchPlaceholder;
			searchEntry.PrimaryIconName = "edit-find-symbolic";

			var store = new ListStore(typeof(string), typeof(string), typeof(string));
			List<SandboxProfile> items = AppStorage.LoadItems();
			PopulateStore(store, items);

			var filter = new TreeModelFilter(store, null);
			filter.VisibleFunc = (model, iter) => ProfileSearch.RowMatchesSearch(model, iter, searchEntry.Text);

			searchEntry.SearchChanged += (sender, args) =>
			{
				filter.Refilter();
			};

			var addButton = new Button("Add");
			addButton.Clicked += (sender, args) =>
			{
				AddItemDialog.Show(window, store, filter, items);
			};

			var configButton = new Button("Config");
			configButton.Clicked += (sender, args) =>
			{
				ConfigDialog.Show(window);
			};

			var headerBar = new Box(Orientation.Horizontal, 6);
			headerBar.PackStart(searchEntry, true, true, 0);
			headerBar.PackStart(addButton, false, false, 0);
			headerBar.PackStart(configButton, false, false, 0);

			ScrolledWindow tableContainer = BuildTableContainer(filter);

			layout.PackStart(headerBar, false, false, 0);
			layout.PackStart(tableContainer, true, true, 0);

			window.Add(layout);
			window.DeleteEvent += OnWindowDelete;

			return window;
		}

		internal static void DrainPendingEvents()
		{
			// Let the display backend (re)size the window before the first
			// paint, so the initial frame never targets an infinite surface.
			// Bounded so a misbehaving backend cannot spin forever.
			for (int i = 0; i < MaxDrainIterations; i++)
			{
				if (!GLib.MainContext.Iteration(false))
				{
					break;
				}
			}
		}

		private static ScrolledWindow BuildTableContainer(TreeModelFilter filter)
		{
			var table = new TreeView(filter);
			table.AppendColumn(BuildTextColumn("Name", StoreColumns.Name));
			table.AppendColumn(BuildTextColumn("Last executed", StoreColumns.LastExecuted));

			TreeViewColumn actionColumn = BuildActionColumn();
			table.AppendColumn(actionColumn);

			var scrolledWindow = new ScrolledWindow();
			scrolledWindow.ShadowType = ShadowType.In;
			scrolledWindow.Add(table);

			return scrolledWindow;
		}

		private static TreeViewColumn BuildTextColumn(string title, int columnIndex)
		{
			var cellRenderer = new CellRendererText();
			var column = new TreeViewColumn();
			column.Title = title;
			column.PackStart(cellRenderer, true);
			column.AddAttribute(cellRenderer, "text", columnIndex);

			return column;
		}

		private static TreeViewColumn BuildActionColumn()
		{
			// Intentionally visual only: the Run/Edit/Remove cells are not
			// wired to any handler in this cleanup scope.
			var runIcon = new CellRendererPixbuf();
			runIcon.IconName = "media-playback-start-symbolic";
			var runText = new CellRendererText();
			runText.Text = "Run";

			var editIcon = new CellRendererPixbuf();
			editIcon.IconName = "document-edit-symbolic";
			var editText = new CellRendererText();
			editText.Text = "Edit";

			var removeIcon = new CellRendererPixbuf();
			removeIcon.IconName = "edit-delete-symbolic";
			var removeText = new CellRendererText();
			removeText.Text = "Remove";

			var column = new TreeViewColumn();
			column.Title = "Actions";
			column.Sizing = TreeViewColumnSizing.Fixed;
			column.FixedWidth = 210;
			column.PackStart(runIcon, false);
			column.PackStart(runText, false);
			column.PackStart(editIcon, false);
			column.PackStart(editText, false);
			column.PackStart(removeIcon, false);
			column.PackStart(removeText, false);

			return column;
		}

		private static void PopulateStore(ListStore store, List<SandboxProfile> items)
		{
			foreach (SandboxProfile profile in items)
			{
				store.AppendValues(profile.Name, ProfileFormatting.FormatLastExecuted(profile.LastExecuted), profile.Command);
			}
		}

		private static void OnWindowDelete(object sender, DeleteEventArgs args)
		{
			// Quit ends the main loop, so suppress the default destroy handler.
			Application.Quit();
			args.RetVal = true;
		}
	}
}
