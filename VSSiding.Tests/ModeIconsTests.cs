using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace VSSiding.Tests;

// SidingModePicker builds each icon's path out of the mode's own code, and the loader returns null
// rather than throwing when the file isn't there - a renamed mode or a misspelt file ships as a
// blank tile with nothing in the log.
public class ModeIconsTests
{
    [Fact]
    public void EveryToolModeHasAnIconAndEveryIconHasAToolMode()
    {
        var repoRoot = MaterialTextureOpacityTests.GetAssemblyMetadata("RepoRoot");
        var source = File.ReadAllText(Path.Combine(repoRoot, "VSSiding", "SidingModePicker.cs"));
        var modes = Regex.Matches(source, @"""(\w+)"", new\[\] \{ ""(\w+)"", ""(\w+)"" \}")
            .SelectMany(m => new[] { m.Groups[2].Value, m.Groups[3].Value }).OrderBy(code => code).ToList();

        var icons = Directory.EnumerateFiles(
                Path.Combine(repoRoot, "VSSiding", "assets", "vssiding", "textures", "icons"), "*.svg")
            .Select(file => Path.GetFileNameWithoutExtension(file)!).OrderBy(name => name).ToList();

        Assert.Equal(new[] { "boards", "corner", "wall", "weatherboard" }, modes);
        Assert.Equal(modes, icons);
    }
}
