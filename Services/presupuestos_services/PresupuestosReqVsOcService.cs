using ChiIT.Data;
using Npgsql;
using ClosedXML.Excel;

public class ReqVsOcDto
{
    public string? NO_REQUISICION { get; set; }
    public string? ORDEN_COMPRA { get; set; }
    public string? FECHA_COMPRA { get; set; }
    public decimal? PO_SUBTOTAL { get; set; }
    public string? MONEDA { get; set; }
    public string? OC_SUBTOTAL { get; set; }
    public string? REGISTRADA_EN_OC { get; set; }
}

public class ReqVsOcRow : ReqVsOcDto
{
    public int ID { get; set; }
    public bool TIENE_PDF { get; set; }
}

public class PresupuestosReqVsOcService
{
    private readonly DbConnectionPool _db;

    public PresupuestosReqVsOcService(DbConnectionPool db)
    {
        _db = db;
    }

    private NpgsqlConnection Abrir()
        => _db.Open();

    // ── filtros dinámicos ─────────────────────────────
    private static void AddFilter(
        NpgsqlCommand cmd,
        List<string> where,
        string column,
        string? value,
        string param)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            where.Add($"{column} ILIKE @{param}");
            cmd.Parameters.AddWithValue(param, $"%{value}%");
        }
    }

    // ── GET PAGINADO ──────────────────────────────────
    public (List<ReqVsOcRow> data, int total) GetAll(
        int page = 1,
        int limit = 10,
        string? NO_REQUISICION = null,
        string? ORDEN_COMPRA = null,
        string? FECHA_COMPRA = null,
        string? PO_SUBTOTAL = null,
        string? MONEDA = null,
        string? OC_SUBTOTAL = null,
        string? REGISTRADA_EN_OC = null)
    {
        using var con = Abrir();
        using var cmd = con.CreateCommand();

        var where = new List<string>();

        AddFilter(cmd, where, "no_requisicion", NO_REQUISICION, "nr");
        AddFilter(cmd, where, "orden_compra", ORDEN_COMPRA, "oc");
        AddFilter(cmd, where, "fecha_compra::text", FECHA_COMPRA, "fc");
        AddFilter(cmd, where, "po_subtotal::text", PO_SUBTOTAL, "ps");
        AddFilter(cmd, where, "moneda", MONEDA, "mo");
        AddFilter(cmd, where, "oc_subtotal", OC_SUBTOTAL, "os");
        AddFilter(cmd, where, "registrada_en_oc", REGISTRADA_EN_OC, "re");

        string filtro = where.Count > 0 ? "WHERE " + string.Join(" AND ", where) : "";

        cmd.CommandText = $"SELECT COUNT(*) FROM req_vs_oc {filtro}";
        int total = Convert.ToInt32(cmd.ExecuteScalar());

        int offset = (page - 1) * limit;

        cmd.CommandText = $"""
        SELECT id, no_requisicion, orden_compra, fecha_compra,
               po_subtotal, moneda, oc_subtotal, registrada_en_oc,
               CASE WHEN pdf IS NOT NULL THEN 1 ELSE 0 END
        FROM req_vs_oc
        {filtro}
        ORDER BY id DESC
        OFFSET {offset} LIMIT {limit}
        """;

        var list = new List<ReqVsOcRow>();

        using var dr = cmd.ExecuteReader();

        while (dr.Read())
        {
            list.Add(new ReqVsOcRow
            {
                ID = dr.GetInt32(0),
                NO_REQUISICION = dr.IsDBNull(1) ? null : dr.GetString(1),
                ORDEN_COMPRA = dr.IsDBNull(2) ? null : dr.GetString(2),
                FECHA_COMPRA = dr.IsDBNull(3) ? null : dr.GetValue(3).ToString(),
                PO_SUBTOTAL = dr.IsDBNull(4) ? null : dr.GetDecimal(4),
                MONEDA = dr.IsDBNull(5) ? null : dr.GetString(5),
                OC_SUBTOTAL = dr.IsDBNull(6) ? null : dr.GetString(6),
                REGISTRADA_EN_OC = dr.IsDBNull(7) ? null : dr.GetString(7),
                TIENE_PDF = dr.GetInt32(8) == 1
            });
        }

        return (list, total);
    }

    // ── CREATE ───────────────────────────────────────
    public void Create(ReqVsOcDto d)
    {
        using var con = Abrir();
        using var cmd = con.CreateCommand();

        cmd.CommandText = """
        INSERT INTO req_vs_oc
        (no_requisicion, orden_compra, fecha_compra,
         po_subtotal, moneda, oc_subtotal, registrada_en_oc)
        VALUES
        (@nr,@oc,@fc,@ps,@mo,@os,@re)
        """;

        cmd.Parameters.AddWithValue("nr", (object?)d.NO_REQUISICION ?? DBNull.Value);
        cmd.Parameters.AddWithValue("oc", (object?)d.ORDEN_COMPRA ?? DBNull.Value);
        cmd.Parameters.AddWithValue("fc", (object?)d.FECHA_COMPRA ?? DBNull.Value);
        cmd.Parameters.AddWithValue("ps", (object?)d.PO_SUBTOTAL ?? DBNull.Value);
        cmd.Parameters.AddWithValue("mo", (object?)d.MONEDA ?? DBNull.Value);
        cmd.Parameters.AddWithValue("os", (object?)d.OC_SUBTOTAL ?? DBNull.Value);
        cmd.Parameters.AddWithValue("re", (object?)d.REGISTRADA_EN_OC ?? DBNull.Value);

        cmd.ExecuteNonQuery();
    }

    // ── UPDATE ───────────────────────────────────────
    public void Update(int id, ReqVsOcDto d)
    {
        using var con = Abrir();
        using var cmd = con.CreateCommand();

        cmd.CommandText = """
        UPDATE req_vs_oc SET
            no_requisicion=@nr,
            orden_compra=@oc,
            fecha_compra=@fc,
            po_subtotal=@ps,
            moneda=@mo,
            oc_subtotal=@os,
            registrada_en_oc=@re
        WHERE id=@id
        """;

        cmd.Parameters.AddWithValue("nr", (object?)d.NO_REQUISICION ?? DBNull.Value);
        cmd.Parameters.AddWithValue("oc", (object?)d.ORDEN_COMPRA ?? DBNull.Value);
        cmd.Parameters.AddWithValue("fc", (object?)d.FECHA_COMPRA ?? DBNull.Value);
        cmd.Parameters.AddWithValue("ps", (object?)d.PO_SUBTOTAL ?? DBNull.Value);
        cmd.Parameters.AddWithValue("mo", (object?)d.MONEDA ?? DBNull.Value);
        cmd.Parameters.AddWithValue("os", (object?)d.OC_SUBTOTAL ?? DBNull.Value);
        cmd.Parameters.AddWithValue("re", (object?)d.REGISTRADA_EN_OC ?? DBNull.Value);
        cmd.Parameters.AddWithValue("id", id);

        cmd.ExecuteNonQuery();
    }

    // ── DELETE ───────────────────────────────────────
    public void Delete(int id)
    {
        using var con = Abrir();
        using var cmd = con.CreateCommand();

        cmd.CommandText = "DELETE FROM req_vs_oc WHERE id=@id";
        cmd.Parameters.AddWithValue("id", id);

        cmd.ExecuteNonQuery();
    }

    // ── PDF ──────────────────────────────────────────
    public void GuardarPDF(int id, byte[] bytes)
    {
        using var con = Abrir();
        using var cmd = con.CreateCommand();

        cmd.CommandText = "UPDATE req_vs_oc SET pdf=@pdf WHERE id=@id";
        cmd.Parameters.AddWithValue("pdf", bytes);
        cmd.Parameters.AddWithValue("id", id);

        cmd.ExecuteNonQuery();
    }

    public byte[]? ObtenerPDF(int id)
    {
        using var con = Abrir();
        using var cmd = con.CreateCommand();

        cmd.CommandText = "SELECT pdf FROM req_vs_oc WHERE id=@id";
        cmd.Parameters.AddWithValue("id", id);

        var r = cmd.ExecuteScalar();

        return r is DBNull or null ? null : (byte[])r;
    }

    public void EliminarPDF(int id)
    {
        using var con = Abrir();
        using var cmd = con.CreateCommand();

        cmd.CommandText = "UPDATE req_vs_oc SET pdf=NULL WHERE id=@id";
        cmd.Parameters.AddWithValue("id", id);

        cmd.ExecuteNonQuery();
    }

    // ── EXCEL ────────────────────────────────────────
    public byte[] GenerarExcel(IEnumerable<ReqVsOcRow> rows)
    {
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("REQ vs OC");

        string[] headers =
        {
            "ID","No Requisicion","Orden Compra","Fecha Compra",
            "PO Subtotal","Moneda","OC Subtotal","Registrada en OC"
        };

        for (int i = 0; i < headers.Length; i++)
        {
            ws.Cell(1, i + 1).Value = headers[i];
            ws.Cell(1, i + 1).Style.Font.Bold = true;
        }

        int row = 2;

        foreach (var r in rows)
        {
            ws.Cell(row, 1).Value = r.ID;
            ws.Cell(row, 2).Value = r.NO_REQUISICION;
            ws.Cell(row, 3).Value = r.ORDEN_COMPRA;
            ws.Cell(row, 4).Value = r.FECHA_COMPRA;
            ws.Cell(row, 5).Value = r.PO_SUBTOTAL;
            ws.Cell(row, 6).Value = r.MONEDA;
            ws.Cell(row, 7).Value = r.OC_SUBTOTAL;
            ws.Cell(row, 8).Value = r.REGISTRADA_EN_OC;
            row++;
        }

        ws.Columns().AdjustToContents();

        using var ms = new MemoryStream();
        wb.SaveAs(ms);

        return ms.ToArray();
    }
    public List<ReqVsOcRow> GetPorAnio(int anio)
    {
        using var con = Abrir();
        using var cmd = con.CreateCommand();

        cmd.CommandText = """
    SELECT id, no_requisicion, orden_compra, fecha_compra,
           po_subtotal, moneda, oc_subtotal, registrada_en_oc,
           CASE WHEN pdf IS NOT NULL THEN 1 ELSE 0 END
    FROM req_vs_oc
    WHERE EXTRACT(YEAR FROM fecha_compra) = @anio
    ORDER BY id DESC
    """;

        cmd.Parameters.AddWithValue("anio", anio);

        var list = new List<ReqVsOcRow>();

        using var dr = cmd.ExecuteReader();

        while (dr.Read())
        {
            list.Add(new ReqVsOcRow
            {
                ID = dr.GetInt32(0),
                NO_REQUISICION = dr.IsDBNull(1) ? null : dr.GetString(1),
                ORDEN_COMPRA = dr.IsDBNull(2) ? null : dr.GetString(2),
                FECHA_COMPRA = dr.IsDBNull(3) ? null : dr.GetValue(3).ToString(),
                PO_SUBTOTAL = dr.IsDBNull(4) ? null : dr.GetDecimal(4),
                MONEDA = dr.IsDBNull(5) ? null : dr.GetString(5),
                OC_SUBTOTAL = dr.IsDBNull(6) ? null : dr.GetString(6),
                REGISTRADA_EN_OC = dr.IsDBNull(7) ? null : dr.GetString(7),
                TIENE_PDF = dr.GetInt32(8) == 1
            });
        }

        return list;
    }
}