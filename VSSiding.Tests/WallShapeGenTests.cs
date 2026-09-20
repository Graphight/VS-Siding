using System.IO;
using Newtonsoft.Json.Linq;
using Xunit;

namespace VSSiding.Tests;

// The element table in WallShapeGen is a hand-transcribed reading of the uv derivation rules,
// checked against the committed shape files it is meant to replace. If it drifts from what the
// game actually loads, this is where that would show up.
public class WallShapeGenTests
{
    [Theory]
    [InlineData("wall")]
    [InlineData("cornerout")]
    public void GeneratedShapeMatchesTheCommittedOne(string layout)
    {
        var repoRoot = MaterialTextureOpacityTests.GetAssemblyMetadata("RepoRoot");
        var committed = JObject.Parse(File.ReadAllText(
            Path.Combine(repoRoot, "VSSiding", "assets", "vssiding", "shapes", "block", "wall", layout + ".json")));

        Assert.Equal(committed.ToString(), WallShapeGen.Generate(layout).ToString());
    }
}
