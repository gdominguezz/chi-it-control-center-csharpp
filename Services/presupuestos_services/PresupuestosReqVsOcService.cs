using System.Data;
using Microsoft.Data.SqlClient;
using ClosedXML.Excel;

public class ReqVsOcDto
{
    public string? NO_REQUISICION   { get; set; }
    public string? ORDEN_COMPRA     { get; set; }
    public string? FECHA_COMPRA     { get; set; }
    public decimal? PO_SUBTOTAL     { get; set; }
    public string? MONEDA           { get; set; }
    public string? OC_SUBTOTAL      { get; set; }
    public string? REGISTRADA_EN_OC { get; set; }
}

public class ReqVsOcRow : ReqVsOcDto
{
    public int   ID       { get; set; }
    public bool  TIENE_PDF { get; set; }
}

public class PresupuestosReqVsOcService
{
    private readonly string _conn;
    public PresupuestosReqVsOcService(IConfiguration cfg)
        => _conn = cfg.GetConnectionString("DefaultConnection")!;

    // ── Helpers ────────────────────────────────────────────────────────────
    private SqlConnection Abrir()
    {
        var c = new SqlConnection(_conn);
        c.Open();
        return c;
    }

    private static void AddFilter(
        SqlCommand cmd, List<string> where,
        string column, string? value, string param)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            where.Add($"{column} LIKE @{param}");
            cmd.Parameters.AddWithValue($"@{param}", $"%{value}%");
        }
    }

    // ── GET paginado ───────────────────────────────────────────────────────
    public (List<ReqVsOcRow> data, int total) GetAll(
        int page = 1, int limit = 10,
        string? NO_REQUISICION   = null,
        string? ORDEN_COMPRA     = null,
        string? FECHA_COMPRA     = null,
        string? PO_SUBTOTAL      = null,
        string? MONEDA           = null,
        string? OC_SUBTOTAL      = null,
        string? REGISTRADA_EN_OC = null)
    {
        using var con = Abrir();
        var cmd   = con.CreateCommand();
        var where = new List<string>();

        AddFilter(cmd, where, "NO_REQUISICION",   NO_REQUISICION,   "nr");
        AddFilter(cmd, where, "ORDEN_COMPRA",     ORDEN_COMPRA,     "oc");
        AddFilter(cmd, where, "CAST(FECHA_COMPRA AS VARCHAR)", FECHA_COMPRA, "fc");
        AddFilter(cmd, where, "CAST(PO_SUBTOTAL AS VARCHAR)", PO_SUBTOTAL,  "ps");
        AddFilter(cmd, where, "MONEDA",           MONEDA,           "mo");
        AddFilter(cmd, where, "OC_SUBTOTAL",      OC_SUBTOTAL,      "os");
        AddFilter(cmd, where, "REGISTRADA_EN_OC", REGISTRADA_EN_OC, "re");

        string filtro = where.Count > 0 ? "WHERE " + string.Join(" AND ", where) : "";

        // Total
        cmd.CommandText = $"SELECT COUNT(*) FROM PRESUPUESTOS_REQ_VS_OC {filtro}";
        int total = (int)cmd.ExecuteScalar()!;

        // Datos paginados
        int offset = (page - 1) * limit;
        cmd.CommandText = $@"
            SELECT ID, NO_REQUISICION, ORDEN_COMPRA, FECHA_COMPRA,
                   PO_SUBTOTAL, MONEDA, OC_SUBTOTAL, REGISTRADA_EN_OC,
                   CASE WHEN PDF IS NOT NULL THEN 1 ELSE 0 END AS TIENE_PDF
            FROM PRESUPUESTOS_REQ_VS_OC
            {filtro}
            ORDER BY ID DESC
            OFFSET {offset} ROWS FETCH NEXT {limit} ROWS ONLY";

        var list = new List<ReqVsOcRow>();
        using var dr = cmd.ExecuteReader();
        while (dr.Read())
        {
            list.Add(new ReqVsOcRow
            {
                ID              = dr.GetInt32(0),
                NO_REQUISICION  = dr.IsDBNull(1)  ? null : dr.GetString(1),
                ORDEN_COMPRA    = dr.IsDBNull(2)  ? null : dr.GetString(2),
                FECHA_COMPRA    = dr.IsDBNull(3)  ? null : dr.GetDateTime(3).ToString("yyyy-MM-dd"),
                PO_SUBTOTAL     = dr.IsDBNull(4)  ? null : dr.GetDecimal(4),
                MONEDA          = dr.IsDBNull(5)  ? null : dr.GetString(5),
                OC_SUBTOTAL     = dr.IsDBNull(6)  ? null : dr.GetString(6),
                REGISTRADA_EN_OC= dr.IsDBNull(7)  ? null : dr.GetString(7),
                TIENE_PDF       = dr.GetInt32(8) == 1
            });
        }
        return (list, total);
    }

    // ── CREATE ─────────────────────────────────────────────────────────────
    public void Create(ReqVsOcDto d)
    {
        using var con = Abrir();
        var cmd = con.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO PRESUPUESTOS_REQ_VS_OC
                (NO_REQUISICION, ORDEN_COMPRA, FECHA_COMPRA,
                 PO_SUBTOTAL, MONEDA, OC_SUBTOTAL, REGISTRADA_EN_OC)
            VALUES
                (@NR, @OC, @FC, @PS, @MO, @OS, @RE)";

        cmd.Parameters.AddWithValue("@NR", (object?)d.NO_REQUISICION   ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@OC", (object?)d.ORDEN_COMPRA     ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@FC", (object?)d.FECHA_COMPRA     ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@PS", (object?)d.PO_SUBTOTAL      ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@MO", (object?)d.MONEDA           ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@OS", (object?)d.OC_SUBTOTAL      ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@RE", (object?)d.REGISTRADA_EN_OC ?? DBNull.Value);
        cmd.ExecuteNonQuery();
    }

    // ── UPDATE ─────────────────────────────────────────────────────────────
    public void Update(int id, ReqVsOcDto d)
    {
        using var con = Abrir();
        var cmd = con.CreateCommand();
        cmd.CommandText = @"
            UPDATE PRESUPUESTOS_REQ_VS_OC SET
                NO_REQUISICION   = @NR,
                ORDEN_COMPRA     = @OC,
                FECHA_COMPRA     = @FC,
                PO_SUBTOTAL      = @PS,
                MONEDA           = @MO,
                OC_SUBTOTAL      = @OS,
                REGISTRADA_EN_OC = @RE
            WHERE ID = @ID";

        cmd.Parameters.AddWithValue("@NR", (object?)d.NO_REQUISICION   ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@OC", (object?)d.ORDEN_COMPRA     ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@FC", (object?)d.FECHA_COMPRA     ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@PS", (object?)d.PO_SUBTOTAL      ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@MO", (object?)d.MONEDA           ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@OS", (object?)d.OC_SUBTOTAL      ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@RE", (object?)d.REGISTRADA_EN_OC ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@ID", id);
        cmd.ExecuteNonQuery();
    }

    // ── DELETE ─────────────────────────────────────────────────────────────
    public void Delete(int id)
    {
        using var con = Abrir();
        var cmd = con.CreateCommand();
        cmd.CommandText = "DELETE FROM PRESUPUESTOS_REQ_VS_OC WHERE ID = @ID";
        cmd.Parameters.AddWithValue("@ID", id);
        cmd.ExecuteNonQuery();
    }

    // ── PDF ────────────────────────────────────────────────────────────────
    public void GuardarPDF(int id, byte[] bytes)
    {
        using var con = Abrir();
        var cmd = con.CreateCommand();
        cmd.CommandText = "UPDATE PRESUPUESTOS_REQ_VS_OC SET PDF = @PDF WHERE ID = @ID";
        cmd.Parameters.AddWithValue("@PDF", bytes);
        cmd.Parameters.AddWithValue("@ID",  id);
        cmd.ExecuteNonQuery();
    }

    public byte[]? ObtenerPDF(int id)
    {
        using var con = Abrir();
        var cmd = con.CreateCommand();
        cmd.CommandText = "SELECT PDF FROM PRESUPUESTOS_REQ_VS_OC WHERE ID = @ID";
        cmd.Parameters.AddWithValue("@ID", id);
        var result = cmd.ExecuteScalar();
        return result is DBNull or null ? null : (byte[])result;
    }

    public void EliminarPDF(int id)
    {
        using var con = Abrir();
        var cmd = con.CreateCommand();
        cmd.CommandText = "UPDATE PRESUPUESTOS_REQ_VS_OC SET PDF = NULL WHERE ID = @ID";
        cmd.Parameters.AddWithValue("@ID", id);
        cmd.ExecuteNonQuery();
    }

    // ── GET por año ────────────────────────────────────────────────────────
    public List<ReqVsOcRow> GetPorAnio(int anio)
    {
        using var con = Abrir();
        var cmd = con.CreateCommand();
        cmd.CommandText = @"
            SELECT ID, NO_REQUISICION, ORDEN_COMPRA, FECHA_COMPRA,
                   PO_SUBTOTAL, MONEDA, OC_SUBTOTAL, REGISTRADA_EN_OC,
                   CASE WHEN PDF IS NOT NULL THEN 1 ELSE 0 END AS TIENE_PDF
            FROM PRESUPUESTOS_REQ_VS_OC
            WHERE YEAR(FECHA_COMPRA) = @ANIO
            ORDER BY ID DESC";
        cmd.Parameters.AddWithValue("@ANIO", anio);

        var list = new List<ReqVsOcRow>();
        using var dr = cmd.ExecuteReader();
        while (dr.Read())
        {
            list.Add(new ReqVsOcRow
            {
                ID               = dr.GetInt32(0),
                NO_REQUISICION   = dr.IsDBNull(1) ? null : dr.GetString(1),
                ORDEN_COMPRA     = dr.IsDBNull(2) ? null : dr.GetString(2),
                FECHA_COMPRA     = dr.IsDBNull(3) ? null : dr.GetDateTime(3).ToString("yyyy-MM-dd"),
                PO_SUBTOTAL      = dr.IsDBNull(4) ? null : dr.GetDecimal(4),
                MONEDA           = dr.IsDBNull(5) ? null : dr.GetString(5),
                OC_SUBTOTAL      = dr.IsDBNull(6) ? null : dr.GetString(6),
                REGISTRADA_EN_OC = dr.IsDBNull(7) ? null : dr.GetString(7),
                TIENE_PDF        = dr.GetInt32(8) == 1
            });
        }
        return list;
    }

    // ── EXCEL ──────────────────────────────────────────────────────────────
    public byte[] GenerarExcel(IEnumerable<ReqVsOcRow> rows)
    {
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("REQ vs OC");

        // Encabezados
        string[] headers = {
            "ID","No. Requisición","Orden de Compra","Fecha de Compra",
            "PO Subtotal","Moneda","OC Subtotal","Registrada en OC"
        };
        for (int i = 0; i < headers.Length; i++)
        {
            var cell = ws.Cell(1, i + 1);
            cell.Value = headers[i];
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#1e293b");
            cell.Style.Font.FontColor = XLColor.White;
        }

        // Datos
        int row = 2;
        foreach (var r in rows)
        {
            ws.Cell(row, 1).Value = r.ID;
            ws.Cell(row, 2).Value = r.NO_REQUISICION   ?? "";
            ws.Cell(row, 3).Value = r.ORDEN_COMPRA     ?? "";
            ws.Cell(row, 4).Value = r.FECHA_COMPRA     ?? "";
            ws.Cell(row, 5).Value = (double?)r.PO_SUBTOTAL ?? 0;
            ws.Cell(row, 6).Value = r.MONEDA           ?? "";
            ws.Cell(row, 7).Value = r.OC_SUBTOTAL      ?? "";
            ws.Cell(row, 8).Value = r.REGISTRADA_EN_OC ?? "";

            if (row % 2 == 0)
                ws.Row(row).Style.Fill.BackgroundColor = XLColor.FromHtml("#f1f5f9");
            row++;
        }

        ws.Columns().AdjustToContents();

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }
}
