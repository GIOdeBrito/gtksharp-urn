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

			// Ensure config.json exists and is valid. Values are reserved
			// for future bwrap template expansion and intentionally unused.
			AppStorage.LoadConfig();

			Window mainWindow = MainWindow.Build();
			mainWindow.ShowAll();
			MainWindow.DrainPendingEvents();

			Application.Run();
		}
	}
}
