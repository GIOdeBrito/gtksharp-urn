using System;

namespace UrnWrapper.Models
{
	public sealed record SandboxProfile(string Name, string Command, DateTime? LastExecuted);
}
