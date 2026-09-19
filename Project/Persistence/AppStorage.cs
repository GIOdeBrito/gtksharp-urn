using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using UrnWrapper.Models;

namespace UrnWrapper.Persistence
{
	internal static class AppStorage
	{
		private const string ItemsFileName = "items.json";
		private const string ConfigFileName = "config.json";
		private const string UnsafeLegacyMarker = "--ro-bind / /";
		private const string LegacySysfsMarker = "--sysfs";

		internal const string SandboxHome = "/home/sandbox";

		internal const string DefaultCommandTemplate = "bwrap --unshare-all --share-net --die-with-parent --new-session"
			+ " --ro-bind /usr /usr --ro-bind /etc /etc --ro-bind /opt /opt"
			+ " --symlink usr/lib /lib --symlink usr/lib64 /lib64 --symlink usr/bin /bin --symlink usr/sbin /sbin"
			+ " --proc /proc --dev /dev --ro-bind-try /sys /sys --tmpfs /tmp --tmpfs /run"
			+ " --dev-bind-try /dev/dri /dev/dri --dev-bind-try /dev/fuse /dev/fuse"
			+ " --ro-bind-try /etc/fonts /etc/fonts --ro-bind-try /usr/share/fonts /usr/share/fonts --ro-bind-try /etc/ssl /etc/ssl"
			+ " --bind-try /tmp/.X11-unix /tmp/.X11-unix --bind-try %xdgRuntimeDir% %xdgRuntimeDir%"
			+ " --dir %sandboxHome% --bind %defaultHomeDir% %sandboxHome%"
			+ " --setenv HOME %sandboxHome% --setenv XDG_CONFIG_HOME %sandboxConfig% --setenv XDG_CACHE_HOME %sandboxCache% --setenv XDG_DATA_HOME %sandboxData% --setenv XDG_RUNTIME_DIR /run"
			+ " --setenv DISPLAY %display% --setenv WAYLAND_DISPLAY %waylandDisplay%"
			+ " --chdir %sandboxHome%"
			+ " --ro-bind-try %programDir% %programDir% -- %programPath%";

		internal static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
		{
			PropertyNameCaseInsensitive = true,
			WriteIndented = true,
		};

		internal static string GetItemsFilePath()
		{
			return Path.Combine(AppContext.BaseDirectory, ItemsFileName);
		}

		internal static string GetConfigFilePath()
		{
			return Path.Combine(AppContext.BaseDirectory, ConfigFileName);
		}

		internal static List<SandboxProfile> LoadItems()
		{
			string filePath = GetItemsFilePath();

			if (!File.Exists(filePath))
			{
				TrySaveItems(filePath, new List<SandboxProfile>());
				return new List<SandboxProfile>();
			}

			try
			{
				string json = File.ReadAllText(filePath);
				List<SandboxProfile>? items = JsonSerializer.Deserialize<List<SandboxProfile>>(json, JsonOptions);
				return items ?? new List<SandboxProfile>();
			}
			catch (Exception exception) when (exception is JsonException || exception is IOException || exception is UnauthorizedAccessException)
			{
				BackupCorruptFile(filePath);
				return new List<SandboxProfile>();
			}
		}

		internal static Config LoadConfig()
		{
			string filePath = GetConfigFilePath();

			if (!File.Exists(filePath))
			{
				var defaults = new Config("", DefaultCommandTemplate);
				TrySaveConfig(filePath, defaults);
				return defaults;
			}

			try
			{
				string json = File.ReadAllText(filePath);
				Config? config = JsonSerializer.Deserialize<Config>(json, JsonOptions);

				if (config == null)
				{
					return MigrateToSecureDefaults(filePath, "");
				}

				if (config.DefaultHome == null || config.DefaultCommand == null)
				{
					return MigrateToSecureDefaults(filePath, config.DefaultHome ?? "");
				}

				if (IsUnsafeTemplate(config.DefaultCommand))
				{
					return MigrateToSecureDefaults(filePath, config.DefaultHome);
				}

				return config;
			}
			catch (Exception exception) when (exception is JsonException || exception is IOException || exception is UnauthorizedAccessException)
			{
				BackupCorruptFile(filePath);
				return new Config("", DefaultCommandTemplate);
			}
		}

