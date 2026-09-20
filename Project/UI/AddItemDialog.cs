using System;
using System.Collections.Generic;
using Gtk;
using UrnWrapper.Models;
using UrnWrapper.Persistence;

namespace UrnWrapper.UI
{
	internal static class AddItemDialog
	{
		internal static void Show(Window parent, ListStore store, TreeModelFilter filter, List<SandboxProfile> items)
		{
			using (var dialog = new Dialog("Add Item", parent, DialogFlags.Modal))
			{
				dialog.AddButton("Cancel", ResponseType.Cancel);
				dialog.AddButton("Add", ResponseType.Accept);
				DialogSizing.Apply(dialog);

				var nameEntry = new Entry();
				var commandEntry = new Entry();
				SandboxPermissionControls permissions = SandboxPermissionControls.FromOptions(AppStorage.DefaultOptions);

				Box contentArea = dialog.ContentArea;
				contentArea.Spacing = 6;
				contentArea.Margin = 6;

				contentArea.PackStart(BuildLabeledRow("Name:", nameEntry), false, false, 0);
				contentArea.PackStart(BuildLabeledRow("Command:", commandEntry), false, false, 0);
				contentArea.PackStart(permissions.BuildFrame(), false, false, 0);

				dialog.ShowAll();

				ResponseType response = (ResponseType)dialog.Run();
				string name = nameEntry.Text.Trim();
				string command = commandEntry.Text.Trim();

				if (response != ResponseType.Accept)
				{
					return;
				}

				if (string.IsNullOrWhiteSpace(name))
				{
					return;
				}

				if (string.IsNullOrWhiteSpace(command))
				{
					ShowError(parent, "Command must not be empty.");
					return;
				}

				if (IsDuplicateName(items, name))
				{
					ShowError(parent, "An item with that name already exists.");
					return;
				}

				var profile = new SandboxProfile(name, command, null, permissions.ToOptions());
				items.Add(profile);

				if (!AppStorage.TrySaveItems(AppStorage.GetItemsFilePath(), items))
				{
					items.RemoveAt(items.Count - 1);
					ShowError(parent, "Could not save items.json.");
					return;
				}

				store.AppendValues(profile.Name, ProfileFormatting.FormatLastExecuted(profile.LastExecuted), profile.Command);
				filter.Refilter();
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

		private static bool IsDuplicateName(List<SandboxProfile> items, string name)
		{
			foreach (SandboxProfile profile in items)
			{
				if (string.Equals(profile.Name, name, StringComparison.OrdinalIgnoreCase))
				{
					return true;
				}
			}

			return false;
		}

		private static void ShowError(Window parent, string message)
		{
			using (var error = new MessageDialog(parent, DialogFlags.Modal, MessageType.Error, ButtonsType.Ok, message))
			{
				DialogSizing.ApplyMessage(error);
				error.Run();
			}
		}
	}
}
