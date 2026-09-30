using System.Text.Json.Serialization;
using Tomlyn.Serialization;

namespace Moinsearch.Configuration;

/// <summary>
/// ~/.moinsearch.toml の未検証な生モデル。
/// </summary>
internal sealed class TomlConfigModel
{
    [JsonPropertyName("url")]
    public string? Url { get; set; }

    [JsonPropertyName("username")]
    public string? Username { get; set; }

    [JsonPropertyName("password")]
    public string? Password { get; set; }
}

/// <summary>
/// Tomlyn のソース生成コンテキスト。Native AOT では既定でリフレクションが無効化されるため、
/// TOML ⇔ POCO のマッピングをコンパイル時に生成してリフレクションに依存しないようにする。
/// </summary>
[TomlSerializable(typeof(TomlConfigModel))]
internal partial class MoinsearchTomlContext : TomlSerializerContext
{
}
