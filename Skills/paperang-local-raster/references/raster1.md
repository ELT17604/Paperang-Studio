# MacClient raster1 import contract

Use one UTF-8 JSON object per sheet. Its fields are:

| Field | Required value |
| --- | --- |
| type | "raster1" |
| schemaVersion | 1 |
| width | 576 |
| height | Integer from 1 to 1400 |
| encoding | "base64" |
| bitOrder | "msb-first" |
| blackIsOne | true |
| data | Standard padded Base64 of the raw bitmap, without spaces or line breaks |
| sha256 | Lowercase hexadecimal SHA-256 of the decoded bytes |

The first row is the top row. Each row is exactly 72 bytes, scanned left to right with no row padding. Pixel x on row y is at byte offset `72*y + x//8`, mask `0x80 >> (x%8)`. Set the bit for black ink. The decoded length is exactly `72*height` and at most 100800 bytes.

For a one-row orientation test, black pixels at x=0, 7, 8 and 575 yield first bytes `81 80`, then 69 zero bytes, then `01` (hexadecimal). Decode the file and compare the preview with the intended layout before delivery.

The updated MacClient also accepts the workbench's existing .raw export. It contains only the packed bytes with no header or checksum. The MacClient infers height from file length. Prefer JSON for generated files so accidental data corruption and incompatible dimensions can be detected. The JSON contains the raster object itself, without a Relay job wrapper. Import does not resize, apply the workbench's image filters, or change print density, paper feed or copy settings.
