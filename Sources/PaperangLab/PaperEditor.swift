import SwiftUI
import UniformTypeIdentifiers
import PrintCore

struct SystemFontPicker: View
{
	@Binding var selection: String
	private final class Panel: ObservableObject
	{
		@Published var showing = false
		@Published var query = ""
	}
	@StateObject private var panel = Panel()
	private static let common = ["PingFang SC", "Menlo", "Helvetica", "Times New Roman"]
	private static let fonts = NSFontManager.shared.availableFonts.sorted
	{
		$0.localizedStandardCompare($1) == .orderedAscending
	}

	var body: some View
	{
		LabeledContent("字体")
		{
			Button
			{
				panel.query = ""
				panel.showing.toggle()
			} label:
			{
				Label(selection, systemImage: "chevron.up.chevron.down")
					.lineLimit(1)
			}
			.popover(isPresented: $panel.showing)
			{
				VStack(alignment: .leading, spacing: 10)
				{
					TextField("搜索系统字体", text: $panel.query)
						.textFieldStyle(.roundedBorder)
					ScrollView
					{
						LazyVStack(alignment: .leading, spacing: 2)
						{
							Text("常用字体").font(.headline)
							ForEach(filtered(Self.common), id: \.self, content: row)
							Divider().padding(.vertical, 6)
							Text("全部系统字体").font(.headline)
							ForEach(filtered(Self.fonts), id: \.self, content: row)
						}.padding(4)
					}
				}.padding(12).frame(width: 310, height: 340)
			}
		}
	}

	private func filtered(_ fonts: [String]) -> [String]
	{
		panel.query.isEmpty ? fonts : fonts.filter
		{
			$0.localizedCaseInsensitiveContains(panel.query)
		}
	}

	private func row(_ font: String) -> some View
	{
		Button
		{
			selection = font
			panel.showing = false
		} label:
		{
			HStack
			{
				Text(font).font(.custom(font, size: 13))
				Spacer()
				if font == selection
				{
					Image(systemName: "checkmark")
				}
			}.padding(4).contentShape(Rectangle())
		}.buttonStyle(.plain)
	}
}

struct PaperEditor: View
{
	@Environment(\.dismiss) private var dismiss
	private final class Session: ObservableObject
	{
		@Published var draft: PaperComposition
		@Published var selected: UUID?
		@Published var error = ""
		var dragOrigin: CGPoint?

		init(draft: PaperComposition)
		{
			self.draft = draft
			selected = draft.layers.last?.id
		}
	}
	@StateObject private var session: Session
	let confirm: (PaperComposition) throws -> Void
	init(draft: PaperComposition, confirm: @escaping (PaperComposition) throws -> Void)
	{
		_session = StateObject(wrappedValue: Session(draft: draft))
		self.confirm = confirm
	}

	private let scale: CGFloat = 0.78
	private static let kinds: [PaperLayer.Kind] = [.text, .label, .photo, .qr]
	private static let dropPrefix = "paperang-studio-layer:"

	var body: some View
	{
		VStack(alignment: .leading, spacing: 12)
		{
			HStack
			{
				Text("高级排版").font(.title2.bold())
				Spacer()
				Stepper("纸长 \(session.draft.height) 点", value: $session.draft.height, in: 120...1400, step: 20)
					.fixedSize()
			}
			HStack
			{
				ForEach(Self.kinds, id: \.self)
				{
					kind in
					Button(kind.rawValue)
					{
						insert(kind)
					}
						.onDrag
						{
							NSItemProvider(object: (Self.dropPrefix + kind.rawValue) as NSString)
						}
				}
				Spacer()
				Text("拖入对象或 Finder 图片，拖动纸面对象调整位置").font(.caption)
			}
			HStack(alignment: .top, spacing: 16)
			{
				ScrollView([.vertical, .horizontal])
				{
					paper.padding(12)
				}
					.background(.quaternary.opacity(0.4), in: RoundedRectangle(cornerRadius: 8))
				ScrollView
				{
					if let index = session.draft.layers.firstIndex(where:
					{
						$0.id == session.selected
					})
					{
						inspector($session.draft.layers[index]).id(session.draft.layers[index].id)
					} else
					{
						Text("选择纸上的对象以调整格式、位置和尺寸。")
							.foregroundStyle(.secondary).frame(maxWidth: .infinity, alignment: .leading)
					}
				}.frame(width: 290)
			}
			if !session.error.isEmpty
			{
				Text(session.error).foregroundStyle(.red).textSelection(.enabled)
			}
			HStack
			{
				Text("576 点宽 · 对象按纸面所见顺序叠放").foregroundStyle(.secondary)
				Spacer()
				Button("取消")
				{
					dismiss()
				}.keyboardShortcut(.cancelAction)
				Button("确认并渲染")
				{
					do
					{
						try confirm(session.draft)
						dismiss()
					} catch
					{
						session.error = error.localizedDescription
					}
				}.buttonStyle(.borderedProminent)
			}
		}
		.padding(20).frame(width: 830, height: 640)
		.onChange(of: session.draft.height)
		{
			_, _ in
			for index in session.draft.layers.indices
			{
				session.draft.layers[index].frame = bounded(session.draft.layers[index].frame)
			}
		}
	}

