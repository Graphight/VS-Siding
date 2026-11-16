using System.IO;
using Newtonsoft.Json.Linq;
using Xunit;

namespace VSSiding.Tests;

// A vent block is its source block under another code, so a change to wall.json or floor.json has to reach it.
public class VentBlocktypesTests
{
    [Theory]
    [InlineData("wall", "wallvent")]
    [InlineData("floor", "floorvent")]
    public void VentBlocktypeIsItsSourceUnderAnotherCode(string source, string vent)
    {
        var dir = Path.Combine(MaterialTextureOpacityTests.GetAssemblyMetadata("RepoRoot"), "VSSiding", "assets", "vssiding", "blocktypes");
        var expected = JObject.Parse(File.ReadAllText(Path.Combine(dir, source + ".json")));
        expected["code"] = vent;
        expected["creativeinventory"] = new JObject();

        Assert.True(JToken.DeepEquals(expected, JObject.Parse(File.ReadAllText(Path.Combine(dir, vent + ".json")))));
    }
}
