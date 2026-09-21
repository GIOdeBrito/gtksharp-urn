using System;
using System.Collections.Generic;
using System.IO;
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
				DialogSizing.Apply(dialog);

				var nameEntry = new Entry();
				nameEntry.Text = original.Name;

				var programEntry = new Entry();
				programEntry.Text = original.Program;

				var argumentsEntry = new Entry();
				argumentsEntry.Text = original.Arguments;

				var browseButton = new Button("Browse...");
				browseButton.Clicked += (sender, args) =>
				{
					BrowseForProgram(dialog, programEntry, argumentsEntry);
				};

				SandboxPermissionControls permissions = SandboxPermissionControls.FromOptions(AppStorage.NormalizeOptions(original.Options));

				Box contentArea = dialog.ContentArea;
				contentArea.Spacing = 6;
				contentArea.Margin = 6;

				contentArea.PackStart(BuildLabeledRow("Name:", nameEntry), false, false, 0);
				contentArea.PackStart(BuildProgramRow(programEntry, browseButton), false, false, 0);
				contentArea.PackStart(BuildLabeledRow("Arguments:", argumentsEntry), false, false, 0);
				contentArea.PackStart(permissions.BuildFrame(), false, false, 0);

				dialog.ShowAll();

				ResponseType response = (ResponseType)dialog.Run();
				string name = nameEntry.Text.Trim();
				string program = programEntry.Text.Trim();
				string arguments = argumentsEntry.Text.Trim();

				if (response != ResponseType.Accept)
				{
					return;
				}

				if (string.IsNullOrWhiteSpace(name))
				{
					ShowError(parent, "Name must not be empty.");
					return;
				}

				if (string.IsNullOrWhiteSpace(program))
				{
					ShowError(parent, "Program must not be empty.");
					return;
				}

				if (IsDuplicateName(items, name, itemIndex))
				{
					ShowError(parent, "An item with that name already exists.");
					return;
				}

				if (IsUnchanged(original, name, program, arguments, permissions.ToOptions()))
				{
					return;
				}

				var updated = new SandboxProfile(name, program, arguments, original.LastExecuted, permissions.ToOptions());
				items[itemIndex] = updated;

				if (!AppStorage.TrySaveItems(AppStorage.GetItemsFilePath(), items))
				{
					items[itemIndex] = original;
					ShowError(parent, "Could not save items.json.");
					return;
				}

				store.SetValue(storeIter, StoreColumns.Name, updated.Name);
				store.SetValue(storeIter, StoreColumns.LastExecuted, ProfileFormatting.FormatLastExecuted(updated.LastExecuted));
				store.SetValue(storeIter, StoreColumns.Program, ProfileCommand.Combine(updated.Program, updated.Arguments));
				filter.Refilter();
			}
		}

		private static Box BuildProgramRow(Entry programEntry, Button browseButton)
		{
			var label = new Label("Program:");
			label.Xalign = 1;
			label.WidthRequest = 80;

			var row = new Box(Orientation.Horizontal, 6);
			row.PackStart(label, false, false, 0);
			row.PackStart(programEntry, true, true, 0);
			row.PackStart(browseButton, false, false, 0);

			return row;
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

		private static void BrowseForProgram(Dialog parent, Entry programEntry, Entry argumentsEntry)
		{
			using (var chooser = new FileChooserDialog("Select Program", parent, FileChooserAction.Open, "Cancel", ResponseType.Cancel, "Open", ResponseType.Accept))
			{
				DialogSizing.ApplyChooser(chooser);
				SetInitialProgramFolder(chooser, programEntry.Text.Trim());

				ResponseType response = (ResponseType)chooser.Run();
				string? picked = chooser.Filename;

				if (response != ResponseType.Accept)
				{
					return;
				}

				if (string.IsNullOrWhiteSpace(picked))
				{
					return;
				}

				programEntry.Text = picked.Trim();
				argumentsEntry.Text = string.Empty;
			}
		}

		private static void SetInitialProgramFolder(FileChooserDialog chooser, string currentProgram)
		{
			if (!string.IsNullOrWhiteSpace(currentProgram))
			{
				if (File.Exists(currentProgram))
				{
					chooser.SetFilename(currentProgram);
					return;
				}

				string? directory = Path.GetDirectoryName(currentProgram);

				if (!string.IsNullOrWhiteSpace(directory) && Directory.Exists(directory))
				{
					chooser.SetCurrentFolder(directory);
					return;
				}
			}

			if (Directory.Exists("/usr/bin"))
			{
				chooser.SetCurrentFolder("/usr/bin");
				return;
			}
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

		private static bool IsUnchanged(SandboxProfile original, string name, string program, string arguments, SandboxOptions options)
		{
			if (!string.Equals(original.Name, name, StringComparison.Ordinal))
			{
				return false;
			}

			if (!string.Equals(original.Program, program, StringComparison.Ordinal))
			{
				return false;
			}

			if (!string.Equals(original.Arguments, arguments, StringComparison.Ordinal))
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
				DialogSizing.ApplyMessage(error);
				error.Run();
			}
		}
	}
}
