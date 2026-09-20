using System.Text.Json.Serialization;

namespace UrnWrapper.Models
{
	public sealed record SandboxOptions(
		[property: JsonPropertyName("shareNetwork")] bool ShareNetwork = true,
		[property: JsonPropertyName("allowGpu")] bool AllowGpu = true,
		[property: JsonPropertyName("allowX11")] bool AllowX11 = true,
		[property: JsonPropertyName("allowWayland")] bool AllowWayland = true,
		[property: JsonPropertyName("allowAudio")] bool AllowAudio = false,
		[property: JsonPropertyName("allowAppImage")] bool AllowAppImage = false,
		[property: JsonPropertyName("appImageExtractAndRun")] bool AppImageExtractAndRun = false
	);
}
