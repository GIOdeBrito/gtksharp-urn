using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;
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
		private const string ProgramArgsPlaceholder = "%programArgs%";
		private const string OptionalArgsPlaceholder = "%optionalArgs%";
		private const string FilesystemHideMarker = "--tmpfs /home";
		private const string XdgRuntimeDirPlaceholder = "%xdgRuntimeDir%";
		private const string DisplayPlaceholder = "%display%";
		private const string WaylandDisplayPlaceholder = "%waylandDisplay%";
		private const string UnsafeLegacyMarker = "--ro-bind / /";
		private const string FallbackProgramDir = "/usr/bin";
		private const string ShellPath = "/bin/sh";
		private const string SandboxBinary = "bwrap";
		private const string AppImageExtension = ".AppImage";
		private const string AppImageCacheDirName = ".cache";
		private const string AppImageCacheSubDir = "appimage";
		private const string AppImageSquashDir = "squashfs-root";
		private const string AppImageAppRun = "AppRun";
		private const string AppImageExtractFlag = "--appimage-extract";

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

			if (!config.DefaultCommand.Contains(FilesystemHideMarker, StringComparison.Ordinal))
			{
				ShowError(parent, "Template lacks filesystem hiding. Reset it in Config.");
				return;
			}

			if (!config.DefaultCommand.Contains(ProgramArgsPlaceholder, StringComparison.Ordinal))
			{
				ShowError(parent, "Template lacks program args support. Reset it in Config.");
				return;
			}

			if (!config.DefaultCommand.Contains(OptionalArgsPlaceholder, StringComparison.Ordinal))
			{
				ShowError(parent, "Template lacks permissions support. Reset it in Config.");
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

			string[] argv = SplitCommand(target.Command);

			if (argv.Length == 0)
			{
				ShowError(parent, "Command must not be empty.");
				return;
			}

			string programPath = argv[0];
			string[] programArgs = argv.Length > 1 ? argv[1..] : Array.Empty<string>();

			if (string.IsNullOrWhiteSpace(programPath))
			{
				ShowError(parent, "Command must not be empty.");
				return;
			}

			string programDir = ResolveProgramDir(programPath);

			if (Path.IsPathRooted(programPath) && AppStorage.IsRealHomePath(programDir))
			{
				ShowError(parent, "Program inside the real home would expose it. Move it outside.");
				return;
			}

			string xdgRuntimeDir = Environment.GetEnvironmentVariable("XDG_RUNTIME_DIR") ?? string.Empty;
			string display = Environment.GetEnvironmentVariable("DISPLAY") ?? string.Empty;
			string waylandDisplay = Environment.GetEnvironmentVariable("WAYLAND_DISPLAY") ?? string.Empty;
			SandboxOptions options = AppStorage.NormalizeOptions(target.Options);

			if (IsAppImagePath(programPath) && options.AppImageExtractAndRun)
			{
				if (!TryPrepareExtractedAppRun(config.DefaultHome, programPath, out string extractedAppRun, out string extractedDir, out string extractError))
				{
					ShowError(parent, extractError);
					return;
				}

				programPath = extractedAppRun;
				programDir = ResolveProgramDir(programPath);

				if (IsSubPathOf(programDir, config.DefaultHome))
				{
					// The extracted tree is already visible via the
					// defaultHome bind. Re-binding the host path onto
					// itself would be a no-op at best and a read-only
					// shadow at worst, so bind an already-bound dir.
					programDir = FallbackProgramDir;
				}
			}

			string expandedCommand = ExpandCommand(config.DefaultCommand, config.DefaultHome, AppStorage.SandboxHome, programPath, programArgs, programDir, xdgRuntimeDir, display, waylandDisplay, options);

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

		internal static string ExpandCommand(string template, string defaultHome, string sandboxHome, string programPath, IReadOnlyList<string> programArgs, string programDir, string xdgRuntimeDir, string display, string waylandDisplay, SandboxOptions? options)
		{
			if (template == null)
			{
				return string.Empty;
			}

			string sandboxConfig = sandboxHome.TrimEnd('/') + "/.config";
			string sandboxCache = sandboxHome.TrimEnd('/') + "/.cache";
			string sandboxData = sandboxHome.TrimEnd('/') + "/.local/share";
			string optionalArgs = BuildOptionalArgs(options, xdgRuntimeDir, display, waylandDisplay);
			string quotedProgramArgs = BuildProgramArgs(programArgs);

			return template
				.Replace(OptionalArgsPlaceholder, optionalArgs, StringComparison.Ordinal)
				.Replace(DefaultHomePlaceholder, QuoteForShell(defaultHome), StringComparison.Ordinal)
				.Replace(SandboxConfigPlaceholder, QuoteForShell(sandboxConfig), StringComparison.Ordinal)
				.Replace(SandboxCachePlaceholder, QuoteForShell(sandboxCache), StringComparison.Ordinal)
				.Replace(SandboxDataPlaceholder, QuoteForShell(sandboxData), StringComparison.Ordinal)
				.Replace(SandboxHomePlaceholder, QuoteForShell(sandboxHome), StringComparison.Ordinal)
				.Replace(ProgramDirPlaceholder, QuoteForShell(programDir), StringComparison.Ordinal)
				.Replace(XdgRuntimeDirPlaceholder, QuoteForShell(xdgRuntimeDir), StringComparison.Ordinal)
				.Replace(DisplayPlaceholder, QuoteForShell(display), StringComparison.Ordinal)
				.Replace(WaylandDisplayPlaceholder, QuoteForShell(waylandDisplay), StringComparison.Ordinal)
				.Replace(ProgramPathPlaceholder, QuoteForShell(programPath), StringComparison.Ordinal)
				.Replace(ProgramArgsPlaceholder, quotedProgramArgs, StringComparison.Ordinal);
		}

		internal static string BuildProgramArgs(IReadOnlyList<string>? args)
		{
			if (args == null)
			{
				return string.Empty;
			}

			if (args.Count == 0)
			{
				return string.Empty;
			}

			var quoted = new List<string>(args.Count);

			foreach (string arg in args)
			{
				quoted.Add(QuoteForShell(arg));
			}

			return " " + string.Join(" ", quoted);
		}

		internal static string[] SplitCommand(string? command)
		{
			// Shell-word split so `prog --flag "quoted arg"` keeps one
			// argv entry per word. Each entry is quoted separately later,
			// so metacharacters stay inert under `sh -c`.
			if (string.IsNullOrWhiteSpace(command))
			{
				return Array.Empty<string>();
			}

			var argv = new List<string>();
			var current = new System.Text.StringBuilder();
			bool inToken = false;
			int i = 0;

			while (i < command.Length)
			{
				char c = command[i];

				if (!inToken)
				{
					if (char.IsWhiteSpace(c))
					{
						i++;
						continue;
					}

					inToken = true;
					continue;
				}

				if (char.IsWhiteSpace(c))
				{
					argv.Add(current.ToString());
					current.Clear();
					inToken = false;
					i++;
					continue;
				}

				if (c == '\'')
				{
					i = AppendSingleQuoted(command, i, current);
					continue;
				}

				if (c == '"')
				{
					i = AppendDoubleQuoted(command, i, current);
					continue;
				}

				if (c == '\\')
				{
					i = AppendEscaped(command, i, current);
					continue;
				}

				current.Append(c);
				i++;
			}

			if (inToken)
			{
				argv.Add(current.ToString());
			}

			return argv.ToArray();
		}

		private static int AppendSingleQuoted(string command, int quoteIndex, System.Text.StringBuilder current)
		{
			int i = quoteIndex + 1;

			while (i < command.Length)
			{
				if (command[i] == '\'')
				{
					return i + 1;
				}

				current.Append(command[i]);
				i++;
			}

			return i;
		}

		private static int AppendDoubleQuoted(string command, int quoteIndex, System.Text.StringBuilder current)
		{
			int i = quoteIndex + 1;

			while (i < command.Length)
			{
				if (command[i] == '"')
				{
					return i + 1;
				}

				if (command[i] == '\\' && i + 1 < command.Length)
				{
					char next = command[i + 1];

					if (next == '"' || next == '\\' || next == '$' || next == '`')
					{
						current.Append(next);
						i += 2;
						continue;
					}
				}

				current.Append(command[i]);
				i++;
			}

			return i;
		}

		private static int AppendEscaped(string command, int backslashIndex, System.Text.StringBuilder current)
		{
			if (backslashIndex + 1 >= command.Length)
			{
				return command.Length;
			}

			current.Append(command[backslashIndex + 1]);
			return backslashIndex + 2;
		}

		internal static string BuildOptionalArgs(SandboxOptions? options, string xdgRuntimeDir, string display, string waylandDisplay)
		{
			SandboxOptions effective = AppStorage.NormalizeOptions(options);
			var fragments = new List<string>();

			if (effective.ShareNetwork)
			{
				fragments.Add("--share-net");
			}

			if (effective.AllowGpu)
			{
				fragments.Add("--dev-bind-try /dev/dri /dev/dri");
			}

			if (effective.AllowX11)
			{
				fragments.Add("--bind-try /tmp/.X11-unix /tmp/.X11-unix --setenv DISPLAY " + QuoteForShell(display));
			}

			if (effective.AllowWayland)
			{
				AppendWaylandArgs(fragments, xdgRuntimeDir, waylandDisplay);
			}

			if (effective.AllowAudio)
			{
				AppendAudioArgs(fragments, xdgRuntimeDir);
			}

			if (effective.AllowAppImage && !effective.AppImageExtractAndRun)
			{
				AppendAppImageArgs(fragments);
			}

			if (fragments.Count == 0)
			{
				return string.Empty;
			}

			return " " + string.Join(" ", fragments);
		}

		private static void AppendWaylandArgs(List<string> fragments, string xdgRuntimeDir, string waylandDisplay)
		{
			// Bind only the single Wayland socket instead of the whole XDG dir,
			// so the Wayland toggle stays independent from the Audio toggle.
			// bind-try keeps missing sockets a safe no-op.
			if (IsBareSocketName(waylandDisplay) && !string.IsNullOrWhiteSpace(xdgRuntimeDir))
			{
				string source = xdgRuntimeDir.TrimEnd('/') + "/" + waylandDisplay.Trim();
				string target = "/run/" + waylandDisplay.Trim();
				fragments.Add("--bind-try " + QuoteForShell(source) + " " + QuoteForShell(target));
			}

			fragments.Add("--setenv WAYLAND_DISPLAY " + QuoteForShell(waylandDisplay));
		}

		private static void AppendAudioArgs(List<string> fragments, string xdgRuntimeDir)
		{
			// PulseAudio and PipeWire sockets both live under XDG_RUNTIME_DIR.
			// bind-try keeps absent daemons a safe no-op.
			if (!string.IsNullOrWhiteSpace(xdgRuntimeDir))
			{
				string baseDir = xdgRuntimeDir.TrimEnd('/');
				fragments.Add("--bind-try " + QuoteForShell(baseDir + "/pulse") + " " + QuoteForShell("/run/pulse"));
				fragments.Add("--bind-try " + QuoteForShell(baseDir + "/pipewire-0") + " " + QuoteForShell("/run/pipewire-0"));
			}

			fragments.Add("--setenv PULSE_SERVER " + QuoteForShell("unix:/run/pulse/native"));
		}

		private static void AppendAppImageArgs(List<string> fragments)
		{
			// Type2 AppImages mount squashfs via fusermount on PATH plus /dev/fuse.
			// Both stay disabled unless this toggle is on; the device alone
			// is inert without a helper, and bind-try keeps distro-specific
			// helper paths a safe no-op.
			fragments.Add("--dev-bind-try /dev/fuse /dev/fuse");
			fragments.Add("--ro-bind-try /usr/bin/fusermount /usr/bin/fusermount");
			fragments.Add("--ro-bind-try /usr/bin/fusermount3 /usr/bin/fusermount3");
			fragments.Add("--ro-bind-try /bin/fusermount /bin/fusermount");
			fragments.Add("--ro-bind-try /bin/fusermount3 /bin/fusermount3");
			fragments.Add("--ro-bind-try /etc/fuse.conf /etc/fuse.conf");
		}

		private static bool IsBareSocketName(string? value)
		{
			if (string.IsNullOrWhiteSpace(value))
			{
				return false;
			}

			string trimmed = value.Trim();

			if (trimmed.Contains('/', StringComparison.Ordinal))
			{
				return false;
			}

			if (trimmed.Contains("..", StringComparison.Ordinal))
			{
				return false;
			}

			return true;
		}

		private static string ResolveProgramDir(string programPath)
		{
			if (string.IsNullOrWhiteSpace(programPath))
			{
				return FallbackProgramDir;
			}

			string trimmed = programPath.Trim();

			if (string.IsNullOrWhiteSpace(trimmed))
			{
				return FallbackProgramDir;
			}

			string? directory = Path.GetDirectoryName(trimmed);

			if (string.IsNullOrWhiteSpace(directory))
			{
				return FallbackProgramDir;
			}

			return directory;
		}

		private static bool IsAppImagePath(string? programPath)
		{
			if (string.IsNullOrWhiteSpace(programPath))
			{
				return false;
			}

			return programPath.Trim().EndsWith(AppImageExtension, StringComparison.OrdinalIgnoreCase);
		}

		private static bool IsSubPathOf(string? candidate, string? baseDir)
		{
			if (string.IsNullOrWhiteSpace(candidate))
			{
				return false;
			}

			if (string.IsNullOrWhiteSpace(baseDir))
			{
				return false;
			}

			string fullCandidate;
			string fullBase;

			try
			{
				fullCandidate = Path.GetFullPath(candidate.Trim()).TrimEnd(Path.DirectorySeparatorChar);
				fullBase = Path.GetFullPath(baseDir.Trim()).TrimEnd(Path.DirectorySeparatorChar);
			}
			catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException || exception is ArgumentException || exception is NotSupportedException)
			{
				return false;
			}

			if (string.Equals(fullCandidate, fullBase, StringComparison.Ordinal))
			{
				return true;
			}

			return fullCandidate.StartsWith(fullBase + Path.DirectorySeparatorChar, StringComparison.Ordinal);
		}

		private static bool TryPrepareExtractedAppRun(string defaultHome, string appImagePath, out string appRunHostPath, out string appRunDirHostPath, out string error)
		{
			appRunHostPath = string.Empty;
			appRunDirHostPath = string.Empty;
			error = string.Empty;

			if (string.IsNullOrWhiteSpace(defaultHome))
			{
				error = "Set Default home in Config first.";
				return false;
			}

			if (!File.Exists(appImagePath))
			{
				error = "AppImage file does not exist: " + appImagePath;
				return false;
			}

			FileInfo info;

			try
			{
				info = new FileInfo(appImagePath);
			}
			catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException || exception is ArgumentException || exception is NotSupportedException)
			{
				error = "Could not inspect AppImage: " + exception.Message;
				return false;
			}

			string cacheDir = Path.Combine(defaultHome, AppImageCacheDirName, AppImageCacheSubDir);
			string extractDir = Path.Combine(cacheDir, BuildExtractKey(appImagePath, info));
			string squashDir = Path.Combine(extractDir, AppImageSquashDir);
			string appRun = Path.Combine(squashDir, AppImageAppRun);

			if (File.Exists(appRun))
			{
				appRunHostPath = appRun;
				appRunDirHostPath = squashDir;
				return true;
			}

			if (!TryExtractAppImage(defaultHome, appImagePath, extractDir, out error))
			{
				return false;
			}

			if (!File.Exists(appRun))
			{
				error = "Extraction did not produce AppRun. The file may not be a Type2 AppImage. Try FUSE mode instead.";
				return false;
			}

			appRunHostPath = appRun;
			appRunDirHostPath = squashDir;
			return true;
		}

		private static string BuildExtractKey(string appImagePath, FileInfo info)
		{
			string fullPath;

			try
			{
				fullPath = Path.GetFullPath(appImagePath.Trim());
			}
			catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException || exception is ArgumentException || exception is NotSupportedException)
			{
				fullPath = appImagePath.Trim();
			}

			long length = 0;
			long ticks = 0;

			try
			{
				length = info.Length;
				ticks = info.LastWriteTimeUtc.Ticks;
			}
			catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
			{
				length = 0;
				ticks = 0;
			}

			string fingerprint = fullPath + "|" + length.ToString() + "|" + ticks.ToString();
			byte[] bytes = Encoding.UTF8.GetBytes(fingerprint);

			using (SHA256 sha = SHA256.Create())
			{
				byte[] hash = sha.ComputeHash(bytes);
				var builder = new StringBuilder(hash.Length * 2);

				foreach (byte b in hash)
				{
					builder.Append(b.ToString("x2"));
				}

				return builder.ToString();
			}
		}

		private static bool TryExtractAppImage(string defaultHome, string appImagePath, string extractDir, out string error)
		{
			error = string.Empty;
			string stagingDir = extractDir + ".staging";

			try
			{
				if (Directory.Exists(stagingDir))
				{
					Directory.Delete(stagingDir, true);
				}

				Directory.CreateDirectory(stagingDir);
			}
			catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException || exception is ArgumentException || exception is NotSupportedException)
			{
				error = "Could not prepare AppImage cache: " + exception.Message;
				return false;
			}

			int exitCode = RunAppImageExtract(defaultHome, appImagePath, stagingDir, out string detail);

			if (exitCode != 0)
			{
				TryDeleteDirectory(stagingDir);
				error = "AppImage extraction failed (code " + exitCode + "). " + detail;
				return false;
			}

			try
			{
				if (Directory.Exists(extractDir))
				{
					Directory.Delete(extractDir, true);
				}

				Directory.Move(stagingDir, extractDir);
				return true;
			}
			catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException || exception is ArgumentException || exception is NotSupportedException)
			{
				TryDeleteDirectory(stagingDir);
				error = "Could not finalize AppImage cache: " + exception.Message;
				return false;
			}
		}

		private static int RunAppImageExtract(string defaultHome, string appImagePath, string workingDir, out string detail)
		{
			detail = string.Empty;

			var startInfo = new ProcessStartInfo(appImagePath);
			startInfo.UseShellExecute = false;
			startInfo.WorkingDirectory = workingDir;
			startInfo.RedirectStandardOutput = true;
			startInfo.RedirectStandardError = true;
			startInfo.ArgumentList.Add(AppImageExtractFlag);

			try
			{
				startInfo.Environment["HOME"] = defaultHome;
			}
			catch (Exception exception) when (exception is ArgumentException || exception is NotSupportedException || exception is InvalidOperationException)
			{
				detail = exception.Message;
				return -1;
			}

			try
			{
				Process? process = Process.Start(startInfo);

				if (process == null)
				{
					detail = "Could not start AppImage extractor.";
					return -1;
				}

				using (process)
				{
					string output = process.StandardOutput.ReadToEnd();
					string errors = process.StandardError.ReadToEnd();
					process.WaitForExit();
					detail = (errors + " " + output).Trim();

					if (detail.Length > 500)
					{
						detail = detail.Substring(0, 500);
					}

					return process.ExitCode;
				}
			}
			catch (Exception exception) when (exception is Win32Exception || exception is IOException || exception is UnauthorizedAccessException || exception is InvalidOperationException || exception is NotSupportedException)
			{
				detail = exception.Message;
				return -1;
			}
		}

		private static void TryDeleteDirectory(string path)
		{
			try
			{
				if (Directory.Exists(path))
				{
					Directory.Delete(path, true);
				}
			}
			catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException || exception is ArgumentException || exception is NotSupportedException)
			{
				// Best-effort cleanup of a stale staging dir. A leftover
				// is harmless because the next run recreates it.
				Console.Error.WriteLine("Could not clean AppImage staging dir: " + exception.Message);
			}
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
			var stamped = new SandboxProfile(current.Name, current.Command, DateTime.Now, current.Options);
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
