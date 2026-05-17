using System.Text.Json;
using Json.Schema;
using PhoneFork.Core.Services;

namespace PhoneFork.Core.Tests;

public class SchemaCompatibilityTests
{
    [Fact]
    public void OpenArchiveManifestMatchesDraft202012CompatibilitySchema()
    {
        var manifest = new OpenArchiveManifest
        {
            CreatedAt = DateTimeOffset.Parse("2026-05-17T12:00:00Z"),
            ToolVersion = "0.9.0-pre",
            MigrationId = "mig-test",
            Source = new ArchiveEndpointInfo
            {
                DeviceHash = "0123456789ab",
                Label = "Galaxy Source",
                AndroidVersion = "16",
                OneUiVersion = "8.5",
            },
            Categories =
            [
                new CategoryEntry
                {
                    Name = "apps",
                    File = "apps/manifest.json",
                    Sha256 = new string('a', 64),
                    Bytes = 1234,
                    Rows = 2,
                },
            ],
            CrossPlatform = new CrossPlatformMetadata
            {
                IosCompatibleApps = ["com.example.app"],
                Notes = ["test"],
            },
        };

        using var json = JsonSerializer.SerializeToDocument(manifest);
        var result = OpenArchiveManifestSchema.Evaluate(json.RootElement, new EvaluationOptions
        {
            OutputFormat = OutputFormat.List,
        });

        Assert.True(result.IsValid, JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));
    }

    private static readonly JsonSchema OpenArchiveManifestSchema = JsonSchema.FromText("""
    {
      "$schema": "https://json-schema.org/draft/2020-12/schema",
      "type": "object",
      "required": ["schema", "createdAt", "toolVersion", "migrationId", "source", "categories"],
      "properties": {
        "schema": { "const": "phonefork-open-archive-v1" },
        "createdAt": { "type": "string", "format": "date-time" },
        "toolVersion": { "type": "string", "minLength": 1 },
        "migrationId": { "type": "string", "minLength": 1 },
        "source": { "$ref": "#/$defs/endpoint" },
        "destination": {
          "oneOf": [
            { "$ref": "#/$defs/endpoint" },
            { "type": "null" }
          ]
        },
        "categories": {
          "type": "array",
          "items": { "$ref": "#/$defs/category" }
        },
        "notes": {
          "type": "array",
          "items": { "type": "string" }
        },
        "crossPlatform": {
          "oneOf": [
            { "$ref": "#/$defs/crossPlatform" },
            { "type": "null" }
          ]
        }
      },
      "$defs": {
        "endpoint": {
          "type": "object",
          "required": ["deviceHash", "label", "androidVersion", "oneUiVersion"],
          "properties": {
            "deviceHash": { "type": "string", "pattern": "^[0-9a-f]{12}$" },
            "label": { "type": "string" },
            "androidVersion": { "type": "string" },
            "oneUiVersion": { "type": "string" }
          }
        },
        "category": {
          "type": "object",
          "required": ["name", "file", "sha256", "bytes"],
          "properties": {
            "name": { "type": "string", "minLength": 1 },
            "file": { "type": "string", "minLength": 1 },
            "sha256": { "type": "string", "pattern": "^[0-9a-f]{64}$" },
            "bytes": { "type": "integer", "minimum": 0 },
            "rows": {
              "oneOf": [
                { "type": "integer", "minimum": 0 },
                { "type": "null" }
              ]
            }
          }
        },
        "crossPlatform": {
          "type": "object",
          "required": ["iosCompatibleApps", "schemaVersion"],
          "properties": {
            "iosCompatibleApps": {
              "type": "array",
              "items": { "type": "string" }
            },
            "schemaVersion": { "type": "integer", "const": 1 },
            "notes": {
              "type": "array",
              "items": { "type": "string" }
            }
          }
        }
      }
    }
    """);
}
