using System.Collections.Generic;
using Gtk;
using UrnWrapper.Models;
using UrnWrapper.Persistence;

namespace UrnWrapper.UI
{
	internal static class RemoveItemDialog
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

			SandboxProfile target = items[itemIndex];

			using (var confirm = new MessageDialog(parent, DialogFlags.Modal, MessageType.Question, ButtonsType.YesNo, "Delete \"" + target.Name + "\"?"))
			{
				DialogSizing.ApplyMessage(confirm);
				ResponseType response = (ResponseType)confirm.Run();

				if (response != ResponseType.Yes)
				{
					return;
				}
			}

			items.RemoveAt(itemIndex);

			if (!AppStorage.TrySaveItems(AppStorage.GetItemsFilePath(), items))
			{
				items.Insert(itemIndex, target);
				ShowError(parent, "Could not save items.json.");
				return;
			}

			TreeIter iter = storeIter;
			store.Remove(ref iter);
			filter.Refilter();
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
