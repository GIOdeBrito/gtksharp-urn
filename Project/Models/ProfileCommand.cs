using System;
using System.Collections.Generic;
using System.Text;

namespace UrnWrapper.Models
{
	internal static class ProfileCommand
	{
		internal static string[] Split(string? command)
		{
			// Shell-word split so `prog --flag "quoted arg"` keeps one
			// entry per word. Each entry is quoted separately later,
			// so metacharacters stay inert under `sh -c`.
			if (string.IsNullOrWhiteSpace(command))
			{
				return Array.Empty<string>();
			}

			var argv = new List<string>();
			var current = new StringBuilder();
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

		internal static string JoinQuoted(IReadOnlyList<string>? args)
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
				quoted.Add(QuoteIfNeeded(arg));
			}

			return string.Join(" ", quoted);
		}

		internal static string Combine(string? program, string? arguments)
		{
			string programText = program == null ? string.Empty : program.Trim();
			string argsText = arguments == null ? string.Empty : arguments.Trim();

			if (string.IsNullOrEmpty(argsText))
			{
				return programText;
			}

			if (string.IsNullOrEmpty(programText))
			{
				return argsText;
			}

			return programText + " " + argsText;
		}

		private static string QuoteIfNeeded(string? value)
		{
			if (string.IsNullOrEmpty(value))
			{
				return "''";
			}

			bool needsQuotes = false;

			foreach (char c in value)
			{
				if (char.IsWhiteSpace(c) || c == '\'' || c == '"' || c == '\\')
				{
					needsQuotes = true;
					break;
				}
			}

			if (!needsQuotes)
			{
				return value;
			}

			return "'" + value.Replace("'", "'\"'\"'") + "'";
		}

		private static int AppendSingleQuoted(string command, int quoteIndex, StringBuilder current)
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

		private static int AppendDoubleQuoted(string command, int quoteIndex, StringBuilder current)
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

		private static int AppendEscaped(string command, int backslashIndex, StringBuilder current)
		{
			if (backslashIndex + 1 >= command.Length)
			{
				return command.Length;
			}

			current.Append(command[backslashIndex + 1]);
			return backslashIndex + 2;
		}
	}
}
