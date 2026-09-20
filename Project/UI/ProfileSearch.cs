using System;
using Gtk;

namespace UrnWrapper.UI
{
	internal static class ProfileSearch
	{
		internal static bool RowMatchesSearch(ITreeModel model, TreeIter iter, string? searchText)
		{
			if (string.IsNullOrWhiteSpace(searchText))
			{
				return true;
			}

			string normalizedSearch = searchText.Trim();
			string? name = model.GetValue(iter, StoreColumns.Name) as string;
			string? program = model.GetValue(iter, StoreColumns.Program) as string;

			return ContainsIgnoreCase(name, normalizedSearch)
				|| ContainsIgnoreCase(program, normalizedSearch);
		}

		private static bool ContainsIgnoreCase(string? source, string searchText)
		{
			if (string.IsNullOrEmpty(source))
			{
				return false;
			}

			return source.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >= 0;
		}
	}
}
