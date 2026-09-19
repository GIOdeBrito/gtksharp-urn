using System;
using System.Globalization;

namespace UrnWrapper.UI
{
	internal static class ProfileFormatting
	{
		internal static string FormatLastExecuted(DateTime? lastExecuted)
		{
			if (!lastExecuted.HasValue)
			{
				return "Never";
			}

			return lastExecuted.Value.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
		}
	}
}
