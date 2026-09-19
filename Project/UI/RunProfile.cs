using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using Gtk;
using UrnWrapper.Models;
using UrnWrapper.Persistence;

namespace UrnWrapper.UI
{
	internal static class RunProfile
	{
		private const string DefaultHomePlaceholder = "%defaultHomeDir%";
		private const string ProgramPathPlaceholder = "%programPath%";
		private const string ShellPath = "/bin/sh";
		private const string SandboxBinary = "bwrap";

		internal static void Run(Window parent, ListStore store, TreeModelFilter filter, List<SandboxProfile> items, int itemIndex)
		{
			if (itemIndex < 0)
			{
				return;
			}

			if (itemIndex >= items.Count)
			{
				return;
			}

			Config config = AppStorage.LoadConfig();

			if (string.IsNullOrWhiteSpace(config.DefaultHome))
			{
				ShowError(parent, "Set Default home in Config first.");
				return;
			}

			if (!Directory.Exists(config.DefaultHome))
			{
				ShowError(parent, "Default home folder does not exist. Update it in Config.");
				return;
			}

			SandboxProfile target = items[itemIndex];

			if (string.IsNullOrWhiteSpace(target.Command))
			{
				ShowError(parent, "Command must not be empty.");
				return;
			}

			if (!IsOnPath(SandboxBinary))
			{
				ShowError(parent, "bwrap was not found on PATH.");
				return;
			}

			string expandedCommand = ExpandCommand(config.DefaultCommand, config.DefaultHome, target.Command);

			try
			{
				StartSandbox(parent, expandedCommand, target.Name, store, filter, items);
			}
			catch (Exception exception) when (exception is Win32Exception || exception is IOException || exception is UnauthorizedAccessException || exception is InvalidOperationException)
			{
				ShowError(parent, "Could not start sandbox: " + exception.Message);
			}
		}

		internal static string ExpandCommand(string template, string defaultHome, string programPath)
		{
			if (template == null)
			{
				return string.Empty;
			}

			return template
				.Replace(DefaultHomePlaceholder, QuoteForShell(defaultHome), StringComparison.Ordinal)
				.Replace(ProgramPathPlaceholder, QuoteForShell(programPath), StringComparison.Ordinal);
		}

		private static void StartSandbox(Window parent, string expandedCommand, string profileName, ListStore store, TreeModelFilter filter, List<SandboxProfile> items)
		{
			var startInfo = new ProcessStartInfo(ShellPath);
			startInfo.UseShellExecute = false;
			startInfo.ArgumentList.Add("-c");
			startInfo.ArgumentList.Add(expandedCommand);

			var process = new Process();
			process.StartInfo = startInfo;
			process.EnableRaisingEvents = true;
			process.Exited += (sender, args) =>
			{
				process.Dispose();

				GLib.Idle.Add(() =>
				{
					StampRun(parent, profileName, store, filter, items);
					return false;
				});
			};
			process.Start();
		}

		private static void StampRun(Window parent, string profileName, ListStore store, TreeModelFilter filter, List<SandboxProfile> items)
		{
			int itemIndex = FindItemIndex(items, profileName);

			if (itemIndex < 0)
			{
				return;
			}

			SandboxProfile current = items[itemIndex];
			var stamped = new SandboxProfile(current.Name, current.Command, DateTime.Now);
			items[itemIndex] = stamped;

			if (!AppStorage.TrySaveItems(AppStorage.GetItemsFilePath(), items))
			{
				items[itemIndex] = current;
				ShowError(parent, "Could not save items.json.");
				return;
			}

			if (!TryFindStoreIter(store, profileName, out TreeIter storeIter))
			{
				return;
			}

			store.SetValue(storeIter, StoreColumns.LastExecuted, ProfileFormatting.FormatLastExecuted(stamped.LastExecuted));
			filter.Refilter();
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

		private static bool TryFindStoreIter(ListStore store, string profileName, out TreeIter storeIter)
		{
			storeIter = TreeIter.Zero;

			if (!store.GetIterFirst(out TreeIter iter))
			{
				return false;
			}

			do
			{
				string? name = store.GetValue(iter, StoreColumns.Name) as string;

				if (string.Equals(name, profileName, StringComparison.OrdinalIgnoreCase))
				{
					storeIter = iter;
					return true;
				}
			}
			while (store.IterNext(ref iter));

			return false;
		}

		private static bool IsOnPath(string fileName)
		{
			string? pathVariable = Environment.GetEnvironmentVariable("PATH");

			if (string.IsNullOrEmpty(pathVariable))
			{
				return false;
			}

			foreach (string directory in pathVariable.Split(Path.PathSeparator))
			{
				if (string.IsNullOrWhiteSpace(directory))
				{
					continue;
				}

				if (File.Exists(Path.Combine(directory, fileName)))
				{
					return true;
				}
			}

			return false;
		}

		private static string QuoteForShell(string? value)
		{
			if (string.IsNullOrEmpty(value))
			{
				return "''";
			}

			return "'" + value.Replace("'", "'\"'\"'") + "'";
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
