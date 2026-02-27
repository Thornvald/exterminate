using System.Text.Json;
using System.Text.Json.Serialization;
using Exterminate.Models;

namespace Exterminate.Services;

[JsonSourceGenerationOptions(
    PropertyNameCaseInsensitive = true,
    ReadCommentHandling = JsonCommentHandling.Skip,
    AllowTrailingCommas = true)]
[JsonSerializable(typeof(AppConfig))]
internal sealed partial class ExterminateJsonContext : JsonSerializerContext
{
}
