using System.Text.Json;
using Paperang.Core;

namespace Paperang.WinLite;

internal sealed class QuotaForm : Form
{
	private sealed record Scope(string Id, string Label)
	{
		public override string ToString() => Label;
	}
	private readonly RelayClient client;
	private readonly CancellationTokenSource lifetime;
	private readonly ComboBox target = new() { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Top };
	private readonly Label usage =
	    new() { AutoSize = true, MaximumSize = new Size(550, 0), ForeColor = Color.FromArgb(50, 75, 110) };
	private readonly Label feedback = new() { AutoSize = true, MaximumSize = new Size(550, 0) };
	private readonly Button save =
	    new() { Text = "保存此对象的限额", AutoSize = true, Padding = new Padding(10, 6, 10, 6) };
	private readonly List<(string Field, CheckBox Inherit, NumericUpDown Value)> fields = [];
	private bool busy;

	internal QuotaForm(RelayClient client, string? userId, string? displayName, CancellationToken token)
	{
		SuspendLayout();
		this.client = client;
		lifetime = CancellationTokenSource.CreateLinkedTokenSource(token);
		AutoScaleDimensions = new SizeF(96, 96);
		AutoScaleMode = AutoScaleMode.Dpi;
		Text = "用量与限额";
		ClientSize = new Size(620, 650);
		MinimumSize = new Size(600, 610);
		Font = new Font("Microsoft YaHei UI", 10);
		BackColor = Color.White;
		StartPosition = FormStartPosition.CenterParent;
		var panel = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(24), ColumnCount = 1,
		                                   AutoScroll = true };
		panel.ColumnStyles.Add(new(SizeType.Percent, 100));
		panel.Controls.Add(
		    new Label { Text = "用量与限额", AutoSize = true, Font = new Font(Font.FontFamily, 20, FontStyle.Bold) });
		panel.Controls.Add(new Label { Text = "单个身份优先于分类默认值。勾选继承使用上级设置；设为 0 可禁止投稿。",
		                               AutoSize = true, MaximumSize = new Size(550, 0),
		                               Margin = new Padding(0, 12, 0, 16), ForeColor = Color.DimGray });
		if (!string.IsNullOrEmpty(userId))
			target.Items.Add(new Scope("user:" + userId, "所选身份 · " + displayName));
		target.Items.Add(new Scope("anonymous", "全部匿名身份 · 默认限额（每个身份）"));
		target.Items.Add(new Scope("authenticated", "全部注册身份 · 默认限额（每个身份）"));
		target.SelectedIndex = 0;
		panel.Controls.Add(target);
		panel.Controls.Add(usage);
		foreach (var item in new[] { ("hourly_limit", "每小时任务数", 1000000000),
		                             ("hourly_chars", "每小时内容量（字数 + 图片行数）", 1000000000),
		                             ("max_length", "单次内容量（字数 + 图片行数）", 10000) })
		{
			panel.Controls.Add(new Label { Text = item.Item2, AutoSize = true, Margin = new Padding(0, 14, 0, 4) });
			var row = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true };
			var inherit = new CheckBox { Text = "继承默认值", AutoSize = true, Padding = new Padding(0, 5, 14, 0) };
			var value = new NumericUpDown { Minimum = 0, Maximum = item.Item3, ThousandsSeparator = true, Width = 190 };
			inherit.CheckedChanged += (_, _) => value.Enabled = !inherit.Checked;
			row.Controls.AddRange([inherit, value]);
			panel.Controls.Add(row);
			fields.Add((item.Item1, inherit, value));
		}
		var refresh = new Button { Text = "刷新用量", AutoSize = true, Padding = new Padding(8, 6, 8, 6) };
		var close = new Button { Text = "关闭", AutoSize = true, Padding = new Padding(8, 6, 8, 6),
		                         DialogResult = DialogResult.Cancel };
		var buttons = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Top, Margin = new Padding(0, 20, 0, 8) };
		buttons.Controls.AddRange([save, refresh, close]);
		panel.Controls.Add(buttons);
		panel.Controls.Add(feedback);
		Controls.Add(panel);
		CancelButton = close;
		target.SelectedIndexChanged += async (_, _) => await Run(false);
		save.Click += async (_, _) => await Run(true);
		refresh.Click += async (_, _) => await Run(false);
		Shown += async (_, _) => await Run(false);
		FormClosed += (_, _) =>
		{
			lifetime.Cancel();
			lifetime.Dispose();
		};
		ResumeLayout(true);
	}
	private async Task Run(bool write)
	{
		if (busy)
			return;
		busy = true;
		target.Enabled = save.Enabled = false;
		foreach (var field in fields)
		{
			field.Inherit.Enabled = false;
			field.Value.Enabled = false;
		}
		try
		{
			string scope = ((Scope)target.SelectedItem!).Id;
			var limits = fields.ToDictionary(x => x.Field, x => x.Inherit.Checked ? (int?)null : (int)x.Value.Value);
			var value = await client.Post("v2/admin/quotas/" + (write ? "set" : "get"),
			                              write ? (object) new { scope, limits } : new { scope }, lifetime.Token);
			if (IsDisposed)
				return;
			var effective = value.GetProperty("effective");
			var overrides = value.GetProperty("overrides");
			var current = value.GetProperty("usage");
			foreach (var field in fields)
			{
				field.Inherit.Checked =
				    !overrides.TryGetProperty(field.Field, out var custom) || custom.ValueKind == JsonValueKind.Null;
				field.Value.Value = Math.Clamp(effective.GetProperty(field.Field).GetInt32(), (int)field.Value.Minimum,
				                               (int)field.Value.Maximum);
			}
			var reset = DateTimeOffset.UnixEpoch.AddSeconds(value.GetProperty("resetAt").GetDouble()).ToLocalTime();
			usage.Text =
			    $"本小时{(current.GetProperty("aggregate").GetBoolean() ? "此类身份累计" : "此身份")}：{current.GetProperty("jobs")} 个任务 / {current.GetProperty("units")} 内容量\n重置时间：{reset:HH:mm}；分类额度分别应用到每个身份。";
			var ceilings = value.GetProperty("ceilings");
			feedback.Text =
			    (write ? "已保存。新投稿立即使用；不清空已累计用量。\n" : "") +
			    $"另受 IP 与全站上限约束：本类 IP 每小时 {ceilings.GetProperty("ip_hourly_limit")} 任务 / {ceilings.GetProperty("ip_hourly_chars")} 内容量。图片每行计一个内容单位。";
			save.Tag = true;
		}
		catch (OperationCanceledException)
		{
		}
		catch (RelayHttpException e)
		{
			if (!IsDisposed)
			{
				save.Tag = false;
				feedback.Text =
				    e.Status == 404 ? "当前中转服务器尚未提供限额管理接口，请先部署配套服务器更新。" : e.Message;
			}
		}
		catch
		{
			if (!IsDisposed)
			{
				save.Tag = false;
				feedback.Text = "读取或保存失败，请刷新后重试。";
			}
		}
		finally
		{
			busy = false;
			if (!IsDisposed)
			{
				target.Enabled = true;
				save.Enabled = save.Tag is true;
				foreach (var field in fields)
				{
					field.Inherit.Enabled = save.Enabled;
					field.Value.Enabled = save.Enabled && !field.Inherit.Checked;
				}
			}
		}
	}
}
