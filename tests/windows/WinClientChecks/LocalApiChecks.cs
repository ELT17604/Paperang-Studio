using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Paperang.Core;

namespace Paperang.WinClient;

internal sealed partial class WorkspaceWindow
{
	private async Task CheckLocalApi()
	{
		const string secret = "test-local-token-012345678901234567890123456789";
		using var server = new LoopbackServer(0, secret, ["https://example.com"], DispatchLocalApi);
		server.Start();
		using var http = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{server.Port}"), Timeout = TimeSpan.FromSeconds(8) };
		using (var response = await http.GetAsync("/v2/status"))
			if (response.StatusCode != HttpStatusCode.Unauthorized) throw new Exception("Unauthenticated API access succeeded");
		http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", secret);
		using (var request = new HttpRequestMessage(HttpMethod.Get, "/v2/status"))
		{
			request.Headers.Host = "untrusted.example";
			using var response = await http.SendAsync(request);
			if (response.StatusCode != HttpStatusCode.Forbidden) throw new Exception("API accepted a non-loopback Host");
		}
		using (var request = new HttpRequestMessage(HttpMethod.Get, "/v2/status"))
		{
			request.Headers.Add("Origin", "https://untrusted.example");
			using var response = await http.SendAsync(request);
			if (response.StatusCode != HttpStatusCode.Forbidden) throw new Exception("API accepted an unapproved Origin");
		}
		using (var request = new HttpRequestMessage(HttpMethod.Get, "/v2/status"))
		{
			request.Headers.Add("Origin", "https://example.com");
			using var response = await http.SendAsync(request);
			if (response.StatusCode != HttpStatusCode.OK || response.Headers.GetValues("Access-Control-Allow-Origin").Single() != "https://example.com")
				throw new Exception("API did not accept the approved Origin");
		}
		using (var response = await http.GetAsync("/v1/status"))
			if (response.StatusCode != HttpStatusCode.Gone) throw new Exception("API v1 retirement is inconsistent");
		using (var response = await http.GetAsync("/v2/logs?limit=1"))
		{
			using var result = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
			if (result.RootElement.GetProperty("logs").GetArrayLength() != 1) throw new Exception("API ignored log limit");
		}
		using (var response = await http.GetAsync("/v2/protocol"))
		{
			using var result = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
			if (result.RootElement.GetProperty("commands").GetArrayLength() != 37) throw new Exception("API omitted protocol catalog commands");
		}
		using (var response = await http.GetAsync("/v2/openapi"))
		{
			response.EnsureSuccessStatusCode();
			using var schema = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
			var paths = schema.RootElement.GetProperty("paths");
			if (!paths.GetProperty("/v2/jobs").TryGetProperty("get", out _) ||
			    !paths.GetProperty("/v2/jobs").TryGetProperty("post", out _) ||
			    !paths.TryGetProperty("/v2/jobs/{id}/cancel", out _))
				throw new Exception("Local API schema does not cover the implemented job routes");
		}
		using (var response = await http.PostAsync("/v2/render", JsonContent("{\"text\":\"API 中文 test\",\"includeRaw\":true}")))
		{
			response.EnsureSuccessStatusCode();
			using var result = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
			if (Convert.FromBase64String(result.RootElement.GetProperty("rawBase64").GetString()!).Length != result.RootElement.GetProperty("height").GetInt32() * 72)
				throw new Exception("Local rendering returned an incorrect raw size");
		}
		foreach (string invalid in new[] { "{\"text\":\"test\",\"settings\":{\"unknown\":1}}", "{\"text\":\"test\",\"imageBase64\":\"AA==\"}", "{\"text\":\"test\",\"settings\":{\"gamma\":0}}" })
			using (var response = await http.PostAsync("/v2/render", JsonContent(invalid)))
				if (response.StatusCode != HttpStatusCode.BadRequest) throw new Exception("Local rendering accepted invalid input");
		using (var response = await http.PostAsync("/v2/feed", JsonContent("{\"lines\":10,\"allowPaper\":false}")))
			if (response.StatusCode != HttpStatusCode.Forbidden) throw new Exception("The API bypassed paper permission");
		Console.WriteLine("PASS local API loopback binding, authentication, Host/Origin checks, schemas, rendering and consent");
	}
	private static StringContent JsonContent(string text) => new(text, Encoding.UTF8, "application/json");
}
