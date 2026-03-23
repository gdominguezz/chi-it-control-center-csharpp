using QRCoder;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.Fonts;

namespace ChiIT.Services;

public class QrService
{
    private readonly string _baseUrl;
    private readonly string _qrDir;

    public QrService(IConfiguration config)
    {
        _baseUrl = config["AppSettings:ServerBaseUrl"] ?? "http://172.24.104.1:8000";
        _qrDir   = config["AppSettings:QrDir"]        ?? "QR_CODES/MESAS";
        Directory.CreateDirectory(_qrDir);
    }

    /// <summary>
    /// Genera la imagen QR con texto de ubicación abajo y la devuelve como bytes PNG.
    /// </summary>
    public byte[] Generar(string ubicacion)
    {
        var url = $"{_baseUrl}/preventivos/qr/{Uri.EscapeDataString(ubicacion)}";

        // Generar QR
        using var qrGen = new QRCodeGenerator();
        using var qrData = qrGen.CreateQrCode(url, QRCodeGenerator.ECCLevel.M);
        using var qrCode = new PngByteQRCode(qrData);
        var qrBytes = qrCode.GetGraphic(10); // 10px por módulo → ~330px

        // Cargar QR como ImageSharp
        using var qrImg = Image.Load<Rgba32>(qrBytes);
        int qrW = qrImg.Width;
        int qrH = qrImg.Height;
        int totalH = qrH + 60;

        // Imagen final con espacio para el texto
        using var final = new Image<Rgba32>(qrW, totalH, Color.White);
        final.Mutate(ctx =>
        {
            ctx.DrawImage(qrImg, new Point(0, 0), 1f);

            // Texto centrado
            var font = SystemFonts.CreateFont("Arial", 20, FontStyle.Regular);
            var opts = new RichTextOptions(font)
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                Origin = new System.Numerics.Vector2(qrW / 2f, qrH + 15)
            };
            ctx.DrawText(opts, ubicacion, Color.Black);
        });

        using var ms = new MemoryStream();
        final.SaveAsPng(ms);
        return ms.ToArray();
    }
}
