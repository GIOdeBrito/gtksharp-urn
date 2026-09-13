using System;
using Gtk;

namespace GtkSharpDemo
{
	internal static class Program
	{
		[STAThread]
		private static void Main()
		{
			Application.Init();

			Window mainWindow = BuildMainWindow();
			mainWindow.ShowAll();
			DrainPendingEvents();

			Application.Run();
		}

		private static void DrainPendingEvents()
		{
			// Let the display backend (re)size the window before the first
			// paint, so the initial frame never targets an infinite surface.
			while (GLib.MainContext.Iteration(false))
			{
			}
		}

		private static Window BuildMainWindow()
		{
			var window = new Window("GtkSharp Demo");
			window.SetSizeRequest(400, 200);

			var greetingLabel = new Label("Hello from GtkSharp!");
			var clickButton = new Button("Click me");

			clickButton.Clicked += (sender, args) =>
			{
				greetingLabel.Text = "Button was clicked!";
			};

			var layout = new Box(Orientation.Vertical, 6);
			layout.PackStart(greetingLabel, true, true, 0);
			layout.PackStart(clickButton, false, false, 0);

			window.Add(layout);
			window.DeleteEvent += OnWindowDelete;

			return window;
		}

		private static void OnWindowDelete(object sender, DeleteEventArgs args)
		{
			Application.Quit();
			args.RetVal = true;
		}
	}
}