using Gtk;
using UrnWrapper.Models;
using UrnWrapper.Persistence;

namespace UrnWrapper.UI
{
	internal sealed class SandboxPermissionControls
	{
		internal CheckButton NetworkCheck { get; }
		internal CheckButton GpuCheck { get; }
		internal CheckButton X11Check { get; }
		internal CheckButton WaylandCheck { get; }
		internal CheckButton AudioCheck { get; }
		internal CheckButton AppImageCheck { get; }

		private SandboxPermissionControls(SandboxOptions options)
		{
			NetworkCheck = BuildCheck("Network (--share-net)", "Share host network inside the sandbox.", options.ShareNetwork);
			GpuCheck = BuildCheck("GPU / DRI (/dev/dri)", "Expose GPU device nodes for hardware acceleration.", options.AllowGpu);
			X11Check = BuildCheck("X11 (/tmp/.X11-unix + DISPLAY)", "Expose the X11 socket and DISPLAY variable.", options.AllowX11);
			WaylandCheck = BuildCheck("Wayland (socket + WAYLAND_DISPLAY)", "Expose only the Wayland socket, not the whole runtime dir.", options.AllowWayland);
			AudioCheck = BuildCheck("Audio (Pulse/PipeWire)", "Expose audio sockets and set PULSE_SERVER.", options.AllowAudio);
			AppImageCheck = BuildCheck("AppImage / FUSE (fusermount)", "Expose FUSE helpers for Type2 AppImages. Increases kernel surface, enable only for AppImages.", options.AllowAppImage);
		}

		internal static SandboxPermissionControls FromOptions(SandboxOptions? options)
		{
			SandboxOptions effective = AppStorage.NormalizeOptions(options);
			return new SandboxPermissionControls(effective);
		}

		internal SandboxOptions ToOptions()
		{
			return new SandboxOptions(
				NetworkCheck.Active,
				GpuCheck.Active,
				X11Check.Active,
				WaylandCheck.Active,
				AudioCheck.Active,
				AppImageCheck.Active
			);
		}

		internal Frame BuildFrame()
		{
			var list = new Box(Orientation.Vertical, 2);
			list.PackStart(NetworkCheck, false, false, 0);
			list.PackStart(GpuCheck, false, false, 0);
			list.PackStart(X11Check, false, false, 0);
			list.PackStart(WaylandCheck, false, false, 0);
			list.PackStart(AudioCheck, false, false, 0);
			list.PackStart(AppImageCheck, false, false, 0);

			var frame = new Frame("Sandbox permissions");
			frame.Add(list);

			return frame;
		}

		private static CheckButton BuildCheck(string label, string tooltip, bool active)
		{
			var check = new CheckButton(label);
			check.TooltipText = tooltip;
			check.Active = active;

			return check;
		}
	}
}
