using System;
using System.Collections.Generic;
using Gtk;

namespace UrnWrapper
{
	internal static class Program
	{
		private const string SearchPlaceholder = "Search...";

		[STAThread]
		private static void Main()
		{
			Application.Init();

			Window mainWindow = BuildMainWindow();
			mainWindow.ShowAll();
			DrainPendingEvents();

			Application.Run();
		}

		private static void DrainPendingEvents()
		{
			// Let the display backend (re)size the window before the first
			// paint, so the initial frame never targets an infinite surface.
			while (GLib.MainContext.Iteration(false))
			{
			}
		}

		private static Window BuildMainWindow()
		{
			var window = new Window("UrnWrapper");
			window.SetSizeRequest(600, 400);

			var layout = new Box(Orientation.Vertical, 6);
			layout.Margin = 6;

			var searchEntry = new SearchEntry();
			searchEntry.PlaceholderText = SearchPlaceholder;
			searchEntry.PrimaryIconName = "edit-find-symbolic";

			ScrolledWindow tableContainer = BuildTableContainer(searchEntry);

			layout.PackStart(searchEntry, false, false, 0);
			layout.PackStart(tableContainer, true, true, 0);

			window.Add(layout);
			window.DeleteEvent += OnWindowDelete;

			return window;
		}

		private static ScrolledWindow BuildTableContainer(SearchEntry searchEntry)
		{
			var store = new ListStore(typeof(string), typeof(string), typeof(int));
			PopulateStore(store);

			var filter = new TreeModelFilter(store, null);
			filter.VisibleFunc = (model, iter) => RowMatchesSearch(model, iter, searchEntry.Text);

			searchEntry.SearchChanged += (sender, args) =>
			{
				filter.Refilter();
			};

			var table = new TreeView(filter);
			table.AppendColumn(BuildTextColumn("Name", 0));
			table.AppendColumn(BuildTextColumn("Category", 1));
			table.AppendColumn(BuildTextColumn("Quantity", 2));

			var scrolledWindow = new ScrolledWindow();
			scrolledWindow.ShadowType = ShadowType.In;
			scrolledWindow.Add(table);

			return scrolledWindow;
		}

		private static void PopulateStore(ListStore store)
		{
			foreach (SampleItem item in GetSampleItems())
			{
				store.AppendValues(item.Name, item.Category, item.Quantity);
			}
		}

		private static IReadOnlyList<SampleItem> GetSampleItems()
		{
			return new[]
			{
				new SampleItem("Apple", "Fruit", 12),
				new SampleItem("Banana", "Fruit", 8),
				new SampleItem("Carrot", "Vegetable", 5),
				new SampleItem("Bread", "Bakery", 3),
				new SampleItem("Milk", "Dairy", 2),
			};
		}

		private static bool RowMatchesSearch(ITreeModel model, TreeIter iter, string searchText)
		{
			if (string.IsNullOrWhiteSpace(searchText))
			{
				return true;
			}

			string normalizedSearch = searchText.Trim();
			string name = (string)model.GetValue(iter, 0);
			string category = (string)model.GetValue(iter, 1);
			int quantity = (int)model.GetValue(iter, 2);

			return ContainsIgnoreCase(name, normalizedSearch)
				|| ContainsIgnoreCase(category, normalizedSearch)
				|| ContainsIgnoreCase(quantity.ToString(), normalizedSearch);
		}

		private static bool ContainsIgnoreCase(string source, string searchText)
		{
			return source.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >= 0;
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

		private static void OnWindowDelete(object sender, DeleteEventArgs args)
		{
			Application.Quit();
			args.RetVal = true;
		}

		private sealed class SampleItem
		{
			public SampleItem(string name, string category, int quantity)
			{
				Name = name;
				Category = category;
				Quantity = quantity;
			}

			public string Name { get; }

			public string Category { get; }

			public int Quantity { get; }
		}
	}
}