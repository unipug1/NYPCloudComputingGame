using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

// Builds the game for the web (WebGL) into <project>/WebBuild (a separate git repo published to GitHub Pages).
//  - Menu: Tools > Build > WebGL (for GitHub Pages)
//  - Or drop an empty file called "WebBuild.request" in the project folder; the open editor picks it up.
// Progress / result is written to <project>/WebBuild.status. The WebBuild/.git folder is kept between builds.
[InitializeOnLoad]
public static class WebGLBuilder
{
    const string OUT = "WebBuild";
    const string TITLE = "NYP Open House - Cloud Game";
    static double nextCheck;

    static string Root => Path.GetDirectoryName(Application.dataPath);
    static string RequestFile => Path.Combine(Root, "WebBuild.request");
    static string StatusFile => Path.Combine(Root, "WebBuild.status");

    static WebGLBuilder() { EditorApplication.update += Poll; }

    static void Poll()
    {
        if (EditorApplication.timeSinceStartup < nextCheck) return;
        nextCheck = EditorApplication.timeSinceStartup + 2;
        if (!File.Exists(RequestFile) || EditorApplication.isCompiling || EditorApplication.isPlaying) return;
        File.Delete(RequestFile);
        EditorApplication.delayCall += Build;
    }

    static void Status(string s) { File.WriteAllText(StatusFile, System.DateTime.Now.ToString("HH:mm:ss") + " " + s); Debug.Log("[WebGLBuilder] " + s); }

    [MenuItem("Tools/Build/WebGL (for GitHub Pages)")]
    public static void Build()
    {
        if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.WebGL, BuildTarget.WebGL))
        {
            Status("FAILED: WebGL Build Support module is not installed (Unity Hub > Installs > Add modules).");
            EditorUtility.DisplayDialog("WebGL build", "WebGL Build Support isn't installed for this Unity version.\nAdd it in Unity Hub > Installs > (this version) > Add modules.", "OK");
            return;
        }

        Status("BUILDING...");
        // GitHub Pages can't send gzip headers, so let the loader unpack it in the browser
        PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
        PlayerSettings.WebGL.decompressionFallback = true;
        PlayerSettings.WebGL.nameFilesAsHashes = false;
        PlayerSettings.WebGL.dataCaching = true;
        PlayerSettings.WebGL.template = "APPLICATION:Default";
        PlayerSettings.runInBackground = true;

        var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
        string outPath = Path.Combine(Root, OUT);
        CleanKeepGit(outPath);

        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = outPath,
            target = BuildTarget.WebGL,
            targetGroup = BuildTargetGroup.WebGL,
            options = BuildOptions.None
        });
        var r = report.summary;
        if (r.result == BuildResult.Succeeded)
        {
            File.WriteAllText(Path.Combine(outPath, ".nojekyll"), "");
            var index = Path.Combine(outPath, "index.html");
            if (File.Exists(index))
            {
                var html = File.ReadAllText(index);
                html = System.Text.RegularExpressions.Regex.Replace(html, "<title>.*?</title>", "<title>" + TITLE + "</title>");
                html = System.Text.RegularExpressions.Regex.Replace(html, "<div id=\"unity-build-title\">.*?</div>", "<div id=\"unity-build-title\">" + TITLE + "</div>");
                File.WriteAllText(index, html);
            }
            Status("SUCCEEDED " + (r.totalSize / (1024f * 1024f)).ToString("0.0") + " MB in " + r.totalTime.TotalMinutes.ToString("0.0") + " min, scenes: " + string.Join(", ", scenes));
        }
        else
        {
            var errs = report.steps.SelectMany(s => s.messages).Where(m => m.type == LogType.Error || m.type == LogType.Exception).Select(m => m.content).Take(15);
            Status("FAILED (" + r.result + ", " + r.totalErrors + " errors)\n" + string.Join("\n", errs));
        }
    }

    // empty the output folder but keep its .git, so the build can be committed and pushed as-is
    static void CleanKeepGit(string dir)
    {
        if (!Directory.Exists(dir)) return;
        foreach (var f in Directory.GetFiles(dir)) File.Delete(f);
        foreach (var d in Directory.GetDirectories(dir))
            if (Path.GetFileName(d) != ".git") Directory.Delete(d, true);
    }
}
