using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace IconGenerator;

class Program
{
    static void Main(string[] args)
    {
        string outputPath = args.Length > 0
            ? args[0]
            : Path.Combine(Directory.GetCurrentDirectory(), "..", "AgentHost", "olmez.ico");

        Console.WriteLine("olmez - Professional Icon Generator");
        Console.WriteLine("====================================");
        Console.WriteLine();

        // Create multi-resolution icon (256x256, 128x128, 64x64, 48x48, 32x32, 16x16)
        var sizes = new[] { 256, 128, 64, 48, 32, 16 };
        using var iconStream = new MemoryStream();

        // Write ICO header
        WriteIconHeader(iconStream, sizes.Length);

        var imageDataList = new List<(int size, byte[] data, int offset)>();
        int currentOffset = 6 + (16 * sizes.Length); // Header + directory entries

        // Generate images for each size
        foreach (var size in sizes)
        {
            Console.WriteLine($"Generating {size}x{size} icon...");
            var imageData = GenerateIconImage(size);
            imageDataList.Add((size, imageData, currentOffset));
            currentOffset += imageData.Length;
        }

        // Write directory entries
        foreach (var (size, data, offset) in imageDataList)
        {
            WriteIconDirectoryEntry(iconStream, size, data.Length, offset);
        }

        // Write image data
        foreach (var (_, data, _) in imageDataList)
        {
            iconStream.Write(data, 0, data.Length);
        }

        // Save to file
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        File.WriteAllBytes(outputPath, iconStream.ToArray());

        Console.WriteLine();
        Console.WriteLine($"✓ Icon successfully created: {outputPath}");
        Console.WriteLine($"  File size: {new FileInfo(outputPath).Length:N0} bytes");
    }

    static void WriteIconHeader(Stream stream, int imageCount)
    {
        var writer = new BinaryWriter(stream);
        writer.Write((short)0);        // Reserved (must be 0)
        writer.Write((short)1);        // Type (1 = ICO)
        writer.Write((short)imageCount); // Number of images
    }

    static void WriteIconDirectoryEntry(Stream stream, int size, int dataLength, int offset)
    {
        var writer = new BinaryWriter(stream);
        writer.Write((byte)(size >= 256 ? 0 : size)); // Width
        writer.Write((byte)(size >= 256 ? 0 : size)); // Height
        writer.Write((byte)0);         // Color palette (0 for PNG)
        writer.Write((byte)0);         // Reserved
        writer.Write((short)1);        // Color planes
        writer.Write((short)32);       // Bits per pixel
        writer.Write(dataLength);      // Size of image data
        writer.Write(offset);          // Offset to image data
    }

    static byte[] GenerateIconImage(int size)
    {
        using var bitmap = new Bitmap(size, size, PixelFormat.Format32bppArgb);
        using var graphics = Graphics.FromImage(bitmap);

        // High quality rendering
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
        graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
        graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;

        // Clear with transparency
        graphics.Clear(Color.Transparent);

        // Create gradient background circle
        var rect = new Rectangle(0, 0, size, size);
        var padding = size / 16;
        var circleRect = new Rectangle(padding, padding, size - padding * 2, size - padding * 2);

        using (var gradientBrush = new LinearGradientBrush(
            circleRect,
            Color.FromArgb(255, 30, 136, 229),  // Modern blue
            Color.FromArgb(255, 13, 71, 161),   // Dark blue
            LinearGradientMode.ForwardDiagonal))
        {
            // Add smooth gradient
            var blend = new ColorBlend(3);
            blend.Colors = new[] {
                Color.FromArgb(255, 30, 136, 229),
                Color.FromArgb(255, 21, 101, 192),
                Color.FromArgb(255, 13, 71, 161)
            };
            blend.Positions = new[] { 0f, 0.5f, 1f };
            gradientBrush.InterpolationColors = blend;

            graphics.FillEllipse(gradientBrush, circleRect);
        }

        // Add subtle shadow/border
        using (var borderPen = new Pen(Color.FromArgb(100, 0, 0, 0), size / 64f))
        {
            var borderRect = circleRect;
            borderRect.Inflate(-1, -1);
            graphics.DrawEllipse(borderPen, borderRect);
        }

        // Draw "Ö" letter
        var fontSize = size * 0.55f;
        using (var font = new Font("Segoe UI", fontSize, FontStyle.Bold, GraphicsUnit.Pixel))
        {
            var text = "Ö";
            var textSize = graphics.MeasureString(text, font);

            // Draw shadow for depth
            using (var shadowBrush = new SolidBrush(Color.FromArgb(60, 0, 0, 0)))
            {
                var shadowX = (size - textSize.Width) / 2 + size / 64f;
                var shadowY = (size - textSize.Height) / 2 + size / 64f;
                graphics.DrawString(text, font, shadowBrush, shadowX, shadowY);
            }

            // Draw main text
            using (var textBrush = new SolidBrush(Color.White))
            {
                var textX = (size - textSize.Width) / 2;
                var textY = (size - textSize.Height) / 2;
                graphics.DrawString(text, font, textBrush, textX, textY);
            }
        }

        // Convert to PNG
        using var ms = new MemoryStream();
        bitmap.Save(ms, ImageFormat.Png);
        return ms.ToArray();
    }
}
