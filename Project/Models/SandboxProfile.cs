using System;
using System.Text.Json.Serialization;

namespace UrnWrapper.Models
{
	public sealed record SandboxProfile(
		[property: JsonPropertyName("name")] string Name,
		[property: JsonPropertyName("command")] string Command,
		[property: JsonPropertyName("lastExecuted")] DateTime? LastExecuted,
		[property: JsonPropertyName("options")] SandboxOptions? Options = null);
}
