using System.Text.Json;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using Paperang.Core;
using Paperang.Windows;

namespace Paperang.WinClient;

internal sealed partial class WorkspaceWindow
{
	private readonly ComboBox relayFilter = Choice("任务状态", "全部", "待处理", "进行中", "已发送", "纸面完成", "异常", "已拒绝");
	private readonly TextBox relaySearch = new() { Header = "搜索任务 ID、发送者或摘要" };
	private readonly ListView relayQueue = new() { Header = "远程任务", MaxHeight = 300 };
	private readonly TextBox relayDetail = new() { Header = "任务详情", IsReadOnly = true, AcceptsReturn = true,
		TextWrapping = TextWrapping.Wrap, MaxHeight = 240 };
	private readonly Image relayPaper = new() { MaxHeight = 420, Stretch = Microsoft.UI.Xaml.Media.Stretch.Uniform };
	private RelayJob[] remoteJobs = [];
	private sealed record RemoteRow(RelayJob Job)
	{
		public override string ToString() => $"{(string.IsNullOrWhiteSpace(Job.ShortID) ? Job.Id[..8] : Job.ShortID)} · {RemoteState(Job.State)} · {Job.Sender} · {Job.Text}";
	}
	private static string RemoteState(string state) => state switch
	{
		"queued" => "待处理", "claimed" => "已认领", "printing" => "发送中", "sent" => "已发送，待核对纸面",
		"confirmed" => "纸面完成", "unknown" => "结果不确定", "rejected" => "已拒绝", _ => state
	};
	private void BuildRelayQueue()
	{
		relay.Children.Add(Heading("任务队列"));
		relay.Children.Add(Paragraph("自动打印关闭时，可以认领打印所选待处理任务。查看完整内容需要先在管理页面登录 C 权限；执行凭证不会获取未认领正文。"));
		relay.Children.Add(TwoColumns(relayFilter, relaySearch));
		relayFilter.SelectionChanged += (_, _) => FilterRemoteJobs();
		relaySearch.TextChanged += (_, _) => FilterRemoteJobs();
		relay.Children.Add(ActionButton("刷新远程队列", RefreshRemoteJobs));
		relay.Children.Add(relayQueue);
		relayQueue.SelectionChanged += (_, _) =>
		{
			relayPaper.Source = null;
			relayDetail.Text = relayQueue.SelectedItem is RemoteRow row ? JsonSerializer.Serialize(row.Job, RelayClient.Json) : "";
		};
		relay.Children.Add(ActionButton("预览完整纸条", async token =>
		{
			var job = await FullRemoteJob(token);
			var image = new NoteRenderer().Render(job, tray.Settings()).Validate();
			relayPaper.Source = await RasterImage(image);
			relayDetail.Text = JsonSerializer.Serialize(job, RelayClient.Json);
		}));
		relay.Children.Add(ActionButton("载入完整内容到工作台", async token =>
		{
			var job = await FullRemoteJob(token);
			// Preserve the exact Relay template and drawing composition; importing is not a send.
			var image = new NoteRenderer().Render(job, tray.Settings()).Validate();
			sourceMode = "raster"; importedRaster = image;
			await Preview(image, applyTrim: false);
			openWorkspace?.Invoke();
		}));
		relay.Children.Add(ActionButton("认领并打印所选任务…", async token =>
		{
			var job = SelectedRemoteJob();
			if (job.State != "queued") throw new InvalidOperationException("仅可认领待处理任务，请先刷新队列。");
			if (!await Confirm(relay, "认领并打印此任务？", $"{job.Id}\n{job.Sender}\n将消耗打印纸；自动打印设置保持当前选择。")) return;
			await tray.RequestRemotePrintAsync(job.Id, token);
			Report("已请求认领所选任务，请在中转状态查看发送结果。");
		}));
		relay.Children.Add(ActionButton("拒绝所选待处理任务…", async token =>
		{
			var job = SelectedRemoteJob();
			if (job.State != "queued") throw new InvalidOperationException("仅可拒绝待处理任务。");
			if (!await Confirm(relay, "拒绝此任务？", job.Id)) return;
			using var client = new RelayClient(tray.Settings(), productVersion: Program.Version);
			await client.Post($"v2/device/jobs/{Uri.EscapeDataString(job.Id)}/reject", new { }, token);
			await RefreshRemoteJobs(token);
		}));
		relay.Children.Add(relayDetail);
		relay.Children.Add(relayPaper);
		Closed += (_, _) => { remoteJobs = []; relayDetail.Text = ""; relayPaper.Source = null; };
	}
	private RelayJob SelectedRemoteJob() => (relayQueue.SelectedItem as RemoteRow)?.Job ??
		throw new InvalidOperationException("请先选择远程任务。");
	private async Task RefreshRemoteJobs(CancellationToken token)
	{
		using var client = new RelayClient(tray.Settings(), productVersion: Program.Version);
		ApplyRemoteQueue(await client.Get("v2/device/jobs", token));
	}
	private void ApplyRemoteQueue(JsonElement result)
	{
		var next = result.GetProperty("jobs").Deserialize<RelayJob[]>(RelayClient.Json) ?? [];
		if (next.Length > 100 || next.Select(job => job.Id).Distinct().Count() != next.Length)
			throw new InvalidDataException("队列数量或任务 ID 无效。");
		foreach (var job in next) job.Validate();
		remoteJobs = next;
		FilterRemoteJobs();
	}
	private void FilterRemoteJobs()
	{
		string? selected = (relayQueue.SelectedItem as RemoteRow)?.Job.Id;
		string query = relaySearch.Text.Trim();
		var rows = remoteJobs.Where(job => relayFilter.SelectedIndex switch
		{
			1 => job.State == "queued", 2 => job.State is "claimed" or "printing", 3 => job.State == "sent",
			4 => job.State == "confirmed", 5 => job.State == "unknown", 6 => job.State == "rejected", _ => true
		}).Where(job => query.Length == 0 || $"{job.Id} {job.ShortID} {job.Sender} {job.Text}".Contains(query, StringComparison.OrdinalIgnoreCase))
			.OrderByDescending(job => job.Created).Select(job => new RemoteRow(job)).ToArray();
		relayQueue.ItemsSource = rows;
		relayQueue.SelectedItem = rows.FirstOrDefault(row => row.Job.Id == selected);
	}
	private async Task<RelayJob> FullRemoteJob(CancellationToken token)
	{
		var selected = SelectedRemoteJob();
		if (adminSessionServer != null && !string.Equals(adminSessionServer,
			tray.Settings().Server.TrimEnd('/'), StringComparison.OrdinalIgnoreCase))
			throw new InvalidOperationException("管理会话与接收器服务器不同，请登录接收器所在的管理服务器。");
		var result = await AdminClient().Post("v2/admin/jobs/search", new { job_id = selected.Id, limit = 1 }, token);
		var full = result.GetProperty("jobs").Deserialize<RelayJob[]>(RelayClient.Json)?.SingleOrDefault()?.Validate();
		if (full == null || full.Id != selected.Id || full.Truncated)
			throw new InvalidDataException("未获得完整任务正文，已阻止使用摘要。");
		return full;
	}
	private static async Task<BitmapImage> RasterImage(Raster image)
	{
		using var rendered = NoteRenderer.Preview(image);
		using var stream = new MemoryStream();
		rendered.Save(stream, System.Drawing.Imaging.ImageFormat.Png);
		stream.Position = 0;
		using var source = stream.AsRandomAccessStream();
		var bitmap = new BitmapImage();
		await bitmap.SetSourceAsync(source);
		return bitmap;
	}
}
