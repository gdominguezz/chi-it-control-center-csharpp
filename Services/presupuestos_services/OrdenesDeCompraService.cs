using ChiIT.Data;
using Npgsql;
using ClosedXML.Excel;

public class OrdenDeCompraDto
{
    public string?  ORDEN_DE_COMPRA             { get; set; }
    public string?  FOLIO                       { get; set; }
    public string?  SOLICITANTE                 { get; set; }
    public string?  PRESUPUESTO_MES             { get; set; }
    public string?  SERIE_UBICACION_NO_EMPLEADO { get; set; }
    public string?  ACCESORIO_SOLICITADO        { get; set; }
    public string?  PROVEEDOR_ELEGIDO           { get; set; }
    public string?  PIEZA_SERVICIO              { get; set; }
    public decimal? CANTIDAD                    { get; set; }
    public decimal? PRECIO_UNITARIO             { get; set; }
    public decimal? TOTAL_SIN_IVA               { get; set; }
    public string?  MONEDA                      { get; set; }
    public string?  COMENTARIOS                 { get; set; }
    public string?  HOJA_CONTROL                { get; set; }
    public string?  REQUISICION                 { get; set; }
    public string?  FECHA_OC                    { get; set; }
    public string?  OC                          { get; set; }
    public string?  FECHA_ENTRADA               { get; set; }
    public decimal? CANTIDAD_REGISTRADA         { get; set; }
    public string?  ESTATUS_OC                  { get; set; }
}

public class OrdenDeCompraRow : OrdenDeCompraDto
{
    public int    ID        { get; set; }
    public bool   TIENE_PDF { get; set; }
    public string? PDF_RUTA { get; set; }
}

public class OrdenesDeCompraService
{
    private readonly DbConnectionPool _db;

    public OrdenesDeCompraService(DbConnectionPool db)
    {
        _db = db;
    }

    private NpgsqlConnection Abrir() => _db.Open();

