---
name: paperang-local-raster
description: Generate a verified 576-dot monochrome raster1 JSON file for manual import into Paperang Studio MacClient. Use for printable text, graphics, labels, QR codes or layouts the user wants as a local bitmap file.
---

# Generate a local Paperang print bitmap

1. Turn the user's content into a 576-pixel-wide image, 1–1400 pixels high. Render actual glyphs with a font that covers the requested language. Use a QR encoder for codes and leave at least four light modules of quiet space. Split long layouts into separate files at sensible breaks and keep their order.
2. Use code to render and pack the pixels. Do not compose Base64 or SHA-256 in prose. Given a PNG at the exact intended size, run the bundled helper with Python 3.10+ and Pillow installed in that environment:

   ```sh
   python scripts/pack_raster.py design.png ticket.raster1.json --preview ticket-preview.png
   ```

   The helper composites transparency onto white, applies a grayscale threshold and reconstructs the preview from packed bytes. For photos, prepare a dithered PNG first. For text and QR codes, inspect the final black-and-white preview at 1:1; decode the final preview to verify QR payloads when a decoder is available.
3. Give the user the JSON file and preview. In the updated MacClient, choose **打印工作台 → 导入位图…**, select the JSON, inspect the preview, then choose the existing Print button when ready. This skill creates files locally.

For another programming environment, implement and verify the [byte contract](references/raster1.md). If the current model cannot execute code or produce files, return a reproducible script or a PNG design for the user to pack, and identify which artifact has yet to be produced.
