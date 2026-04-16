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
    public string? PDF_RUTA { get; set; }
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

        var whereConditions = new List<string>();
        var paramValues = new List<(string name, string value)>();

        void CollectFilter(string column, string? value, string param)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                whereConditions.Add($"{column} ILIKE @{param}");
                paramValues.Add((param, $"%{value}%"));
            }
        }

        CollectFilter("no_requisicion", NO_REQUISICION, "nr");
        CollectFilter("orden_compra", ORDEN_COMPRA, "oc");
        CollectFilter("fecha_compra::text", FECHA_COMPRA, "fc");
        CollectFilter("po_subtotal::text", PO_SUBTOTAL, "ps");
        CollectFilter("moneda", MONEDA, "mo");
        CollectFilter("oc_subtotal", OC_SUBTOTAL, "os");
        CollectFilter("registrada_en_oc", REGISTRADA_EN_OC, "re");

        string filtro = whereConditions.Count > 0
            ? "WHERE " + string.Join(" AND ", whereConditions)
            : "";

        // ── COUNT ─────────────────────────────────────
        int total;
        using (var cmdCount = con.CreateCommand())
        {
            cmdCount.CommandText = $"SELECT COUNT(*) FROM req_vs_oc {filtro}";
            foreach (var (name, value) in paramValues)
                cmdCount.Parameters.AddWithValue(name, value);
            total = Convert.ToInt32(cmdCount.ExecuteScalar());
        }

        // ── SELECT paginado ───────────────────────────
        int offset = (page - 1) * limit;
        var list = new List<ReqVsOcRow>();

        using (var cmdSelect = con.CreateCommand())
        {
            cmdSelect.CommandText = $"""
                SELECT id, no_requisicion, orden_compra, fecha_compra,
                       po_subtotal, moneda, oc_subtotal, registrada_en_oc,
                       pdf
                FROM req_vs_oc
                {filtro}
                ORDER BY id DESC
                OFFSET {offset} LIMIT {limit}
                """;

            foreach (var (name, value) in paramValues)
                cmdSelect.Parameters.AddWithValue(name, value);

            using var dr = cmdSelect.ExecuteReader();
            while (dr.Read())
            {
                var ruta = dr.IsDBNull(8) ? null : dr.GetString(8);
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
                    PDF_RUTA = ruta,
                    TIENE_PDF = ruta != null
                });
            }
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
            no_requisicion   = @nr,
            orden_compra     = @oc,
            fecha_compra     = @fc,
            po_subtotal      = @ps,
            moneda           = @mo,
            oc_subtotal      = @os,
            registrada_en_oc = @re
        WHERE id = @id
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

        cmd.CommandText = "DELETE FROM req_vs_oc WHERE id = @id";
        cmd.Parameters.AddWithValue("id", id);

        cmd.ExecuteNonQuery();
    }

    // ── PDF: guardar ruta ─────────────────────────────
    public void GuardarRutaPDF(int id, string ruta)
    {
        using var con = Abrir();
        using var cmd = con.CreateCommand();

        cmd.CommandText = "UPDATE req_vs_oc SET pdf = @ruta WHERE id = @id";
        cmd.Parameters.AddWithValue("ruta", ruta);
        cmd.Parameters.AddWithValue("id", id);

        cmd.ExecuteNonQuery();
    }

    // ── PDF: obtener ruta ─────────────────────────────
    public string? ObtenerRutaPDF(int id)
    {
        using var con = Abrir();
        using var cmd = con.CreateCommand();

        cmd.CommandText = "SELECT pdf FROM req_vs_oc WHERE id = @id";
        cmd.Parameters.AddWithValue("id", id);

        var r = cmd.ExecuteScalar();
        return r is DBNull or null ? null : (string)r;
    }

    // ── PDF: eliminar ruta ────────────────────────────
    public void EliminarPDF(int id)
    {
        using var con = Abrir();
        using var cmd = con.CreateCommand();

        cmd.CommandText = "UPDATE req_vs_oc SET pdf = NULL WHERE id = @id";
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
            "No Requisicion","Orden Compra","Fecha Compra",
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
            ws.Cell(row, 1).Value = r.NO_REQUISICION;
            ws.Cell(row, 2).Value = r.ORDEN_COMPRA;
            ws.Cell(row, 3).Value = r.FECHA_COMPRA;
            ws.Cell(row, 4).Value = r.PO_SUBTOTAL;
            ws.Cell(row, 5).Value = r.MONEDA;
            ws.Cell(row, 6).Value = r.OC_SUBTOTAL;
            ws.Cell(row, 7).Value = r.REGISTRADA_EN_OC;
            row++;
        }

        ws.Columns().AdjustToContents();

        using var ms = new MemoryStream();
        wb.SaveAs(ms);

        return ms.ToArray();
    }

    // ── GET POR AÑO ──────────────────────────────────
    public List<ReqVsOcRow> GetPorAnio(int anio)
    {
        using var con = Abrir();
        using var cmd = con.CreateCommand();

        cmd.CommandText = """
            SELECT id, no_requisicion, orden_compra, fecha_compra,
                   po_subtotal, moneda, oc_subtotal, registrada_en_oc,
                   pdf
            FROM req_vs_oc
            WHERE EXTRACT(YEAR FROM fecha_compra) = @anio
            ORDER BY id DESC
            """;

        cmd.Parameters.AddWithValue("anio", anio);

        var list = new List<ReqVsOcRow>();
        using var dr = cmd.ExecuteReader();

        while (dr.Read())
        {
            var ruta = dr.IsDBNull(8) ? null : dr.GetString(8);
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
                PDF_RUTA = ruta,
                TIENE_PDF = ruta != null
            });
        }

        return list;
    }
    public List<object> GetHistorial(int id)
    {
        using var con = Abrir();
        using var cmd = con.CreateCommand();

        cmd.CommandText = """
    SELECT id, fecha_cambio, usuario, registro_anterior, registro_nuevo
    FROM auditoria_req_vs_oc
    WHERE registro_id=@id
    ORDER BY fecha_cambio DESC
    """;

        cmd.Parameters.AddWithValue("id", id);

        var list = new List<object>();

        using var dr = cmd.ExecuteReader();

        while (dr.Read())
        {
            list.Add(new
            {
                id = dr.GetInt32(0),
                fecha = dr.IsDBNull(1) ? null : dr.GetDateTime(1).ToString("yyyy-MM-dd HH:mm:ss"),
                usuario = dr.IsDBNull(2) ? null : dr.GetString(2),
                registro_anterior = dr.IsDBNull(3) ? null : dr.GetString(3),
                registro_nuevo = dr.IsDBNull(4) ? null : dr.GetString(4)
            });
        }

        return list;
    }
}