	private var paper: some View
	{
		ZStack(alignment: .topLeading)
		{
			Color.white.onTapGesture
			{
				session.selected = nil
			}
			if let bitmap = try? session.draft.bitmap(strict: false), let cgImage = bitmap.cgImage
			{
				Image(decorative: cgImage, scale: 1).resizable().interpolation(.none).allowsHitTesting(false)
			}
			ForEach(session.draft.layers)
			{
				layer in
				Rectangle()
					.strokeBorder(
						session.selected == layer.id ? Color.accentColor : .gray.opacity(0.5),
						style: StrokeStyle(lineWidth: session.selected == layer.id ? 2 : 1, dash: [5, 3])
					)
					.background(.clear)
					.contentShape(Rectangle())
					.frame(width: layer.frame.width * scale, height: layer.frame.height * scale)
					.offset(x: layer.frame.minX * scale, y: layer.frame.minY * scale)
					.onTapGesture
					{
						session.selected = layer.id
					}
					.gesture(
						DragGesture(coordinateSpace: .named("paper")).onChanged
						{
							drag in
							guard let index = session.draft.layers.firstIndex(where:
							{
								$0.id == layer.id
							}) else
							{
								return
							}
							var frame = session.draft.layers[index].frame
							if session.dragOrigin == nil
							{
								session.dragOrigin = frame.origin
							}
							guard let origin = session.dragOrigin else
							{
								return
							}
							session.selected = layer.id
							frame.origin = CGPoint(
								x: origin.x + drag.translation.width / scale,
								y: origin.y + drag.translation.height / scale)
							session.draft.layers[index].frame = bounded(frame)
						}.onEnded
						{ _ in session.dragOrigin = nil }
					)
					.accessibilityLabel(layer.kind.rawValue)
					.accessibilityAddTraits(session.selected == layer.id ? .isSelected : [])
			}
		}
		.frame(width: PaperComposition.width * scale, height: CGFloat(session.draft.height) * scale)
		.coordinateSpace(name: "paper")
		.onDrop(of: [.fileURL, .plainText], isTargeted: nil, perform: receive)
	}

	private func receive(_ providers: [NSItemProvider], at point: CGPoint) -> Bool
	{
		guard let provider = providers.first else
		{
			return false
		}
		if provider.hasItemConformingToTypeIdentifier(UTType.fileURL.identifier)
		{
			_ = provider.loadObject(ofClass: URL.self)
			{
				url, _ in
				guard let url else
				{
					return
				}
				DispatchQueue.main.async
				{
					insert(.photo, url: url, at: point)
				}
			}
			return true
		}
		guard provider.canLoadObject(ofClass: NSString.self) else
		{
			return false
		}
		_ = provider.loadObject(ofClass: NSString.self)
		{
			item, _ in
			guard let value = item as? String, value.hasPrefix(Self.dropPrefix),
				let kind = PaperLayer.Kind(rawValue: String(value.dropFirst(Self.dropPrefix.count)))
			else
			{
				return
			}
			DispatchQueue.main.async
			{
				insert(kind, at: point)
			}
		}
		return true
	}

	private func insert(_ kind: PaperLayer.Kind, url: URL? = nil, at point: CGPoint? = nil)
	{
		guard session.draft.layers.count < 24 else
		{
			session.error = "最多添加 24 个对象"
			return
		}
		var image: CGImage?
		if kind == .photo
		{
			let source: URL
			if let url
			{
				source = url
			} else
			{
				let panel = NSOpenPanel()
				panel.allowedContentTypes = [.image]
				guard panel.runModal() == .OK, let url = panel.url else
				{
					return
				}
				source = url
			}
			do
			{
				image = try Renderer.loadImage(source)
			} catch
			{
				session.error = error.localizedDescription
				return
			}
		}
		var layer = PaperLayer(kind, image: image, y: (session.draft.layers.last?.frame.maxY ?? 0) + 16)
		if let point
		{
			layer.frame.origin = CGPoint(x: point.x / scale, y: point.y / scale)
		}
		layer.frame = bounded(layer.frame)
		session.draft.layers.append(layer)
		session.selected = layer.id
		session.error = ""
	}

	private func bounded(_ rect: CGRect) -> CGRect
	{
		let width = min(max(rect.width.rounded(), 24), PaperComposition.width)
		let height = min(max(rect.height.rounded(), 24), CGFloat(session.draft.height))
		return CGRect(
			x: min(max(rect.minX.rounded(), 0), PaperComposition.width - width),
			y: min(max(rect.minY.rounded(), 0), CGFloat(session.draft.height) - height),
			width: width, height: height)
	}

