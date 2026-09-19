using System;
using System.IO;
using Gtk;
using UrnWrapper.Models;
using UrnWrapper.Persistence;

namespace UrnWrapper.UI
{
	internal static class ConfigDialog
	{
		internal static void Show(Window parent)
		{
			Config current = AppStorage.LoadConfig();

			using (var dialog = new Dialog("Config", parent, DialogFlags.Modal))
			{
				dialog.AddButton("Cancel", ResponseType.Cancel);
				dialog.AddButton("Save", ResponseType.Accept);

				var homeEntry = new Entry();
				homeEntry.Text = current.DefaultHome;

				var browseButton = new Button("Browse...");
				browseButton.Clicked += (sender, args) =>
				{
					BrowseForFolder(dialog, homeEntry);
				};

				Box contentArea = dialog.ContentArea;
				contentArea.Spacing = 6;
				contentArea.Margin = 6;

				contentArea.PackStart(BuildHomeRow(homeEntry, browseButton), false, false, 0);

				dialog.ShowAll();

				ResponseType response = (ResponseType)dialog.Run();
				string newHome = homeEntry.Text.Trim();
				dialog.Destroy();

				if (response != ResponseType.Accept)
				{
					return;
				}

				if (string.IsNullOrWhiteSpace(newHome))
				{
					ShowError(parent, "Default home must not be empty.");
					return;
				}

				if (!Directory.Exists(newHome))
				{
					ShowError(parent, "Selected folder does not exist.");
					return;
				}

				if (string.Equals(newHome, current.DefaultHome, StringComparison.Ordinal))
				{
					return;
				}

				var updated = new Config(newHome, current.DefaultCommand);

				if (!AppStorage.TrySaveConfig(AppStorage.GetConfigFilePath(), updated))
				{
					ShowError(parent, "Could not save config.json.");
					return;
				}
			}
		}

		private static Box BuildHomeRow(Entry homeEntry, Button browseButton)
		{
			var label = new Label("Default home:");
			label.Xalign = 0;

			var row = new Box(Orientation.Horizontal, 6);
			row.PackStart(label, false, false, 0);
			row.PackStart(homeEntry, true, true, 0);
			row.PackStart(browseButton, false, false, 0);

			return row;
		}

		private static void BrowseForFolder(Dialog parent, Entry homeEntry)
		{
			using (var chooser = new FileChooserDialog("Select Default Home", parent, FileChooserAction.SelectFolder, "Cancel", ResponseType.Cancel, "Open", ResponseType.Accept))
			{
				SetInitialFolder(chooser, homeEntry.Text.Trim());

				ResponseType response = (ResponseType)chooser.Run();
				string? picked = chooser.Filename;
				chooser.Destroy();

				if (response != ResponseType.Accept)
				{
					return;
				}

				if (string.IsNullOrWhiteSpace(picked))
				{
					return;
				}

				homeEntry.Text = picked.Trim();
			}
		}

		private static void SetInitialFolder(FileChooserDialog chooser, string currentHome)
		{
			if (string.IsNullOrWhiteSpace(currentHome))
			{
				return;
			}

			if (!Directory.Exists(currentHome))
			{
				return;
			}

			chooser.SetCurrentFolder(currentHome);
		}

		private static void ShowError(Window parent, string message)
		{
			using (var error = new MessageDialog(parent, DialogFlags.Modal, MessageType.Error, ButtonsType.Ok, message))
			{
				error.Run();
				error.Destroy();
			}
		}
	}
}
