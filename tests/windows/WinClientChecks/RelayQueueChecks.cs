using System.Net;
using System.Text.Json;
using Microsoft.UI.Xaml.Controls;
using Paperang.Core;

namespace Paperang.WinClient;

internal sealed partial class WorkspaceWindow
{
	private async Task CheckRemoteQueue()
	{
		var navigation = (NavigationView)Content;
		var previousPage = navigation.SelectedItem;
		navigation.SelectedItem = navigation.MenuItems.Cast<NavigationViewItem>().Single(item => (string)item.Tag == "relay");
		await Task.Delay(100);
		var first = new RelayJob { Id = new string('a', 40), Text = "queued sample", Created = 1 };
		var second = first with { Id = new string('b', 40), State = "unknown", Text = "uncertain sample", Created = 2 };
		ApplyRemoteQueue(JsonSerializer.SerializeToElement(new { jobs = new[] { first, second } }, RelayClient.Json));
		if (relayQueue.Items.Count != 2) throw new InvalidOperationException("Queue inventory missing entries");
		relayFilter.SelectedIndex = 1;
		if (relayQueue.Items.Count != 1 || ((RemoteRow)relayQueue.Items[0]).Job.Id != first.Id)
			throw new InvalidOperationException("Queued filter included a non-queued job");
		relayFilter.SelectedIndex = 0; relaySearch.Text = "uncertain";
		await Task.Delay(80);
		if (relayQueue.Items.Count != 1 || ((RemoteRow)relayQueue.Items[0]).Job.Id != second.Id)
			throw new InvalidOperationException($"Remote task search returned the wrong job: filter={relayFilter.SelectedIndex}, search={relaySearch.Text}, count={relayQueue.Items.Count}");
		relaySearch.Text = "";
		await Task.Delay(80);
		relayQueue.SelectedItem = relayQueue.Items.Cast<RemoteRow>().Single(row => row.Job.Id == first.Id);
		using var fixture = new QueueFixture(first);
		adminClient = new RelayClient(new AppSettings { Server = "http://localhost", AuthenticationMode = "legacy", DeviceKey = "psr_fixture" }, fixture);
		try
		{
			if ((await FullRemoteJob(default)).Id != first.Id) throw new Exception("Full task selection changed");
			fixture.Truncated = true;
			try { await FullRemoteJob(default); throw new Exception("A summary was accepted for preview"); }
			catch (InvalidDataException) { }
		}
		finally { LockAdministration(); remoteJobs = []; FilterRemoteJobs(); navigation.SelectedItem = previousPage; }
		Console.WriteLine("PASS remote queue filtering, search, full-content authorization route and summary rejection");
	}
	private sealed class QueueFixture(RelayJob job) : HttpMessageHandler
	{
		internal bool Truncated;
		protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
		{
			if (request.Method != HttpMethod.Post || request.RequestUri!.AbsolutePath != "/v2/admin/jobs/search")
				throw new Exception("Preview did not use administrator search");
			using var json = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(token));
			if (json.RootElement.GetProperty("job_id").GetString() != job.Id) throw new Exception("Wrong preview job ID");
			return new(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(new { jobs = new[] { job with { Truncated = Truncated } } }, RelayClient.Json)) };
		}
	}
}
