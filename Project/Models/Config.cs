using System.Text.Json.Serialization;

namespace UrnWrapper.Models
{
	public sealed record Config(
		[property: JsonPropertyName("defaultHome")] string DefaultHome,
		[property: JsonPropertyName("defaultCommand")] string DefaultCommand
	);
}
