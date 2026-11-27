using System.Text.Json.Serialization;

namespace Devkit.Application.Api.Contracts;

public sealed record SystemSettingsContract([property: JsonRequired] string? TimeZone);