    // ── GET PAGINADO ──────────────────────────────────────────────────────
    public (List<OrdenDeCompraRow> data, int total) GetAll(
        int page = 1,
        int limit = 10,
        string? ORDEN_DE_COMPRA = null,
        string? FOLIO = null,
        string? SOLICITANTE = null,
        string? PRESUPUESTO_MES = null,
        string? SERIE_UBICACION_NO_EMPLEADO = null,
        string? ACCESORIO_SOLICITADO = null,
        string? PROVEEDOR_ELEGIDO = null,
        string? PIEZA_SERVICIO = null,
        string? MONEDA = null,
        string? REQUISICION = null,
        string? OC = null,
        string? ESTATUS_OC = null)
    {
        using var con = Abrir();

        var whereConditions = new List<string>();
        var paramValues     = new List<(string name, string value)>();

        void AddFilter(string column, string? value, string param)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                whereConditions.Add($"{column} ILIKE @{param}");
                paramValues.Add((param, $"%{value}%"));
            }
        }

        AddFilter("orden_de_compra",             ORDEN_DE_COMPRA,             "odc");
        AddFilter("folio",                       FOLIO,                       "fo");
        AddFilter("solicitante",                 SOLICITANTE,                 "sol");
        AddFilter("presupuesto_mes",             PRESUPUESTO_MES,             "pm");
        AddFilter("serie_ubicacion_no_empleado", SERIE_UBICACION_NO_EMPLEADO, "su");
        AddFilter("accesorio_solicitado",        ACCESORIO_SOLICITADO,        "ac");
        AddFilter("proveedor_elegido",           PROVEEDOR_ELEGIDO,           "pr");
        AddFilter("pieza_servicio",              PIEZA_SERVICIO,              "ps");
        AddFilter("moneda",                      MONEDA,                      "mo");
        AddFilter("requisicion",                 REQUISICION,                 "re");
        AddFilter("oc",                          OC,                          "oc");
        AddFilter("estatus_oc",                  ESTATUS_OC,                  "es");

        string filtro = whereConditions.Count > 0
            ? "WHERE " + string.Join(" AND ", whereConditions)
            : "";

        // ── COUNT ─────────────────────────────────────────────────────────
        int total;
        using (var cmdCount = con.CreateCommand())
        {
            cmdCount.CommandText = $"SELECT COUNT(*) FROM ordenes_de_compra {filtro}";
            foreach (var (name, value) in paramValues)
                cmdCount.Parameters.AddWithValue(name, value);
            total = Convert.ToInt32(cmdCount.ExecuteScalar());
        }

        // ── SELECT paginado ───────────────────────────────────────────────
        int offset = (page - 1) * limit;
        var list = new List<OrdenDeCompraRow>();

        using (var cmdSelect = con.CreateCommand())
        {
            cmdSelect.CommandText = $"""
                SELECT id, orden_de_compra, folio, solicitante, presupuesto_mes,
                       serie_ubicacion_no_empleado, accesorio_solicitado, proveedor_elegido,
                       pieza_servicio, cantidad, precio_unitario, total_sin_iva,
                       moneda, comentarios, hoja_control, requisicion,
                       fecha_oc, oc, fecha_entrada, cantidad_registrada, estatus_oc, pdf
                FROM ordenes_de_compra
                {filtro}
                ORDER BY id DESC
                OFFSET {offset} LIMIT {limit}
                """;

            foreach (var (name, value) in paramValues)
                cmdSelect.Parameters.AddWithValue(name, value);

            using var dr = cmdSelect.ExecuteReader();
            while (dr.Read())
            {
                var ruta = dr.IsDBNull(21) ? null : dr.GetString(21);
                list.Add(MapRow(dr, ruta));
            }
        }

        return (list, total);
    }

    // ── GET BY ID ─────────────────────────────────────────────────────────
    public OrdenDeCompraRow? GetById(int id)
    {
        using var con = Abrir();
        using var cmd = con.CreateCommand();

        cmd.CommandText = """
            SELECT id, orden_de_compra, folio, solicitante, presupuesto_mes,
                   serie_ubicacion_no_empleado, accesorio_solicitado, proveedor_elegido,
                   pieza_servicio, cantidad, precio_unitario, total_sin_iva,
                   moneda, comentarios, hoja_control, requisicion,
                   fecha_oc, oc, fecha_entrada, cantidad_registrada, estatus_oc, pdf
            FROM ordenes_de_compra
            WHERE id = @id
            """;

        cmd.Parameters.AddWithValue("id", id);

        using var dr = cmd.ExecuteReader();
        if (!dr.Read()) return null;

        var ruta = dr.IsDBNull(21) ? null : dr.GetString(21);
        return MapRow(dr, ruta);
    }

    // ── MAP ROW ───────────────────────────────────────────────────────────
    private OrdenDeCompraRow MapRow(NpgsqlDataReader dr, string? ruta)
    {
        return new OrdenDeCompraRow
        {
            ID                          = dr.GetInt32(0),
            ORDEN_DE_COMPRA             = dr.IsDBNull(1)  ? null : dr.GetString(1),
            FOLIO                       = dr.IsDBNull(2)  ? null : dr.GetString(2),
            SOLICITANTE                 = dr.IsDBNull(3)  ? null : dr.GetString(3),
            PRESUPUESTO_MES             = dr.IsDBNull(4)  ? null : dr.GetString(4),
            SERIE_UBICACION_NO_EMPLEADO = dr.IsDBNull(5)  ? null : dr.GetString(5),
            ACCESORIO_SOLICITADO        = dr.IsDBNull(6)  ? null : dr.GetString(6),
            PROVEEDOR_ELEGIDO           = dr.IsDBNull(7)  ? null : dr.GetString(7),
            PIEZA_SERVICIO              = dr.IsDBNull(8)  ? null : dr.GetString(8),
            CANTIDAD                    = dr.IsDBNull(9)  ? null : dr.GetDecimal(9),
            PRECIO_UNITARIO             = dr.IsDBNull(10) ? null : dr.GetDecimal(10),
            TOTAL_SIN_IVA               = dr.IsDBNull(11) ? null : dr.GetDecimal(11),
            MONEDA                      = dr.IsDBNull(12) ? null : dr.GetString(12),
            COMENTARIOS                 = dr.IsDBNull(13) ? null : dr.GetString(13),
            HOJA_CONTROL                = dr.IsDBNull(14) ? null : dr.GetString(14),
            REQUISICION                 = dr.IsDBNull(15) ? null : dr.GetString(15),
            FECHA_OC                    = dr.IsDBNull(16) ? null : dr.GetValue(16).ToString(),
            OC                          = dr.IsDBNull(17) ? null : dr.GetString(17),
            FECHA_ENTRADA               = dr.IsDBNull(18) ? null : dr.GetValue(18).ToString(),
            CANTIDAD_REGISTRADA         = dr.IsDBNull(19) ? null : dr.GetDecimal(19),
            ESTATUS_OC                  = dr.IsDBNull(20) ? null : dr.GetString(20),
            PDF_RUTA                    = ruta,
            TIENE_PDF                   = ruta != null
        };
    }

    // ── CREATE ────────────────────────────────────────────────────────────
    public void Create(OrdenDeCompraDto d, string? usuario = null)
    {
        // Calcular TOTAL_SIN_IVA en el backend
        decimal? total = (d.CANTIDAD.HasValue && d.PRECIO_UNITARIO.HasValue)
            ? d.CANTIDAD.Value * d.PRECIO_UNITARIO.Value
            : d.TOTAL_SIN_IVA;

        using var con = Abrir();
        using var cmd = con.CreateCommand();

        cmd.CommandText = """
            INSERT INTO ordenes_de_compra
            (orden_de_compra, folio, solicitante, presupuesto_mes,
             serie_ubicacion_no_empleado, accesorio_solicitado, proveedor_elegido,
             pieza_servicio, cantidad, precio_unitario, total_sin_iva,
             moneda, comentarios, hoja_control, requisicion,
             fecha_oc, oc, fecha_entrada, cantidad_registrada, estatus_oc)
            VALUES
            (@odc,@fo,@sol,@pm,@su,@ac,@pr,@ps,@ca,@pu,@tot,
             @mo,@co,@hc,@re,@foc,@oc,@fen,@cr,@es)
            """;

        cmd.Parameters.AddWithValue("odc", (object?)d.ORDEN_DE_COMPRA             ?? DBNull.Value);
        cmd.Parameters.AddWithValue("fo",  (object?)d.FOLIO                       ?? DBNull.Value);
        cmd.Parameters.AddWithValue("sol", (object?)d.SOLICITANTE                 ?? DBNull.Value);
        cmd.Parameters.AddWithValue("pm",  (object?)d.PRESUPUESTO_MES             ?? DBNull.Value);
        cmd.Parameters.AddWithValue("su",  (object?)d.SERIE_UBICACION_NO_EMPLEADO ?? DBNull.Value);
        cmd.Parameters.AddWithValue("ac",  (object?)d.ACCESORIO_SOLICITADO        ?? DBNull.Value);
        cmd.Parameters.AddWithValue("pr",  (object?)d.PROVEEDOR_ELEGIDO           ?? DBNull.Value);
        cmd.Parameters.AddWithValue("ps",  (object?)d.PIEZA_SERVICIO              ?? DBNull.Value);
        cmd.Parameters.AddWithValue("ca",  (object?)d.CANTIDAD                    ?? DBNull.Value);
        cmd.Parameters.AddWithValue("pu",  (object?)d.PRECIO_UNITARIO             ?? DBNull.Value);
        cmd.Parameters.AddWithValue("tot", (object?)total                         ?? DBNull.Value);
        cmd.Parameters.AddWithValue("mo",  (object?)d.MONEDA                      ?? DBNull.Value);
        cmd.Parameters.AddWithValue("co",  (object?)d.COMENTARIOS                 ?? DBNull.Value);
        cmd.Parameters.AddWithValue("hc",  (object?)d.HOJA_CONTROL                ?? DBNull.Value);
        cmd.Parameters.AddWithValue("re",  (object?)d.REQUISICION                 ?? DBNull.Value);
        cmd.Parameters.AddWithValue("foc", (object?)d.FECHA_OC                    ?? DBNull.Value);
        cmd.Parameters.AddWithValue("oc",  (object?)d.OC                          ?? DBNull.Value);
        cmd.Parameters.AddWithValue("fen", (object?)d.FECHA_ENTRADA               ?? DBNull.Value);
        cmd.Parameters.AddWithValue("cr",  (object?)d.CANTIDAD_REGISTRADA         ?? DBNull.Value);
        cmd.Parameters.AddWithValue("es",  (object?)d.ESTATUS_OC                  ?? DBNull.Value);

        cmd.ExecuteNonQuery();
    }

    // ── UPDATE ────────────────────────────────────────────────────────────
    public void Update(int id, OrdenDeCompraDto d, string? usuario = null)
    {
        var anterior = GetById(id);

        // Recalcular TOTAL_SIN_IVA
        decimal? total = (d.CANTIDAD.HasValue && d.PRECIO_UNITARIO.HasValue)
            ? d.CANTIDAD.Value * d.PRECIO_UNITARIO.Value
            : d.TOTAL_SIN_IVA;

        using var con = Abrir();
        using var cmd = con.CreateCommand();

        cmd.CommandText = """
            UPDATE ordenes_de_compra SET
                orden_de_compra             = @odc,
                folio                       = @fo,
                solicitante                 = @sol,
                presupuesto_mes             = @pm,
                serie_ubicacion_no_empleado = @su,
                accesorio_solicitado        = @ac,
                proveedor_elegido           = @pr,
                pieza_servicio              = @ps,
                cantidad                    = @ca,
                precio_unitario             = @pu,
                total_sin_iva               = @tot,
                moneda                      = @mo,
                comentarios                 = @co,
                hoja_control                = @hc,
                requisicion                 = @re,
                fecha_oc                    = @foc,
                oc                          = @oc,
                fecha_entrada               = @fen,
                cantidad_registrada         = @cr,
                estatus_oc                  = @es
            WHERE id = @id
            """;

        cmd.Parameters.AddWithValue("odc", (object?)d.ORDEN_DE_COMPRA             ?? DBNull.Value);
        cmd.Parameters.AddWithValue("fo",  (object?)d.FOLIO                       ?? DBNull.Value);
        cmd.Parameters.AddWithValue("sol", (object?)d.SOLICITANTE                 ?? DBNull.Value);
        cmd.Parameters.AddWithValue("pm",  (object?)d.PRESUPUESTO_MES             ?? DBNull.Value);
        cmd.Parameters.AddWithValue("su",  (object?)d.SERIE_UBICACION_NO_EMPLEADO ?? DBNull.Value);
        cmd.Parameters.AddWithValue("ac",  (object?)d.ACCESORIO_SOLICITADO        ?? DBNull.Value);
        cmd.Parameters.AddWithValue("pr",  (object?)d.PROVEEDOR_ELEGIDO           ?? DBNull.Value);
        cmd.Parameters.AddWithValue("ps",  (object?)d.PIEZA_SERVICIO              ?? DBNull.Value);
        cmd.Parameters.AddWithValue("ca",  (object?)d.CANTIDAD                    ?? DBNull.Value);
        cmd.Parameters.AddWithValue("pu",  (object?)d.PRECIO_UNITARIO             ?? DBNull.Value);
        cmd.Parameters.AddWithValue("tot", (object?)total                         ?? DBNull.Value);
        cmd.Parameters.AddWithValue("mo",  (object?)d.MONEDA                      ?? DBNull.Value);
        cmd.Parameters.AddWithValue("co",  (object?)d.COMENTARIOS                 ?? DBNull.Value);
        cmd.Parameters.AddWithValue("hc",  (object?)d.HOJA_CONTROL                ?? DBNull.Value);
        cmd.Parameters.AddWithValue("re",  (object?)d.REQUISICION                 ?? DBNull.Value);
        cmd.Parameters.AddWithValue("foc", (object?)d.FECHA_OC                    ?? DBNull.Value);
        cmd.Parameters.AddWithValue("oc",  (object?)d.OC                          ?? DBNull.Value);
        cmd.Parameters.AddWithValue("fen", (object?)d.FECHA_ENTRADA               ?? DBNull.Value);
        cmd.Parameters.AddWithValue("cr",  (object?)d.CANTIDAD_REGISTRADA         ?? DBNull.Value);
        cmd.Parameters.AddWithValue("es",  (object?)d.ESTATUS_OC                  ?? DBNull.Value);
        cmd.Parameters.AddWithValue("id",  id);

        cmd.ExecuteNonQuery();

        if (anterior != null)
            GuardarAuditoria(id, anterior, d, usuario);
    }

    // ── DELETE ────────────────────────────────────────────────────────────
    public void Delete(int id)
    {
        using var con = Abrir();
        using var cmd = con.CreateCommand();
        cmd.CommandText = "DELETE FROM ordenes_de_compra WHERE id = @id";
        cmd.Parameters.AddWithValue("id", id);
        cmd.ExecuteNonQuery();
    }

    // ── PDF ───────────────────────────────────────────────────────────────
    public void GuardarRutaPDF(int id, string ruta)
    {
        using var con = Abrir();
        using var cmd = con.CreateCommand();
        cmd.CommandText = "UPDATE ordenes_de_compra SET pdf = @ruta WHERE id = @id";
        cmd.Parameters.AddWithValue("ruta", ruta);
        cmd.Parameters.AddWithValue("id", id);
        cmd.ExecuteNonQuery();
    }

    public string? ObtenerRutaPDF(int id)
    {
        using var con = Abrir();
        using var cmd = con.CreateCommand();
        cmd.CommandText = "SELECT pdf FROM ordenes_de_compra WHERE id = @id";
        cmd.Parameters.AddWithValue("id", id);
        var r = cmd.ExecuteScalar();
        return r is DBNull or null ? null : (string)r;
    }

    public void EliminarPDF(int id)
    {
        using var con = Abrir();
        using var cmd = con.CreateCommand();
        cmd.CommandText = "UPDATE ordenes_de_compra SET pdf = NULL WHERE id = @id";
        cmd.Parameters.AddWithValue("id", id);
        cmd.ExecuteNonQuery();
    }

    // ── EXCEL ─────────────────────────────────────────────────────────────
    public byte[] GenerarExcel(IEnumerable<OrdenDeCompraRow> rows)
    {
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Ordenes de Compra");

        string[] headers =
        {
            "ID","Orden de Compra","Folio","Solicitante","Presupuesto Mes",
            "Serie/Ubicación/No. Empleado","Accesorio Solicitado","Proveedor Elegido",
            "Pieza/Servicio","Cantidad","Precio Unitario","Total (Sin IVA)",
            "Moneda","Comentarios","Hoja Control","Requisición",
            "Fecha OC","OC","Fecha Entrada","Cantidad Registrada","Estatus OC"
        };

        for (int i = 0; i < headers.Length; i++)
        {
            ws.Cell(1, i + 1).Value = headers[i];
            ws.Cell(1, i + 1).Style.Font.Bold = true;
            ws.Cell(1, i + 1).Style.Fill.BackgroundColor = XLColor.FromHtml("#1a2235");
            ws.Cell(1, i + 1).Style.Font.FontColor = XLColor.White;
        }

        int row = 2;
        foreach (var r in rows)
        {
            ws.Cell(row, 1).Value  = r.ID;
            ws.Cell(row, 2).Value  = r.ORDEN_DE_COMPRA;
            ws.Cell(row, 3).Value  = r.FOLIO;
            ws.Cell(row, 4).Value  = r.SOLICITANTE;
            ws.Cell(row, 5).Value  = r.PRESUPUESTO_MES;
            ws.Cell(row, 6).Value  = r.SERIE_UBICACION_NO_EMPLEADO;
            ws.Cell(row, 7).Value  = r.ACCESORIO_SOLICITADO;
            ws.Cell(row, 8).Value  = r.PROVEEDOR_ELEGIDO;
            ws.Cell(row, 9).Value  = r.PIEZA_SERVICIO;
            ws.Cell(row, 10).Value = r.CANTIDAD;
            ws.Cell(row, 11).Value = r.PRECIO_UNITARIO;
            ws.Cell(row, 12).Value = r.TOTAL_SIN_IVA;
            ws.Cell(row, 13).Value = r.MONEDA;
            ws.Cell(row, 14).Value = r.COMENTARIOS;
            ws.Cell(row, 15).Value = r.HOJA_CONTROL;
            ws.Cell(row, 16).Value = r.REQUISICION;
            ws.Cell(row, 17).Value = r.FECHA_OC;
            ws.Cell(row, 18).Value = r.OC;
            ws.Cell(row, 19).Value = r.FECHA_ENTRADA;
            ws.Cell(row, 20).Value = r.CANTIDAD_REGISTRADA;
            ws.Cell(row, 21).Value = r.ESTATUS_OC;
            row++;
        }

        ws.Columns().AdjustToContents();

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    // ── GET POR AÑO ───────────────────────────────────────────────────────
    public List<OrdenDeCompraRow> GetPorAnio(int anio)
    {
        using var con = Abrir();
        using var cmd = con.CreateCommand();

        cmd.CommandText = """
            SELECT id, orden_de_compra, folio, solicitante, presupuesto_mes,
                   serie_ubicacion_no_empleado, accesorio_solicitado, proveedor_elegido,
                   pieza_servicio, cantidad, precio_unitario, total_sin_iva,
                   moneda, comentarios, hoja_control, requisicion,
                   fecha_oc, oc, fecha_entrada, cantidad_registrada, estatus_oc, pdf
            FROM ordenes_de_compra
            WHERE EXTRACT(YEAR FROM fecha_oc) = @anio
            ORDER BY id DESC
            """;

        cmd.Parameters.AddWithValue("anio", anio);

        var list = new List<OrdenDeCompraRow>();
        using var dr = cmd.ExecuteReader();

        while (dr.Read())
        {
            var ruta = dr.IsDBNull(21) ? null : dr.GetString(21);
            list.Add(MapRow(dr, ruta));
        }

        return list;
    }

    // ── HISTORIAL ─────────────────────────────────────────────────────────
    public List<object> GetHistorial(int id)
    {
        using var con = Abrir();
        using var cmd = con.CreateCommand();

        cmd.CommandText = """
            SELECT id, fecha_cambio, usuario, registro_anterior, registro_nuevo
            FROM auditoria_ordenes_de_compra
            WHERE registro_id = @id
            ORDER BY fecha_cambio DESC
            """;

        cmd.Parameters.AddWithValue("id", id);

        var list = new List<object>();
        using var dr = cmd.ExecuteReader();

        while (dr.Read())
        {
            list.Add(new
            {
                id                = dr.GetInt32(0),
                fecha             = dr.IsDBNull(1) ? null : dr.GetDateTime(1).ToString("yyyy-MM-dd HH:mm:ss"),
                usuario           = dr.IsDBNull(2) ? null : dr.GetString(2),
                registro_anterior = dr.IsDBNull(3) ? null : dr.GetString(3),
                registro_nuevo    = dr.IsDBNull(4) ? null : dr.GetString(4)
            });
        }

        return list;
    }

    // ── AUDITORÍA ─────────────────────────────────────────────────────────
    private void GuardarAuditoria(int id, OrdenDeCompraRow anterior, OrdenDeCompraDto nuevo, string? usuario)
    {
        using var con = Abrir();
        using var cmd = con.CreateCommand();

        cmd.CommandText = """
            INSERT INTO auditoria_ordenes_de_compra
            (registro_id, fecha_cambio, usuario, registro_anterior, registro_nuevo)
            VALUES (@rid, NOW(), @usr, @ant, @nvo)
            """;

        cmd.Parameters.AddWithValue("rid", id);
        cmd.Parameters.AddWithValue("usr", (object?)usuario ?? DBNull.Value);
        cmd.Parameters.AddWithValue("ant", System.Text.Json.JsonSerializer.Serialize(anterior));
        cmd.Parameters.AddWithValue("nvo", System.Text.Json.JsonSerializer.Serialize(nuevo));

        cmd.ExecuteNonQuery();
    }
    public List<string> GetSolicitantesUnicos()
    {
        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
        SELECT DISTINCT SOLICITANTE 
        FROM ORDENES_DE_COMPRA 
        WHERE SOLICITANTE IS NOT NULL AND SOLICITANTE <> ''
        ORDER BY SOLICITANTE";

        var lista = new List<string>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
            lista.Add(reader.GetString(0));

        return lista;
    }
}
