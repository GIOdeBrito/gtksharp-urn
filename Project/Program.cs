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
			window.SetPosition(WindowPosition.Center);

			var layout = new Box(Orientation.Vertical, 6);
			layout.Margin = 6;

			var searchEntry = new SearchEntry();
			searchEntry.PlaceholderText = SearchPlaceholder;
			searchEntry.PrimaryIconName = "edit-find-symbolic";

			var store = new ListStore(typeof(string), typeof(string), typeof(int));
			PopulateStore(store);

			var filter = new TreeModelFilter(store, null);
			filter.VisibleFunc = (model, iter) => RowMatchesSearch(model, iter, searchEntry.Text);

			searchEntry.SearchChanged += (sender, args) =>
			{
				filter.Refilter();
			};

			var addButton = new Button("Add");
			addButton.Clicked += (sender, args) =>
			{
				ShowAddItemDialog(window, store, filter);
			};

			var headerBar = new Box(Orientation.Horizontal, 6);
			headerBar.PackStart(searchEntry, true, true, 0);
			headerBar.PackStart(addButton, false, false, 0);

			ScrolledWindow tableContainer = BuildTableContainer(filter);

			layout.PackStart(headerBar, false, false, 0);
			layout.PackStart(tableContainer, true, true, 0);

			window.Add(layout);
			window.DeleteEvent += OnWindowDelete;

			return window;
		}

		private static ScrolledWindow BuildTableContainer(TreeModelFilter filter)
		{
			var table = new TreeView(filter);
			table.AppendColumn(BuildTextColumn("Name", 0));
			table.AppendColumn(BuildTextColumn("Category", 1));
			table.AppendColumn(BuildTextColumn("Quantity", 2));

			TreeViewColumn actionColumn = BuildActionColumn();
			table.AppendColumn(actionColumn);

			table.ButtonPressEvent += (sender, args) =>
			{
				OnActionColumnPressed(table, actionColumn, args);
			};

			var scrolledWindow = new ScrolledWindow();
			scrolledWindow.ShadowType = ShadowType.In;
			scrolledWindow.Add(table);

			return scrolledWindow;
		}

		private static void OnActionColumnPressed(TreeView table, TreeViewColumn actionColumn, ButtonPressEventArgs args)
		{
			if (args.Event.Button != 1)
			{
				return;
			}

			if (table.GetPathAtPos((int)args.Event.X, (int)args.Event.Y, out TreePath path, out TreeViewColumn column))
			{
				if (column == actionColumn)
				{
					Console.WriteLine("hello");
				}
			}
		}

		private static void ShowAddItemDialog(Window parent, ListStore store, TreeModelFilter filter)
		{
			using (var dialog = new Dialog("Add Item", parent, DialogFlags.Modal))
			{
				dialog.AddButton("Cancel", ResponseType.Cancel);
				dialog.AddButton("Add", ResponseType.Accept);

				var nameEntry = new Entry();
				var categoryEntry = new Entry();
				var quantitySpin = new SpinButton(0, 9999, 1);
				quantitySpin.Value = 1;

				Box contentArea = dialog.ContentArea;
				contentArea.Spacing = 6;
				contentArea.Margin = 6;

				contentArea.PackStart(BuildLabeledRow("Name:", nameEntry), false, false, 0);
				contentArea.PackStart(BuildLabeledRow("Category:", categoryEntry), false, false, 0);
				contentArea.PackStart(BuildLabeledRow("Quantity:", quantitySpin), false, false, 0);

				dialog.ShowAll();

				ResponseType response = (ResponseType)dialog.Run();

				if (response == ResponseType.Accept && !string.IsNullOrWhiteSpace(nameEntry.Text))
				{
					store.AppendValues(nameEntry.Text.Trim(), categoryEntry.Text.Trim(), quantitySpin.ValueAsInt);
					filter.Refilter();
				}
			}
		}

		private static Box BuildLabeledRow(string labelText, Widget inputWidget)
		{
			var label = new Label(labelText);
			label.Xalign = 1;
			label.WidthRequest = 80;

			var row = new Box(Orientation.Horizontal, 6);
			row.PackStart(label, false, false, 0);
			row.PackStart(inputWidget, true, true, 0);

			return row;
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

		private static TreeViewColumn BuildActionColumn()
		{
			var iconRenderer = new CellRendererPixbuf();
			iconRenderer.IconName = "face-smile-symbolic";

			var textRenderer = new CellRendererText();
			textRenderer.Text = "Hello";

			var column = new TreeViewColumn();
			column.Title = "Action";
			column.Sizing = TreeViewColumnSizing.Fixed;
			column.FixedWidth = 90;
			column.PackStart(iconRenderer, false);
			column.PackStart(textRenderer, false);

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