using System;
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
		private static bool isDialogOpen;

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

			TreeView table = BuildProfileTable(window, store, filter, items);

			var configButton = new Button("Config");
			configButton.Clicked += (sender, args) =>
			{
				ConfigDialog.Show(window);
			};

			var headerBar = new Box(Orientation.Horizontal, 6);
			headerBar.PackStart(searchEntry, true, true, 0);
			headerBar.PackStart(addButton, false, false, 0);
			headerBar.PackStart(configButton, false, false, 0);

			ScrolledWindow tableContainer = WrapInScrolledWindow(table);

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

		private static TreeView BuildProfileTable(Window parent, ListStore store, TreeModelFilter filter, List<SandboxProfile> items)
		{
			var table = new TreeView(filter);
			table.Selection.Mode = SelectionMode.Single;
			table.AppendColumn(BuildTextColumn("Name", StoreColumns.Name));
			table.AppendColumn(BuildTextColumn("Last executed", StoreColumns.LastExecuted));

			TreeViewColumn editColumn = BuildActionColumn("Edit", "document-edit-symbolic");
			TreeViewColumn removeColumn = BuildActionColumn("Remove", "edit-delete-symbolic");
			table.AppendColumn(BuildActionColumn("Run", "media-playback-start-symbolic"));
			table.AppendColumn(editColumn);
			table.AppendColumn(removeColumn);

			table.ButtonPressEvent += (sender, args) =>
			{
				DispatchActionClick(parent, table, store, filter, items, editColumn, removeColumn, args);
			};

			return table;
		}

		private static ScrolledWindow WrapInScrolledWindow(TreeView table)
		{
			var scrolledWindow = new ScrolledWindow();
			scrolledWindow.ShadowType = ShadowType.In;
			scrolledWindow.Add(table);

			return scrolledWindow;
		}

		private static void DispatchActionClick(Window parent, TreeView table, ListStore store, TreeModelFilter filter, List<SandboxProfile> items, TreeViewColumn editColumn, TreeViewColumn removeColumn, ButtonPressEventArgs args)
		{
			if (args.Event == null)
			{
				return;
			}

			if (args.Event.Button != 1)
			{
				return;
			}

			TreePath? clickedPath;
			TreeViewColumn? clickedColumn;
			int cellX;
			int cellY;

			if (!table.GetPathAtPos((int)args.Event.X, (int)args.Event.Y, out clickedPath, out clickedColumn, out cellX, out cellY))
			{
				return;
			}

			if (!TryGetStoreIter(filter, clickedPath, out TreeIter storeIter))
			{
				return;
			}

			if (clickedColumn == editColumn)
			{
				EditStoreRow(parent, store, filter, items, storeIter);
				return;
			}

			if (clickedColumn == removeColumn)
			{
				RemoveStoreRow(parent, store, filter, items, storeIter);
				return;
			}
		}

		private static bool TryGetStoreIter(TreeModelFilter filter, TreePath? path, out TreeIter storeIter)
		{
			storeIter = TreeIter.Zero;

			if (path == null)
			{
				return false;
			}

			if (!filter.GetIter(out TreeIter filterIter, path))
			{
				return false;
			}

			storeIter = filter.ConvertIterToChildIter(filterIter);
			return true;
		}

		private static void EditStoreRow(Window parent, ListStore store, TreeModelFilter filter, List<SandboxProfile> items, TreeIter storeIter)
		{
			if (isDialogOpen)
			{
				return;
			}

			if (!TryResolveRow(store, items, storeIter, out int itemIndex))
			{
				return;
			}

			isDialogOpen = true;

			try
			{
				EditItemDialog.Show(parent, store, filter, items, itemIndex, storeIter);
			}
			finally
			{
				isDialogOpen = false;
			}
		}

		private static void RemoveStoreRow(Window parent, ListStore store, TreeModelFilter filter, List<SandboxProfile> items, TreeIter storeIter)
		{
			if (isDialogOpen)
			{
				return;
			}

			if (!TryResolveRow(store, items, storeIter, out int itemIndex))
			{
				return;
			}

			isDialogOpen = true;

			try
			{
				RemoveItemDialog.Show(parent, store, filter, items, itemIndex, storeIter);
			}
			finally
			{
				isDialogOpen = false;
			}
		}

		private static bool TryResolveRow(ListStore store, List<SandboxProfile> items, TreeIter storeIter, out int itemIndex)
		{
			string? name = store.GetValue(storeIter, StoreColumns.Name) as string;

			if (string.IsNullOrEmpty(name))
			{
				itemIndex = -1;
				return false;
			}

			itemIndex = FindItemIndex(items, name);

			if (itemIndex < 0)
			{
				return false;
			}

			return true;
		}

		private static int FindItemIndex(List<SandboxProfile> items, string name)
		{
			for (int i = 0; i < items.Count; i++)
			{
				if (string.Equals(items[i].Name, name, StringComparison.OrdinalIgnoreCase))
				{
					return i;
				}
			}

			return -1;
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

		private static TreeViewColumn BuildActionColumn(string title, string iconName)
		{
			// Run cells are visual only in this scope. A left-click
			// on the Edit column opens the edit dialog for that row,
			// while the Remove column asks for confirmation first.
			var icon = new CellRendererPixbuf();
			icon.IconName = iconName;
			var text = new CellRendererText();
			text.Text = title;

			var column = new TreeViewColumn();
			column.Title = title;
			column.PackStart(icon, false);
			column.PackStart(text, false);

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
