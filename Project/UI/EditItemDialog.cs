using System;
using System.Collections.Generic;
using Gtk;
using UrnWrapper.Models;
using UrnWrapper.Persistence;

namespace UrnWrapper.UI
{
	internal static class EditItemDialog
	{
		internal static void Show(Window parent, ListStore store, TreeModelFilter filter, List<SandboxProfile> items, int itemIndex, TreeIter storeIter)
		{
			if (itemIndex < 0)
			{
				return;
			}

			if (itemIndex >= items.Count)
			{
				return;
			}

			SandboxProfile original = items[itemIndex];

			using (var dialog = new Dialog("Edit Item", parent, DialogFlags.Modal))
			{
				dialog.AddButton("Cancel", ResponseType.Cancel);
				dialog.AddButton("Save", ResponseType.Accept);

				var nameEntry = new Entry();
				nameEntry.Text = original.Name;

				var commandEntry = new Entry();
				commandEntry.Text = original.Command;

				SandboxPermissionControls permissions = SandboxPermissionControls.FromOptions(AppStorage.NormalizeOptions(original.Options));

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

				if (IsDuplicateName(items, name, itemIndex))
				{
					ShowError(parent, "An item with that name already exists.");
					return;
				}

				if (IsUnchanged(original, name, command, permissions.ToOptions()))
				{
					return;
				}

				var updated = new SandboxProfile(name, command, original.LastExecuted, permissions.ToOptions());
				items[itemIndex] = updated;

				if (!AppStorage.TrySaveItems(AppStorage.GetItemsFilePath(), items))
				{
					items[itemIndex] = original;
					ShowError(parent, "Could not save items.json.");
					return;
				}

				store.SetValue(storeIter, StoreColumns.Name, updated.Name);
				store.SetValue(storeIter, StoreColumns.LastExecuted, ProfileFormatting.FormatLastExecuted(updated.LastExecuted));
				store.SetValue(storeIter, StoreColumns.Command, updated.Command);
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

		private static bool IsDuplicateName(List<SandboxProfile> items, string name, int editedIndex)
		{
			for (int i = 0; i < items.Count; i++)
			{
				if (i == editedIndex)
				{
					continue;
				}

				if (string.Equals(items[i].Name, name, StringComparison.OrdinalIgnoreCase))
				{
					return true;
				}
			}

			return false;
		}

		private static bool IsUnchanged(SandboxProfile original, string name, string command, SandboxOptions options)
		{
			if (!string.Equals(original.Name, name, StringComparison.Ordinal))
			{
				return false;
			}

			if (!string.Equals(original.Command, command, StringComparison.Ordinal))
			{
				return false;
			}

			SandboxOptions effective = AppStorage.NormalizeOptions(original.Options);

			if (!effective.Equals(options))
			{
				return false;
			}

			return true;
		}

		private static void ShowError(Window parent, string message)
		{
			using (var error = new MessageDialog(parent, DialogFlags.Modal, MessageType.Error, ButtonsType.Ok, message))
			{
				error.Run();
			}
		}
	}
}
