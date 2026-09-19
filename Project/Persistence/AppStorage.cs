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
		private const string DefaultCommandTemplate = "bwrap --ro-bind / / --dev /dev --proc /proc --bind %defaultHomeDir% $HOME %programPath%";

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
				return config ?? new Config("", DefaultCommandTemplate);
			}
			catch (Exception exception) when (exception is JsonException || exception is IOException || exception is UnauthorizedAccessException)
			{
				BackupCorruptFile(filePath);
				return new Config("", DefaultCommandTemplate);
			}
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