	private func number(
		_ title: String, _ frame: Binding<CGRect>, _ key: WritableKeyPath<CGRect, CGFloat>,
		range: ClosedRange<Int>
	) -> some View
	{
		Stepper(
			"\(title) \(Int(frame.wrappedValue[keyPath: key]))",
			value: Binding(
				get:
				{
					Int(frame.wrappedValue[keyPath: key])
				},
				set:
				{
					value in
					var rect = frame.wrappedValue
					rect[keyPath: key] = CGFloat(value)
					frame.wrappedValue = bounded(rect)
				}), in: range)
	}

	private func align(_ alignment: NSTextAlignment, text: Binding<NSAttributedString>)
	{
		let copy = NSMutableAttributedString(attributedString: text.wrappedValue)
		let range = NSRange(location: 0, length: copy.length)
		text.wrappedValue.enumerateAttribute(.paragraphStyle, in: range)
		{
			value, run, _ in
			let style =
				(value as? NSParagraphStyle)?.mutableCopy() as? NSMutableParagraphStyle ?? NSMutableParagraphStyle()
			style.alignment = alignment
			copy.addAttribute(.paragraphStyle, value: style, range: run)
		}
		text.wrappedValue = NSAttributedString(attributedString: copy)
	}

	private func inspector(_ layer: Binding<PaperLayer>) -> some View
	{
		VStack(alignment: .leading, spacing: 10)
		{
			Text(layer.wrappedValue.kind.rawValue).font(.headline)
			number("左", layer.frame, \.origin.x, range: 0...576)
			number("上", layer.frame, \.origin.y, range: 0...session.draft.height)
			number("宽", layer.frame, \.size.width, range: 24...576)
			number("高", layer.frame, \.size.height, range: 24...session.draft.height)
			Divider()
			switch layer.wrappedValue.kind
			{
			case .qr:
				Picker("内容格式", selection: layer.qr.format)
				{
					ForEach(PaperQR.Format.allCases, id: \.self)
					{
						Text($0.rawValue).tag($0)
					}
				}
				TextEditor(text: layer.qr.content).frame(height: 65)
					.overlay(RoundedRectangle(cornerRadius: 4).stroke(.quaternary))
					.accessibilityLabel("二维码内容")
				Picker("模块形态", selection: layer.qr.shape)
				{
					ForEach(PaperQR.Shape.allCases, id: \.self)
					{
						Text($0.rawValue).tag($0)
					}
				}
				Picker("纠错级别", selection: layer.qr.correction)
				{
					ForEach(["L", "M", "Q", "H"], id: \.self)
					{
						Text($0).tag($0)
					}
				}
				Toggle("加外框", isOn: layer.qr.border)
			case .text, .label:
				RichPaperText(text: layer.text).frame(height: 140)
					.overlay(RoundedRectangle(cornerRadius: 4).stroke(.quaternary))
				Button("字体与样式…")
				{
					NSFontManager.shared.orderFrontFontPanel(nil)
				}
				HStack
				{
					Button("左对齐")
					{
						align(.left, text: layer.text)
					}
					Button("居中")
					{
						align(.center, text: layer.text)
					}
					Button("右对齐")
					{
						align(.right, text: layer.text)
					}
				}
				Text("在编辑框中选中单字或一段文字，再打开字体与样式。对齐作用于整个文字框。")
					.font(.caption).foregroundStyle(.secondary)
			case .photo:
				Text("图片按原比例适配对象边框").font(.caption).foregroundStyle(.secondary)
			}
			Divider()
			HStack
			{
				Button("移到最上层")
				{
					if let index = session.draft.layers.firstIndex(where:
					{
						$0.id == session.selected
					})
					{
						let moved = session.draft.layers.remove(at: index)
						session.draft.layers.append(moved)
					}
				}
				Button("删除", role: .destructive)
				{
					session.draft.layers.removeAll
					{
						$0.id == session.selected
					}
					session.selected = nil
				}
			}
		}.padding(.trailing, 4)
	}
}

private struct RichPaperText: NSViewRepresentable
{
	@Binding var text: NSAttributedString

	func makeCoordinator() -> Coordinator
	{
		Coordinator(self)
	}

	func makeNSView(context: Context) -> NSScrollView
	{
		let scroll = NSTextView.scrollableTextView()
		if let editor = scroll.documentView as? NSTextView
		{
			editor.isRichText = true
			editor.importsGraphics = false
			editor.usesFontPanel = true
			editor.allowsUndo = true
			editor.delegate = context.coordinator
			editor.textStorage?.setAttributedString(text)
			editor.backgroundColor = .white
			editor.insertionPointColor = .black
		}
		return scroll
	}

	func updateNSView(_ view: NSScrollView, context: Context)
	{
		context.coordinator.owner = self
		if let editor = view.documentView as? NSTextView, !editor.attributedString().isEqual(to: text)
		{
			editor.textStorage?.setAttributedString(text)
		}
	}

	final class Coordinator: NSObject, NSTextViewDelegate
	{
		var owner: RichPaperText
		init(_ owner: RichPaperText)
		{
			self.owner = owner
		}

		func textDidChange(_ notification: Notification)
		{
			guard let editor = notification.object as? NSTextView else
			{
				return
			}
			owner.text = NSAttributedString(attributedString: editor.attributedString())
		}
	}
}
