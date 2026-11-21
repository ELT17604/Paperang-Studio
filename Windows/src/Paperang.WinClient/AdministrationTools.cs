using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using System.Text.Json;
using Microsoft.UI.Xaml.Controls;
using Paperang.Core;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace Paperang.WinClient;

internal sealed partial class WorkspaceWindow
{
	private void BuildAdministrationTools()
	{
		var scope = Choice("限额对象", "anonymous", "authenticated", "user");
		var scopeUser = new TextBox { Header = "身份 ID（选择 user 时必填）", MaxLength = 128 };
		var hourlyJobs = new TextBox { Header = "每小时任务数（空白继承）", MaxLength = 10 };
		var hourlyUnits = new TextBox { Header = "每小时内容量（空白继承）", MaxLength = 10 };
		var maxLength = new TextBox { Header = "单次内容量（空白继承）", MaxLength = 10 };
		var usage = Paragraph("读取后显示当前用量。");
		var quotaPanel = new StackPanel { Spacing = 10 };
		foreach (var field in new Microsoft.UI.Xaml.FrameworkElement[] { scope, scopeUser, hourlyJobs, hourlyUnits, maxLength, usage })
			quotaPanel.Children.Add(field);
		quotaPanel.Children.Add(ActionButton("读取用量与限额", token => Quotas(false, token)));
		quotaPanel.Children.Add(ActionButton("保存限额…", token => Quotas(true, token)));
		administrationPage.Children.Add(PropertyGroup("用量与限额", quotaPanel));
		var keyPanel = new StackPanel { Spacing = 10 };
		keyPanel.Children.Add(Paragraph("登记公钥不会传送私钥；生成的新所有者文件只允许当前 Windows 用户读取。"));
		keyPanel.Children.Add(ActionButton("登记已有公钥…", async token =>
		{
			var client = AdminClient();
			var picker = new FileOpenPicker();
			picker.FileTypeFilter.Add(".pem");
			InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(this));
			var file = await picker.PickSingleFileAsync();
			if (file == null) return;
			if (new FileInfo(file.Path).Length > 16384) throw new InvalidDataException("公钥文件过大。");
			using var rsa = RSA.Create();
			rsa.ImportFromPem(await File.ReadAllTextAsync(file.Path, token));
			string name = adminName.Text.Trim();
			if (name.Length == 0) throw new InvalidDataException("请先填写展示名称。");
			if (!await Confirm(administrationPage, "登记这个公钥？", name)) return;
			var result = await client.Post("v2/admin/public-keys", new { name, public_key = rsa.ExportSubjectPublicKeyInfoPem() }, token);
			adminDetail.Text = JsonSerializer.Serialize(result, RelayClient.Json);
		}));
		keyPanel.Children.Add(ActionButton("生成并配对所有者私钥…", GenerateOwnerKey));
		administrationPage.Children.Add(PropertyGroup("所有者公钥", keyPanel));

