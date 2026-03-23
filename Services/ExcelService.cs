using ClosedXML.Excel;
using Npgsql;
using System.Data;

namespace ChiIT.Services;

public class ExcelService
{
    // Mapa de color a hex para celdas
    private static readonly Dictionary<string, XLColor> ColorMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["verde"]    = XLColor.FromHtml("#BBF7D0"),
        ["amarillo"] = XLColor.FromHtml("#FEF9C3"),
        ["rojo"]     = XLColor.FromHtml("#FECACA"),
        ["gris"]     = XLColor.FromHtml("#E5E7EB"),
        ["rosa"]     = XLColor.FromHtml("#FFC5D3"),
        ["azul"]     = XLColor.FromHtml("#BEDFFB"),
    };

    public byte[] GenerarExcel(NpgsqlDataReader reader, string sheetName = "Preventivos")
    {
        var dt = new DataTable();
        dt.Load(reader);

        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add(sheetName);

        // Encabezados
        for (int c = 0; c < dt.Columns.Count; c++)
        {
            var cell = ws.Cell(1, c + 1);
            cell.Value = dt.Columns[c].ColumnName;
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#1E293B");
            cell.Style.Font.FontColor = XLColor.White;
        }

        // Datos con color por categoría
        int catCol = dt.Columns.IndexOf("categoria_color");

        for (int r = 0; r < dt.Rows.Count; r++)
        {
            var row = dt.Rows[r];
            XLColor? fill = null;

            if (catCol >= 0 && row[catCol] != DBNull.Value)
            {
                var cat = row[catCol].ToString()?.ToLower() ?? "";
                foreach (var kv in ColorMap)
                    if (cat.Contains(kv.Key)) { fill = kv.Value; break; }
            }

            for (int c = 0; c < dt.Columns.Count; c++)
            {
                var cell = ws.Cell(r + 2, c + 1);
                var val  = row[c];
                cell.Value = val == DBNull.Value ? "" : XLCellValue.FromObject(val);
                if (fill != null)
                    cell.Style.Fill.BackgroundColor = fill;
            }
        }

        ws.Columns().AdjustToContents();

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }
}