		internal static bool IsRealHomePath(string? candidate)
		{
			if (string.IsNullOrWhiteSpace(candidate))
			{
				return false;
			}

			string? realHome = Environment.GetEnvironmentVariable("HOME");

			if (string.IsNullOrWhiteSpace(realHome))
			{
				return false;
			}

			string normalizedCandidate = Path.GetFullPath(candidate.Trim()).TrimEnd(Path.DirectorySeparatorChar);
			string normalizedReal = Path.GetFullPath(realHome.Trim()).TrimEnd(Path.DirectorySeparatorChar);

			if (string.IsNullOrEmpty(normalizedCandidate))
			{
				return true;
			}

			if (string.Equals(normalizedCandidate, normalizedReal, StringComparison.OrdinalIgnoreCase))
			{
				return true;
			}

			if (normalizedCandidate.StartsWith(normalizedReal + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
			{
				return true;
			}

			// A parent of the real home (e.g. /home) would expose it when bound.
			if (normalizedReal.StartsWith(normalizedCandidate + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
			{
				return true;
			}

			return false;
		}

		internal static bool TemplateExposesRealHome(string? template)
		{
			if (string.IsNullOrWhiteSpace(template))
			{
				return false;
			}

			string? realHome = Environment.GetEnvironmentVariable("HOME");

			if (string.IsNullOrWhiteSpace(realHome))
			{
				return false;
			}

			return template.Contains(realHome.Trim(), StringComparison.Ordinal);
		}

		private static Config MigrateToSecureDefaults(string filePath, string defaultHome)
		{
			// Old installs persist the legacy --ro-bind / / template or the
			// invalid --sysfs flag, which no bwrap release supports.
			// Replace it so old configs keep working and the real home stays hidden.
			var migrated = new Config(defaultHome, DefaultCommandTemplate);
			TrySaveConfig(filePath, migrated);
			return migrated;
		}

		private static bool IsUnsafeTemplate(string template)
		{
			if (template.Contains(UnsafeLegacyMarker, StringComparison.Ordinal))
			{
				return true;
			}

			if (template.Contains(LegacySysfsMarker, StringComparison.Ordinal))
			{
				return true;
			}

			if (TemplateExposesRealHome(template))
			{
				return true;
			}

			return false;
		}

		internal static bool TrySaveItems(string filePath, IReadOnlyList<SandboxProfile> items)
		{
			string json = JsonSerializer.Serialize(items, JsonOptions);
			return TryWriteAtomically(filePath, json);
		}

		internal static bool TrySaveConfig(string filePath, Config config)
		{
			string json = JsonSerializer.Serialize(config, JsonOptions);
			return TryWriteAtomically(filePath, json);
		}

		private static bool TryWriteAtomically(string filePath, string content)
		{
			string tempPath = filePath + ".tmp";

			try
			{
				File.WriteAllText(tempPath, content);
				File.Move(tempPath, filePath, true);
				return true;
			}
			catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
			{
				Console.Error.WriteLine("Failed to save " + filePath + ": " + exception.Message);
				return false;
			}
		}

		private static void BackupCorruptFile(string filePath)
		{
			try
			{
				string backupPath = filePath + ".bad." + DateTime.UtcNow.ToString("yyyyMMddHHmmss") + ".json";
				File.Move(filePath, backupPath);
				Console.Error.WriteLine("Moved corrupt " + filePath + " to " + backupPath);
			}
			catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
			{
				Console.Error.WriteLine("Failed to back up corrupt " + filePath + ": " + exception.Message);
			}
		}
	}
}
