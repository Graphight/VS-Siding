using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using Cake.Common;
using Cake.Common.IO;
using Cake.Common.Tools.DotNet;
using Cake.Common.Tools.DotNet.Clean;
using Cake.Common.Tools.DotNet.Publish;
using Cake.Core;
using Cake.Core.Diagnostics;
using Cake.Frosting;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Vintagestory.API.Common;

namespace CakeBuild;

public static class Program
{
    public static int Main(string[] args)
    {
        return new CakeHost()
            .UseContext<BuildContext>()
            .Run(args);
    }
}

public class BuildContext : FrostingContext
{
    public const string ProjectName = "VSSiding";
    public string BuildConfiguration { get; }
    public string Version { get; }
    public string Name { get; }

    public BuildContext(ICakeContext context)
        : base(context)
    {
        BuildConfiguration = context.Argument("configuration", "Release");
        var modInfo = JsonConvert.DeserializeObject<ModInfo>(File.ReadAllText($"../{ProjectName}/modinfo.json"))!;
        Version = modInfo.Version;
        Name = modInfo.ModID;
    }
}

[TaskName("ValidateJson")]
public sealed class ValidateJsonTask : FrostingTask<BuildContext>
{
    public override void Run(BuildContext context)
    {
        var jsonFiles = context.GetFiles($"../{BuildContext.ProjectName}/assets/**/*.json");
        foreach (var file in jsonFiles)
        {
            try
            {
                var json = File.ReadAllText(file.FullPath);
                JToken.Parse(json);
            }
            catch (JsonException ex)
            {
                throw new Exception(
                    $"Validation failed for JSON file: {file.FullPath}{Environment.NewLine}{ex.Message}", ex);
            }
        }
    }
}

[TaskName("Build")]
[IsDependentOn(typeof(ValidateJsonTask))]
public sealed class BuildTask : FrostingTask<BuildContext>
{
    public override void Run(BuildContext context)
    {
        context.DotNetClean($"../{BuildContext.ProjectName}/{BuildContext.ProjectName}.csproj",
            new DotNetCleanSettings
            {
                Configuration = context.BuildConfiguration
            });


        context.DotNetPublish($"../{BuildContext.ProjectName}/{BuildContext.ProjectName}.csproj",
            new DotNetPublishSettings
            {
                Configuration = context.BuildConfiguration
            });
    }
}

[TaskName("Package")]
[IsDependentOn(typeof(BuildTask))]
public sealed class PackageTask : FrostingTask<BuildContext>
{
    public override void Run(BuildContext context)
    {
        context.EnsureDirectoryExists("../Releases");
        context.CleanDirectory("../Releases");
        context.EnsureDirectoryExists($"../Releases/{context.Name}");
        context.CopyFiles($"../{BuildContext.ProjectName}/bin/{context.BuildConfiguration}/Mods/mod/publish/*",
            $"../Releases/{context.Name}");
        if (context.DirectoryExists($"../{BuildContext.ProjectName}/assets"))
        {
            context.CopyDirectory($"../{BuildContext.ProjectName}/assets", $"../Releases/{context.Name}/assets");
        }

        context.CopyFile($"../{BuildContext.ProjectName}/modinfo.json", $"../Releases/{context.Name}/modinfo.json");
        if (context.FileExists($"../{BuildContext.ProjectName}/modicon.png"))
        {
            context.CopyFile($"../{BuildContext.ProjectName}/modicon.png", $"../Releases/{context.Name}/modicon.png");
        }

        context.Zip($"../Releases/{context.Name}", $"../Releases/{context.Name}_{context.Version}.zip");
    }
}

// Downloads the latest release of --probe=<modid> and, recursively, its declared dependencies into compat/mods/slotN.zip, the fixed names the probe scenarios stage.
// Atlas fails the boot on a path that does not exist, so slots the download leaves empty hold a content-only mod of no assets.
[TaskName("FetchProbe")]
public sealed class FetchProbeTask : FrostingTask<BuildContext>
{
    private const int Slots = 6;
    private static readonly string[] Provided = { "game", "survival", "creative" };

    public override void Run(BuildContext context)
    {
        string modsDir = "../compat/mods";
        context.EnsureDirectoryExists(modsDir);
        context.CleanDirectory(modsDir);
        using var http = new HttpClient();
        var seen = new HashSet<string>();
        var queue = new Queue<string>();
        queue.Enqueue(context.Argument<string>("probe"));
        while (queue.Count > 0)
        {
            string modid = queue.Dequeue();
            if (!seen.Add(modid)) continue;

            var release = JObject.Parse(http.GetStringAsync($"https://mods.vintagestory.at/api/mod/{modid}").Result)["mod"]?["releases"]?[0];
            if (release == null) throw new Exception($"The mod DB has no release for '{modid}'");

            byte[] zip = http.GetByteArrayAsync((string)release["mainfile"]!).Result;
            if (seen.Count > Slots) throw new Exception($"More than {Slots} mods to fetch for the probe; raise the slots here and in CompatProbeScenarios");
            File.WriteAllBytes($"{modsDir}/slot{seen.Count - 1}.zip", zip);
            context.Log.Information("{0} {1}", modid, release["modversion"]);

            using var archive = new ZipArchive(new MemoryStream(zip));
            using var reader = new StreamReader(archive.GetEntry("modinfo.json")!.Open());
            if (JObject.Parse(reader.ReadToEnd())["dependencies"] is not JObject dependencies) continue;
            foreach (var dependency in dependencies.Properties())
                if (Array.IndexOf(Provided, dependency.Name) < 0) queue.Enqueue(dependency.Name);
        }

        for (int slot = seen.Count; slot < Slots; slot++)
        {
            using var padding = new ZipArchive(File.Create($"{modsDir}/slot{slot}.zip"), ZipArchiveMode.Create);
            using var writer = new StreamWriter(padding.CreateEntry("modinfo.json").Open());
            writer.Write($"{{ \"type\": \"content\", \"modid\": \"probepadding{slot}\", \"name\": \"Probe padding\", \"version\": \"1.0.0\" }}");
        }
    }
}

[TaskName("Default")]
[IsDependentOn(typeof(PackageTask))]
public class DefaultTask : FrostingTask
{
}
