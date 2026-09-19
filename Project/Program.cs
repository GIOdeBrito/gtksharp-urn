using System;
using Gtk;
using UrnWrapper.Persistence;
using UrnWrapper.UI;

namespace UrnWrapper
{
	internal static class Program
	{
		[STAThread]
		private static void Main()
		{
			Application.Init();

			// Ensure config.json exists and is valid. Its values feed
			// the bwrap template used by the Run action.
			AppStorage.LoadConfig();

			Window mainWindow = MainWindow.Build();
			mainWindow.ShowAll();
			MainWindow.DrainPendingEvents();

			Application.Run();
		}
	}
}
