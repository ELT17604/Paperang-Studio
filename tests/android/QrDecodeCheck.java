package com.elt.pprstudio.adc;

import com.google.zxing.BinaryBitmap;
import com.google.zxing.MultiFormatReader;
import com.google.zxing.RGBLuminanceSource;
import com.google.zxing.common.HybridBinarizer;
import java.nio.file.Files;
import java.nio.file.Path;
import javax.imageio.ImageIO;

public final class QrDecodeCheck
{
	public static void main(String[] arguments) throws Exception
	{
		int checked = 0;
		try (var files = Files.list(Path.of(arguments[0])))
		{
			for (var file : files.filter(path -> path.toString().endsWith(".png")).sorted().toList())
			{
				var image = ImageIO.read(file.toFile());
				var source = new RGBLuminanceSource(image.getWidth(), image.getHeight(),
				    image.getRGB(0, 0, image.getWidth(), image.getHeight(), null, 0, image.getWidth()));
				var result = new MultiFormatReader().decode(new BinaryBitmap(new HybridBinarizer(source)));
				var expected = Files.readString(Path.of(file.toString().replace(".png", ".txt")));
				if (!expected.equals(result.getText()))
					throw new AssertionError("Decoded payload differs: " + file);
				checked++;
			}
		}
		if (checked != 18) throw new AssertionError("Expected 18 variants, got " + checked);
		System.out.println("PASS 18 QR variants decoded independently with ZXing");
	}
}
