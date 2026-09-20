using System;
using System.Text.Json.Serialization;

namespace UrnWrapper.Models
{
	public sealed record SandboxProfile(
		[property: JsonPropertyName("name")] string Name,
		[property: JsonPropertyName("program")] string Program = "",
		[property: JsonPropertyName("arguments")] string Arguments = "",
		[property: JsonPropertyName("lastExecuted")] DateTime? LastExecuted = null,
		[property: JsonPropertyName("options")] SandboxOptions? Options = null);
}
