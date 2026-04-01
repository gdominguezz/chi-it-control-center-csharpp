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
        _qrDir = config["AppSettings:QrDir"] ?? "QR_CODES/MESAS";
        Directory.CreateDirectory(_qrDir);
    }

    // ── Helper: obtener fuente compatible con Linux/Render ───────────────
    private static Font? ObtenerFuente(float size)
    {
        string[] candidatas = { "Arial", "Liberation Sans", "DejaVu Sans", "FreeSans", "Noto Sans", "Ubuntu" };
        foreach (var nombre in candidatas)
            if (SystemFonts.TryGet(nombre, out var f))
                return f.CreateFont(size, FontStyle.Regular);

        var todas = SystemFonts.Families.ToList();
        return todas.Count > 0 ? todas[0].CreateFont(size, FontStyle.Regular) : null;
    }

    // ── Motor base: genera PNG con QR + etiqueta de texto ────────────────
    private static byte[] GenerarImagenQr(string contenido, string etiqueta)
    {
        using var qrGen = new QRCodeGenerator();
        using var qrData = qrGen.CreateQrCode(contenido, QRCodeGenerator.ECCLevel.M);
        using var qrCode = new PngByteQRCode(qrData);
        var qrBytes = qrCode.GetGraphic(10); // 10 px por módulo ≈ 330 px

        using var qrImg = Image.Load<Rgba32>(qrBytes);
        int qrW = qrImg.Width;
        int qrH = qrImg.Height;
        int totalH = qrH + 60;

        using var final = new Image<Rgba32>(qrW, totalH, Color.White);
        final.Mutate(ctx =>
        {
            ctx.DrawImage(qrImg, new Point(0, 0), 1f);

            var font = ObtenerFuente(18);
            if (font == null) return; // sin fuente disponible → solo el QR

            var opts = new RichTextOptions(font)
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                Origin = new System.Numerics.Vector2(qrW / 2f, qrH + 15)
            };
            ctx.DrawText(opts, etiqueta, Color.Black);
        });

        using var ms = new MemoryStream();
        final.SaveAsPng(ms);
        return ms.ToArray();
    }

    // ── QR por UBICACIÓN (comportamiento original) ────────────────────────
    /// <summary>
    /// QR que apunta a /preventivos/qr/{ubicacion}.
    /// Etiqueta: nombre de la ubicación.
    /// </summary>
    public byte[] Generar(string ubicacion)
    {
        var url = $"{_baseUrl}/preventivos/qr/{Uri.EscapeDataString(ubicacion)}";
        return GenerarImagenQr(url, ubicacion);
    }

    // ── QR por EQUIPO (nuevo) ─────────────────────────────────────────────
    /// <summary>
    /// QR que codifica el ID de equipo como texto plano.
    /// Etiqueta: "ID: {idEquipo}".
    /// Al escanear, el lector obtiene directamente el valor del ID.
    /// </summary>
    public byte[] GenerarPorEquipo(string idEquipo)
    {
        // Contenido del QR = el ID del equipo (texto plano, fácil de leer con cualquier scanner)
        return GenerarImagenQr(idEquipo, $"ID: {idEquipo}");
    }
}