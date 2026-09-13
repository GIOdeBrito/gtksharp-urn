using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Gtk;

namespace UrnWrapper
{
	internal static class Program
	{
		private const string SearchPlaceholder = "Search...";
		private const string ItemsFileName = "items.json";
		private const string ConfigFileName = "config.json";

		private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
		{
			PropertyNameCaseInsensitive = true,
			WriteIndented = true,
		};

		private static Config AppConfig = new Config("");

		[STAThread]
		private static void Main()
		{
			Application.Init();

			AppConfig = LoadConfig();

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

			var store = new ListStore(typeof(string), typeof(string), typeof(string));
			List<SandboxProfile> items = LoadItems();
			PopulateStore(store, items);

			var filter = new TreeModelFilter(store, null);
			filter.VisibleFunc = (model, iter) => RowMatchesSearch(model, iter, searchEntry.Text);

			searchEntry.SearchChanged += (sender, args) =>
			{
				filter.Refilter();
			};

			var addButton = new Button("Add");
			addButton.Clicked += (sender, args) =>
			{
				ShowAddItemDialog(window, store, filter, items);
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
			table.AppendColumn(BuildTextColumn("Last executed", 1));

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

		private static void ShowAddItemDialog(Window parent, ListStore store, TreeModelFilter filter, List<SandboxProfile> items)
		{
			using (var dialog = new Dialog("Add Item", parent, DialogFlags.Modal))
			{
				dialog.AddButton("Cancel", ResponseType.Cancel);
				dialog.AddButton("Add", ResponseType.Accept);

				var nameEntry = new Entry();
				var commandEntry = new Entry();

				Box contentArea = dialog.ContentArea;
				contentArea.Spacing = 6;
				contentArea.Margin = 6;

				contentArea.PackStart(BuildLabeledRow("Name:", nameEntry), false, false, 0);
				contentArea.PackStart(BuildLabeledRow("Command:", commandEntry), false, false, 0);

				dialog.ShowAll();

				ResponseType response = (ResponseType)dialog.Run();

				if (response == ResponseType.Accept && !string.IsNullOrWhiteSpace(nameEntry.Text))
				{
					var profile = new SandboxProfile(nameEntry.Text.Trim(), commandEntry.Text.Trim(), null);
					items.Add(profile);
					SaveItems(GetItemsFilePath(), items);
					store.AppendValues(profile.Name, FormatLastExecuted(profile.LastExecuted), profile.Command);
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

		private static void PopulateStore(ListStore store, List<SandboxProfile> items)
		{
			foreach (SandboxProfile profile in items)
			{
				store.AppendValues(profile.Name, FormatLastExecuted(profile.LastExecuted), profile.Command);
			}
		}

		private static List<SandboxProfile> LoadItems()
		{
			string filePath = GetItemsFilePath();

			if (!File.Exists(filePath))
			{
				var empty = new List<SandboxProfile>();
				SaveItems(filePath, empty);
				return empty;
			}

			string json = File.ReadAllText(filePath);
			List<SandboxProfile>? items = JsonSerializer.Deserialize<List<SandboxProfile>>(json, JsonOptions);

			return items ?? new List<SandboxProfile>();
		}

		private static string GetItemsFilePath()
		{
			return Path.Combine(AppContext.BaseDirectory, ItemsFileName);
		}

		private static Config LoadConfig()
		{
			string filePath = GetConfigFilePath();

			if (!File.Exists(filePath))
			{
				var defaults = new Config("");
				SaveConfig(filePath, defaults);
				return defaults;
			}

			string json = File.ReadAllText(filePath);
			Config? config = JsonSerializer.Deserialize<Config>(json, JsonOptions);

			return config ?? new Config("");
		}

		private static void SaveConfig(string filePath, Config config)
		{
			string json = JsonSerializer.Serialize(config, JsonOptions);
			File.WriteAllText(filePath, json);
		}

		private static string GetConfigFilePath()
		{
			return Path.Combine(AppContext.BaseDirectory, ConfigFileName);
		}

		private static void SaveItems(string filePath, IReadOnlyList<SandboxProfile> items)
		{
			string json = JsonSerializer.Serialize(items, JsonOptions);
			File.WriteAllText(filePath, json);
		}

		private static string FormatLastExecuted(DateTime? lastExecuted)
		{
			if (!lastExecuted.HasValue)
			{
				return "Never";
			}

			return lastExecuted.Value.ToString("yyyy-MM-dd HH:mm");
		}

		private static bool RowMatchesSearch(ITreeModel model, TreeIter iter, string searchText)
		{
			if (string.IsNullOrWhiteSpace(searchText))
			{
				return true;
			}

			string normalizedSearch = searchText.Trim();
			string name = (string)model.GetValue(iter, 0);
			string command = (string)model.GetValue(iter, 2);

			return ContainsIgnoreCase(name, normalizedSearch)
				|| ContainsIgnoreCase(command, normalizedSearch);
		}

		private static bool ContainsIgnoreCase(string source, string searchText)
		{
			if (string.IsNullOrEmpty(source))
			{
				return false;
			}

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
			column.Title = "Actions";
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

		private sealed class SandboxProfile
		{
			public SandboxProfile(string name, string command, DateTime? lastExecuted)
			{
				Name = name;
				Command = command;
				LastExecuted = lastExecuted;
			}

			public string Name { get; }

			public string Command { get; }

			public DateTime? LastExecuted { get; }
		}

		private sealed class Config
		{
			public Config(string defaultHome)
			{
				DefaultHome = defaultHome;
			}

			[JsonPropertyName("defaultHome")]
			public string DefaultHome { get; }
		}
	}
}