		async Task Quotas(bool save, CancellationToken token)
		{
			string target = (string)scope.SelectedItem;
			if (target == "user")
			{
				if (string.IsNullOrWhiteSpace(scopeUser.Text)) throw new InvalidDataException("请输入身份 ID。");
				target = "user:" + scopeUser.Text.Trim();
			}
			var limits = new Dictionary<string, int?>
			{
				["hourly_limit"] = ParseLimit(hourlyJobs.Text, 1_000_000_000),
				["hourly_chars"] = ParseLimit(hourlyUnits.Text, 1_000_000_000),
				["max_length"] = ParseLimit(maxLength.Text, 10000)
			};
			if (save && !await Confirm(administrationPage, "保存此对象的限额？", target + "；空白值使用继承设置")) return;
			object query = save ? new { scope = target, limits } : new { scope = target };
			var result = await AdminClient().Post("v2/admin/quotas/" + (save ? "set" : "get"), query, token);
			var overrides = result.GetProperty("overrides");
			hourlyJobs.Text = Custom("hourly_limit");
			hourlyUnits.Text = Custom("hourly_chars");
			maxLength.Text = Custom("max_length");
			var current = result.GetProperty("usage");
			var effective = result.GetProperty("effective");
			usage.Text = $"本小时：{current.GetProperty("jobs")} 个任务 / {current.GetProperty("units")} 内容量\n" +
				$"生效限额：{effective.GetProperty("hourly_limit")} 个任务 / {effective.GetProperty("hourly_chars")} 内容量；单次 {effective.GetProperty("max_length")}";
			string Custom(string property) => overrides.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.Number ? value.ToString() : "";
		}
	}
	private static int? ParseLimit(string text, int maximum)
	{
		if (string.IsNullOrWhiteSpace(text)) return null;
		if (!int.TryParse(text, out int value) || value < 0 || value > maximum)
			throw new InvalidDataException($"限额须为空白或 0–{maximum} 的整数。");
		return value;
	}
	private async Task GenerateOwnerKey(CancellationToken token)
	{
		var client = AdminClient();
		string name = adminName.Text.Trim();
		if (name.Length == 0) throw new InvalidDataException("请先填写展示名称。");
		var picker = new FileSavePicker { SuggestedFileName = "paperang-owner" };
		picker.FileTypeChoices.Add("所有者文件", new List<string> { ".json" });
		InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(this));
		var target = await picker.PickSaveFileAsync();
		if (target == null) return;
		if (new FileInfo(target.Path).Length != 0) throw new InvalidDataException("请选择新的空文件，避免覆盖已有私钥。");
		if (!await Confirm(administrationPage, "生成并登记新的所有者私钥？", name)) return;
		using var rsa = RSA.Create(3072);
		string privateKey = rsa.ExportPkcs8PrivateKeyPem(), publicKey = rsa.ExportSubjectPublicKeyInfoPem();
		string server = adminSessionServer ?? throw new InvalidOperationException("请重新登录管理服务器后再配对。");
		string pending = target.Path + ".pending-" + Guid.NewGuid().ToString("N");
		WriteOwnerFile(pending, JsonSerializer.Serialize(new { server, id = "", privateKeyPem = privateKey }, RelayClient.Json));
		try
		{
			var result = await client.Post("v2/admin/public-keys", new { name, public_key = publicKey }, token);
			string id = result.GetProperty("id").GetString() ?? throw new InvalidDataException("服务器未返回公钥 ID。");
			if (!Guid.TryParse(id, out _)) throw new InvalidDataException("公钥 ID 无效。");
			string completed = pending + ".paired";
			WriteOwnerFile(completed, JsonSerializer.Serialize(new { server, id, privateKeyPem = privateKey }, RelayClient.Json));
			File.Move(completed, target.Path, true);
			File.Delete(pending);
			Report("所有者文件已生成并配对。请在连接设置中选择该文件并验证登录。", InfoBarSeverity.Success);
		}
		catch (Exception error)
		{
			throw new InvalidOperationException("配对未完成或回执不确定。私钥备份保留于：" + pending + "；请先核对服务器登记结果。", error);
		}
	}
	private static void WriteOwnerFile(string path, string value)
	{
		var owner = WindowsIdentity.GetCurrent().User ?? throw new InvalidOperationException("无法识别 Windows 用户。");
		var acl = new FileSecurity();
		acl.SetOwner(owner);
		acl.SetAccessRuleProtection(true, false);
		acl.AddAccessRule(new FileSystemAccessRule(owner, FileSystemRights.FullControl, AccessControlType.Allow));
		using var output = new FileInfo(path).Create(FileMode.CreateNew, FileSystemRights.FullControl, FileShare.None, 4096, FileOptions.WriteThrough, acl);
		output.Write(Encoding.UTF8.GetBytes(value));
		output.Flush(true);
	}
}
