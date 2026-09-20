using Gtk;

namespace UrnWrapper.UI
{
	internal static class DialogSizing
	{
		private const int DialogWidth = 580;
		private const int FileChooserHeight = 450;

		internal static void Apply(Dialog dialog)
		{
			if (dialog == null)
			{
				return;
			}

			dialog.SetDefaultSize(DialogWidth, -1);
			dialog.SetPosition(WindowPosition.CenterOnParent);
			dialog.Resizable = true;
		}

		internal static void ApplyMessage(MessageDialog dialog)
		{
			if (dialog == null)
			{
				return;
			}

			dialog.SetDefaultSize(DialogWidth, -1);
			dialog.SetPosition(WindowPosition.CenterOnParent);
			dialog.Resizable = true;
		}

		internal static void ApplyChooser(FileChooserDialog chooser)
		{
			if (chooser == null)
			{
				return;
			}

			chooser.SetDefaultSize(DialogWidth, FileChooserHeight);
			chooser.SetPosition(WindowPosition.CenterOnParent);
			chooser.Resizable = true;
		}
	}
}
