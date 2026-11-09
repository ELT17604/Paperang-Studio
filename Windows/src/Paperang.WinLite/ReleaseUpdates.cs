using System.Diagnostics;
using System.Text.Json;
using Paperang.Core;

namespace Paperang.WinLite;

internal static class ReleaseUpdates
{
    private static int busy;
    private static bool TryParse(string value, out (Version Base, int Stage, int Revision) parsed)
    {
        parsed = default;
        var match = System.Text.RegularExpressions.Regex.Match(value,
            @"^v?(\d+\.\d+\.\d+)-winlite(?:-(fix|opt)([1-9]\d*))?$");
        if (!match.Success || !Version.TryParse(match.Groups[1].Value, out var number)) return false;
        int revision = 0;
        if (match.Groups[3].Success && !int.TryParse(match.Groups[3].Value, out revision)) return false;
        parsed = (number, match.Groups[2].Value == "opt" ? 1 : 0, revision);
        return true;
    }
    internal static int Compare(string left, string right)
    {
        if (!TryParse(left, out var a) || !TryParse(right, out var b))
            throw new FormatException("Expected a WinLite distribution version");
        int result = a.Base.CompareTo(b.Base);
        if (result != 0) return result;
        result = a.Stage.CompareTo(b.Stage); return result != 0 ? result : a.Revision.CompareTo(b.Revision);
    }
    public static async Task Check()
    {
        if (Interlocked.Exchange(ref busy, 1) != 0) return;
        try {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(20), MaxResponseContentBufferSize = 2 * 1024 * 1024 };
            client.DefaultRequestHeaders.UserAgent.ParseAdd("PaperangStudio/" + AppVersion.Current);
            using var response = await client.GetAsync("https://api.github.com/repos/ELT17604/Paperang-Studio/releases?per_page=100");
            response.EnsureSuccessStatusCode();
            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            string? newest = null, url = null;
            foreach (var release in document.RootElement.EnumerateArray()) {
                if (release.GetProperty("draft").GetBoolean() || release.GetProperty("prerelease").GetBoolean()) continue;
                string tag = release.GetProperty("tag_name").GetString() ?? "";
                if (!TryParse(tag, out _)) continue;
                if (newest == null || Compare(tag, newest) > 0) { newest = tag; url = release.GetProperty("html_url").GetString(); }
            }
            if (newest == null) { MessageBox.Show("GitHub 尚未发布 WinLite 正式发行版。当前版本：v" + AppVersion.Current, "检查更新"); return; }
            if (Compare(newest, AppVersion.Current) <= 0) { MessageBox.Show("当前已是最新 WinLite 正式版。\n当前版本：v" + AppVersion.Current, "检查更新"); return; }
            if (url == null || !url.StartsWith("https://github.com/ELT17604/Paperang-Studio/releases/tag/", StringComparison.Ordinal)) throw new InvalidDataException();
            if (MessageBox.Show("发现 " + newest + "\n当前：v" + AppVersion.Current + "\n\n打开 GitHub 发行页面下载安装包？安装会保留现有配置。", "发现新版本", MessageBoxButtons.YesNo, MessageBoxIcon.Information) == DialogResult.Yes)
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        } catch { MessageBox.Show("无法检查更新，请检查网络或稍后重试。GitHub 可能限制了请求频率。\n当前版本：v" + AppVersion.Current, "检查更新", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
        finally { Interlocked.Exchange(ref busy, 0); }
    }
}
