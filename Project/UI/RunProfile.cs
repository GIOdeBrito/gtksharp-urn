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
		private const string SandboxHomePlaceholder = "%sandboxHome%";
		private const string SandboxConfigPlaceholder = "%sandboxConfig%";
		private const string SandboxCachePlaceholder = "%sandboxCache%";
		private const string SandboxDataPlaceholder = "%sandboxData%";
		private const string ProgramPathPlaceholder = "%programPath%";
		private const string ProgramDirPlaceholder = "%programDir%";
		private const string XdgRuntimeDirPlaceholder = "%xdgRuntimeDir%";
		private const string DisplayPlaceholder = "%display%";
		private const string WaylandDisplayPlaceholder = "%waylandDisplay%";
		private const string UnsafeLegacyMarker = "--ro-bind / /";
		private const string FallbackProgramDir = "/usr/bin";
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

			if (AppStorage.IsRealHomePath(config.DefaultHome))
			{
				ShowError(parent, "Default home must never be the real home. Pick an isolated folder.");
				return;
			}

			if (string.IsNullOrWhiteSpace(config.DefaultCommand))
			{
				ShowError(parent, "Default command template is empty. Reset it in Config.");
				return;
			}

			if (config.DefaultCommand.Contains(UnsafeLegacyMarker, StringComparison.Ordinal))
			{
				ShowError(parent, "Insecure template exposing the whole filesystem. Reset it in Config.");
				return;
			}

			if (AppStorage.TemplateExposesRealHome(config.DefaultCommand))
			{
				ShowError(parent, "Template would expose the real home. Reset it in Config.");
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

			string programCommand = target.Command.Trim();
			string programDir = ResolveProgramDir(programCommand);

			if (AppStorage.IsRealHomePath(programDir))
			{
				ShowError(parent, "Program inside the real home would expose it. Move it outside.");
				return;
			}

			string xdgRuntimeDir = Environment.GetEnvironmentVariable("XDG_RUNTIME_DIR") ?? string.Empty;
			string display = Environment.GetEnvironmentVariable("DISPLAY") ?? string.Empty;
			string waylandDisplay = Environment.GetEnvironmentVariable("WAYLAND_DISPLAY") ?? string.Empty;

			string expandedCommand = ExpandCommand(config.DefaultCommand, config.DefaultHome, AppStorage.SandboxHome, programCommand, programDir, xdgRuntimeDir, display, waylandDisplay);

			if (string.IsNullOrWhiteSpace(expandedCommand))
			{
				ShowError(parent, "Expanded sandbox command is empty.");
				return;
			}

			if (AppStorage.TemplateExposesRealHome(expandedCommand))
			{
				ShowError(parent, "Refusing to run: expanded command would expose the real home.");
				return;
			}

			try
			{
				StartSandbox(parent, expandedCommand, target.Name, store, filter, items);
			}
			catch (Exception exception) when (exception is Win32Exception || exception is IOException || exception is UnauthorizedAccessException || exception is InvalidOperationException)
			{
				ShowError(parent, "Could not start sandbox: " + exception.Message);
			}
		}

		internal static string ExpandCommand(string template, string defaultHome, string sandboxHome, string programPath, string programDir, string xdgRuntimeDir, string display, string waylandDisplay)
		{
			if (template == null)
			{
				return string.Empty;
			}

			string sandboxConfig = sandboxHome.TrimEnd('/') + "/.config";
			string sandboxCache = sandboxHome.TrimEnd('/') + "/.cache";
			string sandboxData = sandboxHome.TrimEnd('/') + "/.local/share";

			return template
				.Replace(DefaultHomePlaceholder, QuoteForShell(defaultHome), StringComparison.Ordinal)
				.Replace(SandboxConfigPlaceholder, QuoteForShell(sandboxConfig), StringComparison.Ordinal)
				.Replace(SandboxCachePlaceholder, QuoteForShell(sandboxCache), StringComparison.Ordinal)
				.Replace(SandboxDataPlaceholder, QuoteForShell(sandboxData), StringComparison.Ordinal)
				.Replace(SandboxHomePlaceholder, QuoteForShell(sandboxHome), StringComparison.Ordinal)
				.Replace(ProgramDirPlaceholder, QuoteForShell(programDir), StringComparison.Ordinal)
				.Replace(XdgRuntimeDirPlaceholder, QuoteForShell(xdgRuntimeDir), StringComparison.Ordinal)
				.Replace(DisplayPlaceholder, QuoteForShell(display), StringComparison.Ordinal)
				.Replace(WaylandDisplayPlaceholder, QuoteForShell(waylandDisplay), StringComparison.Ordinal)
				.Replace(ProgramPathPlaceholder, QuoteForShell(programPath), StringComparison.Ordinal);
		}

		private static string ResolveProgramDir(string programCommand)
		{
			if (string.IsNullOrWhiteSpace(programCommand))
			{
				return FallbackProgramDir;
			}

			string? directory = Path.GetDirectoryName(programCommand);

			if (string.IsNullOrWhiteSpace(directory))
			{
				return FallbackProgramDir;
			}

			return directory;
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
				int exitCode = process.ExitCode;
				process.Dispose();

				GLib.Idle.Add(() =>
				{
					if (exitCode != 0)
					{
						ShowError(parent, "Sandbox exited with code " + exitCode + ".");
						return false;
					}

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
