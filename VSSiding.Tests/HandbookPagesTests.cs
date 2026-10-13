using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using Xunit;

namespace VSSiding.Tests;

public class HandbookPagesTests
{
    const string GuideCode = "gamemechanicinfo-siding";
    static readonly string[] Tags = { "font", "itemstack", "icon", "hk", "a", "clear", "code", "strong", "i", "br" };

    static readonly string AssetsRoot = Path.Combine(MaterialTextureOpacityTests.GetAssemblyMetadata("RepoRoot"), "VSSiding", "assets", "vssiding");

    record Page(string PageCode, string Title, string Text);

    static (List<Page> Pages, List<string> MissingKeys) Load()
    {
        var lang = (JObject)JToken.Parse(File.ReadAllText(Path.Combine(AssetsRoot, "lang", "en.json")));
        var pages = new List<Page>();
        var missing = new List<string>();
        foreach (var file in Directory.EnumerateFiles(Path.Combine(AssetsRoot, "config", "handbook"), "*.json").OrderBy(f => f))
        {
            var json = (JObject)JToken.Parse(File.ReadAllText(file));
            string code = (string)json["pageCode"]!;
            var strings = new List<string>();
            foreach (var field in new[] { "title", "text" })
            {
                string key = ((string)json[field]!)["vssiding:".Length..];
                string? value = (string?)lang[key];
                if (value == null) missing.Add($"{code}: {key}");
                strings.Add(value ?? "");
            }
            pages.Add(new Page(code, strings[0], strings[1]));
        }
        return (pages, missing);
    }

    [Fact]
    public void EveryPageTitleAndTextKeyExistsInLangFile()
    {
        Assert.Equal(Array.Empty<string>(), Load().MissingKeys.ToArray());
    }

    [Fact]
    public void EverySidingHandbookLinkNamesAPageThatExists()
    {
        var (pages, _) = Load();
        var codes = pages.Select(p => p.PageCode).ToHashSet();
        var dead = pages
            .SelectMany(p => Regex.Matches(p.Text, @"handbook://(gamemechanicinfo-siding[a-z-]*)").Select(m => (p.PageCode, Target: m.Groups[1].Value)))
            .Where(link => !codes.Contains(link.Target))
            .Select(link => $"{link.PageCode} -> {link.Target}")
            .ToArray();

        Assert.Equal(Array.Empty<string>(), dead);
    }

    [Fact]
    public void EveryPageLinksBackToTheGuideAndTheGuideLinksToEveryPage()
    {
        var (pages, _) = Load();
        var guide = pages.Single(p => p.PageCode == GuideCode);
        var others = pages.Where(p => p != guide).ToList();

        var unlinkedFromGuide = others.Where(p => !guide.Text.Contains($"handbook://{p.PageCode}\"")).Select(p => p.PageCode).ToArray();
        var noLinkBack = others.Where(p => !p.Text.Contains($"handbook://{GuideCode}\"")).Select(p => p.PageCode).ToArray();

        Assert.Equal((Array.Empty<string>(), Array.Empty<string>()), (unlinkedFromGuide, noLinkBack));
    }

    [Fact]
    public void EveryIconPathNamesAFileUnderTextures()
    {
        var (pages, _) = Load();
        var missing = pages
            .SelectMany(p => Regex.Matches(p.Text, "<icon path=\"vssiding:([^\"]+)\"").Select(m => (p.PageCode, Path: m.Groups[1].Value)))
            .Where(i => !File.Exists(Path.Combine(AssetsRoot, "textures", i.Path)))
            .Select(i => $"{i.PageCode}: {i.Path}")
            .ToArray();

        Assert.Equal(Array.Empty<string>(), missing);
    }

    [Fact]
    public void EveryTagIsFromTheRendererTableAndClosed()
    {
        var (pages, _) = Load();
        var problems = new List<string>();
        foreach (var page in pages)
        {
            var open = new Stack<string>();
            foreach (Match m in Regex.Matches(page.Title + page.Text, @"<(/?)([a-zA-Z]+)[^>]*>"))
            {
                string name = m.Groups[2].Value;
                if (!Tags.Contains(name)) { problems.Add($"{page.PageCode}: unknown <{name}>"); continue; }
                if (name == "br") continue;
                if (m.Groups[1].Value == "")
                    open.Push(name);
                else if (open.Count > 0 && open.Peek() == name)
                    open.Pop();
                else
                    problems.Add($"{page.PageCode}: stray </{name}>");
            }
            problems.AddRange(open.Select(name => $"{page.PageCode}: unclosed <{name}>"));
        }

        Assert.Equal(Array.Empty<string>(), problems.ToArray());
    }
}
