using Microsoft.UI.Xaml;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;

namespace Paperang.WinClient;

internal sealed partial class WorkspaceWindow
{
	private void InitializeDrop()
	{
		document.AllowDrop = true;
		document.DragOver += (_, args) =>
		{
			if (operation == null && args.DataView.Contains(StandardDataFormats.StorageItems))
			{
				args.AcceptedOperation = DataPackageOperation.Copy;
				args.DragUIOverride.Caption = "导入图片、点阵或参数预设 JSON";
			}
		};
		document.Drop += async (_, args) =>
		{
			var deferral = args.GetDeferral();
			try
			{
				if (operation != null) throw new InvalidOperationException("请先等待或取消当前操作。");
				using var cancellation = new CancellationTokenSource();
				operation = cancellation; cancel.IsEnabled = true;
				try
				{
					var items = await args.DataView.GetStorageItemsAsync();
					if (items.Count != 1 || items[0] is not StorageFile file)
						throw new InvalidDataException("请一次拖入一个图片或点阵文件。");
					await ImportPath(file.Path, file.Name, cancellation.Token);
				}
				finally { operation = null; cancel.IsEnabled = false; }
			}
			catch (OperationCanceledException) { Report("已取消导入。"); }
			catch (Exception error) { Report(error.Message, Microsoft.UI.Xaml.Controls.InfoBarSeverity.Error); }
			finally { deferral.Complete(); }
		};
	}
}
