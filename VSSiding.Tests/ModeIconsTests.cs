using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace VSSiding.Tests;

// PlaceWallFrame loads each mode's icon from a path built out of the mode's own code, and
// IGuiAPI.LoadSvg returns null rather than throwing when the file isn't there - a renamed mode
// or a misspelt file ships as a blank tile with nothing in the log.
public class ModeIconsTests
{
    [Fact]
    public void EveryToolModeHasAnIconAndEveryIconHasAToolMode()
    {
        var repoRoot = MaterialTextureOpacityTests.GetAssemblyMetadata("RepoRoot");
        var source = File.ReadAllText(Path.Combine(repoRoot, "VSSiding", "PlaceWallFrame.cs"));
        var modes = Regex.Matches(source, @"new SkillItem \{ Code = new AssetLocation\(""(\w+)""\)")
            .Select(m => m.Groups[1].Value).OrderBy(code => code).ToList();

        var icons = Directory.EnumerateFiles(
                Path.Combine(repoRoot, "VSSiding", "assets", "vssiding", "textures", "icons"), "*.svg")
            .Select(file => Path.GetFileNameWithoutExtension(file)!).OrderBy(name => name).ToList();

        Assert.Equal(new[] { "boards", "corner", "wall", "weatherboard" }, modes);
        Assert.Equal(modes, icons);
    }

    // The handbook's saw mode list draws the same four files inline. IconComponent prefixes the
    // path with textures/ and silently falls through to DrawIcon when the asset is missing, so a
    // wrong path here shows as a stray vanilla icon rather than an error.
    [Fact]
    public void TheHandbookModeListDrawsTheSameFourIcons()
    {
        var repoRoot = MaterialTextureOpacityTests.GetAssemblyMetadata("RepoRoot");
        var lang = File.ReadAllText(Path.Combine(repoRoot, "VSSiding", "assets", "vssiding", "lang", "en.json"));
        var referenced = Regex.Matches(lang, @"<icon path=\\""vssiding:icons/(\w+)\.svg\\"">")
            .Select(m => m.Groups[1].Value).OrderBy(code => code).ToList();

        Assert.Equal(new[] { "boards", "corner", "wall", "weatherboard" }, referenced.OrderBy(c => c));
        Assert.All(referenced, code => Assert.True(File.Exists(Path.Combine(
            repoRoot, "VSSiding", "assets", "vssiding", "textures", "icons", code + ".svg"))));
    }
}
