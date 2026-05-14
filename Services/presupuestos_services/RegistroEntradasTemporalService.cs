using ChiIT.Data;
using Microsoft.Data.SqlClient;
using ClosedXML.Excel;

public class RegistroEntradaDto
{
    public string? ID_REGISTRO { get; set; }
    public string? ID_OC { get; set; }
    public string? SERIE { get; set; }
    public string? FOLIO { get; set; }
    public string? OC { get; set; }
    public string? FECHA_INGRESO { get; set; }
    public string? PROVEEDOR { get; set; }
    public string? RECIBIDO_POR { get; set; }
    public string? CATEGORIA { get; set; }
    public string? SUBCATEGORIA { get; set; }
    public string? TIPO { get; set; }
    public string? MARCA { get; set; }
    public string? MODELO { get; set; }
    public string? UBICACION { get; set; }
    public string? COMENTARIOS { get; set; }
    public decimal? PRECIO_UNITARIO { get; set; }
    public string? MONEDA { get; set; }
    public string? STATUS_ENTRADA { get; set; }
    public string? FECHA_SALIDA { get; set; }
    public string? DESTINO { get; set; }
    public string? RESPONSABLE { get; set; }
}

public class RegistroEntradaRow : RegistroEntradaDto
{
    public int ID { get; set; }
    public bool TIENE_PDF { get; set; }
    public string? PDF_RUTA { get; set; }
}

public class RegistroEntradasTemporalService
{
    private readonly DbConnectionPool _db;

    public RegistroEntradasTemporalService(DbConnectionPool db)
    {
        _db = db;
    }

    private SqlConnection Abrir() => _db.Open();

    // ── GET PAGINADO ──────────────────────────────────────────────────────
    public (List<RegistroEntradaRow> data, int total) GetAll(
        int page = 1,
        int limit = 10,
        string? ID_REGISTRO = null,
        string? ID_OC = null,
        string? SERIE = null,
        string? FOLIO = null,
        string? OC = null,
        string? FECHA_INGRESO = null,
        string? PROVEEDOR = null,
        string? RECIBIDO_POR = null,
        string? CATEGORIA = null,
        string? SUBCATEGORIA = null,
        string? TIPO = null,
        string? MARCA = null,
        string? MODELO = null,
        string? UBICACION = null,
        string? MONEDA = null,
        string? STATUS_ENTRADA = null)
    {
        using var con = Abrir();

        var whereConditions = new List<string>();

        whereConditions.Add("(activo IS NULL OR activo = 1)");

        var paramValues = new List<(string name, string value)>();

        void AddFilter(string column, string? value, string param)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                whereConditions.Add($"{column} LIKE @{param}");
                paramValues.Add((param, $"%{value}%"));
            }
        }

        AddFilter("id_registro",        ID_REGISTRO,  "ir");
        AddFilter("id_oc",              ID_OC,        "io");
        AddFilter("serie",              SERIE,        "se");
        AddFilter("folio",              FOLIO,        "fo");
        AddFilter("oc",                 OC,           "oc");
        AddFilter("fecha_ingreso",FECHA_INGRESO,"fi");
        AddFilter("proveedor",          PROVEEDOR,    "pr");
        AddFilter("recibido_por",       RECIBIDO_POR, "rp");
        AddFilter("categoria",          CATEGORIA,    "ca");
        AddFilter("subcategoria",       SUBCATEGORIA, "sc");
        AddFilter("tipo",               TIPO,         "ti");
        AddFilter("marca",              MARCA,        "ma");
        AddFilter("modelo",             MODELO,       "mo");
        AddFilter("ubicacion",          UBICACION,    "ub");
        AddFilter("moneda",             MONEDA,       "mn");
        AddFilter("status_entrada",     STATUS_ENTRADA,"st");

        string filtro = whereConditions.Count > 0
            ? "WHERE " + string.Join(" AND ", whereConditions)
            : "";

        // ── COUNT ─────────────────────────────────────────────────────────
        int total;
        using (var cmdCount = con.CreateCommand())
        {
            cmdCount.CommandText = $"SELECT COUNT(*) FROM registro_entradas_temporal {filtro}";
            foreach (var (name, value) in paramValues)
                cmdCount.Parameters.AddWithValue(name, value);
            total = Convert.ToInt32(cmdCount.ExecuteScalar());
        }

        // ── SELECT paginado ───────────────────────────────────────────────
        int offset = (page - 1) * limit;
        var list = new List<RegistroEntradaRow>();

        using (var cmdSelect = con.CreateCommand())
        {
            cmdSelect.CommandText = $"""
                SELECT id, id_registro, id_oc, serie, folio, oc,
                       fecha_ingreso, proveedor, recibido_por, categoria,
                       subcategoria, tipo, marca, modelo, ubicacion,
                       comentarios, precio_unitario, moneda, status_entrada,
                       fecha_salida, destino, responsable, pdf
                FROM registro_entradas_temporal
                {filtro}
                ORDER BY id DESC
                OFFSET {offset} LIMIT {limit}
                """;

            foreach (var (name, value) in paramValues)
                cmdSelect.Parameters.AddWithValue(name, value);

            using var dr = cmdSelect.ExecuteReader();
            while (dr.Read())
            {
                var ruta = dr.IsDBNull(22) ? null : dr.GetString(22);
                list.Add(new RegistroEntradaRow
                {
                    ID               = dr.GetInt32(0),
                    ID_REGISTRO      = dr.IsDBNull(1)  ? null : dr.GetString(1),
                    ID_OC            = dr.IsDBNull(2)  ? null : dr.GetString(2),
                    SERIE            = dr.IsDBNull(3)  ? null : dr.GetString(3),
                    FOLIO            = dr.IsDBNull(4)  ? null : dr.GetString(4),
                    OC               = dr.IsDBNull(5)  ? null : dr.GetString(5),
                    FECHA_INGRESO    = dr.IsDBNull(6)  ? null : dr.GetValue(6).ToString(),
                    PROVEEDOR        = dr.IsDBNull(7)  ? null : dr.GetString(7),
                    RECIBIDO_POR     = dr.IsDBNull(8)  ? null : dr.GetString(8),
                    CATEGORIA        = dr.IsDBNull(9)  ? null : dr.GetString(9),
                    SUBCATEGORIA     = dr.IsDBNull(10) ? null : dr.GetString(10),
                    TIPO             = dr.IsDBNull(11) ? null : dr.GetString(11),
                    MARCA            = dr.IsDBNull(12) ? null : dr.GetString(12),
                    MODELO           = dr.IsDBNull(13) ? null : dr.GetString(13),
                    UBICACION        = dr.IsDBNull(14) ? null : dr.GetString(14),
                    COMENTARIOS      = dr.IsDBNull(15) ? null : dr.GetString(15),
                    PRECIO_UNITARIO  = dr.IsDBNull(16) ? null : dr.GetDecimal(16),
                    MONEDA           = dr.IsDBNull(17) ? null : dr.GetString(17),
                    STATUS_ENTRADA   = dr.IsDBNull(18) ? null : dr.GetString(18),
                    FECHA_SALIDA     = dr.IsDBNull(19) ? null : dr.GetValue(19).ToString(),
                    DESTINO          = dr.IsDBNull(20) ? null : dr.GetString(20),
                    RESPONSABLE      = dr.IsDBNull(21) ? null : dr.GetString(21),
                    PDF_RUTA         = ruta,
                    TIENE_PDF        = ruta != null
                });
            }
        }

        return (list, total);
    }

    // ── GET BY ID ─────────────────────────────────────────────────────────
    public RegistroEntradaRow? GetById(int id)
    {
        using var con = Abrir();
        using var cmd = con.CreateCommand();

        cmd.CommandText = """
            SELECT id, id_registro, id_oc, serie, folio, oc,
                   fecha_ingreso, proveedor, recibido_por, categoria,
                   subcategoria, tipo, marca, modelo, ubicacion,
                   comentarios, precio_unitario, moneda, status_entrada,
                   fecha_salida, destino, responsable, pdf
            FROM registro_entradas_temporal
            WHERE id = @id
            AND (activo IS NULL OR activo = 1)
            """;

        cmd.Parameters.AddWithValue("id", id);

        using var dr = cmd.ExecuteReader();
        if (!dr.Read()) return null;

        var ruta = dr.IsDBNull(22) ? null : dr.GetString(22);
        return new RegistroEntradaRow
        {
            ID               = dr.GetInt32(0),
            ID_REGISTRO      = dr.IsDBNull(1)  ? null : dr.GetString(1),
            ID_OC            = dr.IsDBNull(2)  ? null : dr.GetString(2),
            SERIE            = dr.IsDBNull(3)  ? null : dr.GetString(3),
            FOLIO            = dr.IsDBNull(4)  ? null : dr.GetString(4),
            OC               = dr.IsDBNull(5)  ? null : dr.GetString(5),
            FECHA_INGRESO    = dr.IsDBNull(6)  ? null : dr.GetValue(6).ToString(),
            PROVEEDOR        = dr.IsDBNull(7)  ? null : dr.GetString(7),
            RECIBIDO_POR     = dr.IsDBNull(8)  ? null : dr.GetString(8),
            CATEGORIA        = dr.IsDBNull(9)  ? null : dr.GetString(9),
            SUBCATEGORIA     = dr.IsDBNull(10) ? null : dr.GetString(10),
            TIPO             = dr.IsDBNull(11) ? null : dr.GetString(11),
            MARCA            = dr.IsDBNull(12) ? null : dr.GetString(12),
            MODELO           = dr.IsDBNull(13) ? null : dr.GetString(13),
            UBICACION        = dr.IsDBNull(14) ? null : dr.GetString(14),
            COMENTARIOS      = dr.IsDBNull(15) ? null : dr.GetString(15),
            PRECIO_UNITARIO  = dr.IsDBNull(16) ? null : dr.GetDecimal(16),
            MONEDA           = dr.IsDBNull(17) ? null : dr.GetString(17),
            STATUS_ENTRADA   = dr.IsDBNull(18) ? null : dr.GetString(18),
            FECHA_SALIDA     = dr.IsDBNull(19) ? null : dr.GetValue(19).ToString(),
            DESTINO          = dr.IsDBNull(20) ? null : dr.GetString(20),
            RESPONSABLE      = dr.IsDBNull(21) ? null : dr.GetString(21),
            PDF_RUTA         = ruta,
            TIENE_PDF        = ruta != null
        };
    }

    // ── CREATE ────────────────────────────────────────────────────────────
    public void Create(RegistroEntradaDto d)
    {
        using var con = Abrir();
        using var cmd = con.CreateCommand();

        cmd.CommandText = """
            INSERT INTO registro_entradas_temporal
            (id_registro, id_oc, serie, folio, oc, fecha_ingreso,
             proveedor, recibido_por, categoria, subcategoria, tipo,
             marca, modelo, ubicacion, comentarios, precio_unitario,
             moneda, status_entrada, fecha_salida, destino, responsable)
            VALUES
            (@ir,@io,@se,@fo,@oc,@fi,
             @pr,@rp,@ca,@sc,@ti,
             @ma,@mo,@ub,@co,@pu,
             @mn,@st,@fs,@de,@re)
            """;

        cmd.Parameters.AddWithValue("ir", (object?)d.ID_REGISTRO     ?? DBNull.Value);
        cmd.Parameters.AddWithValue("io", (object?)d.ID_OC           ?? DBNull.Value);
        cmd.Parameters.AddWithValue("se", (object?)d.SERIE           ?? DBNull.Value);
        cmd.Parameters.AddWithValue("fo", (object?)d.FOLIO           ?? DBNull.Value);
        cmd.Parameters.AddWithValue("oc", (object?)d.OC              ?? DBNull.Value);
        cmd.Parameters.AddWithValue("fi", (object?)d.FECHA_INGRESO   ?? DBNull.Value);
        cmd.Parameters.AddWithValue("pr", (object?)d.PROVEEDOR       ?? DBNull.Value);
        cmd.Parameters.AddWithValue("rp", (object?)d.RECIBIDO_POR    ?? DBNull.Value);
        cmd.Parameters.AddWithValue("ca", (object?)d.CATEGORIA       ?? DBNull.Value);
        cmd.Parameters.AddWithValue("sc", (object?)d.SUBCATEGORIA    ?? DBNull.Value);
        cmd.Parameters.AddWithValue("ti", (object?)d.TIPO            ?? DBNull.Value);
        cmd.Parameters.AddWithValue("ma", (object?)d.MARCA           ?? DBNull.Value);
        cmd.Parameters.AddWithValue("mo", (object?)d.MODELO          ?? DBNull.Value);
        cmd.Parameters.AddWithValue("ub", (object?)d.UBICACION       ?? DBNull.Value);
        cmd.Parameters.AddWithValue("co", (object?)d.COMENTARIOS     ?? DBNull.Value);
        cmd.Parameters.AddWithValue("pu", (object?)d.PRECIO_UNITARIO ?? DBNull.Value);
        cmd.Parameters.AddWithValue("mn", (object?)d.MONEDA          ?? DBNull.Value);
        cmd.Parameters.AddWithValue("st", (object?)d.STATUS_ENTRADA  ?? DBNull.Value);
        cmd.Parameters.AddWithValue("fs", (object?)d.FECHA_SALIDA    ?? DBNull.Value);
        cmd.Parameters.AddWithValue("de", (object?)d.DESTINO         ?? DBNull.Value);
        cmd.Parameters.AddWithValue("re", (object?)d.RESPONSABLE     ?? DBNull.Value);

        cmd.ExecuteNonQuery();
    }

    // ── UPDATE ────────────────────────────────────────────────────────────
    public void Update(int id, RegistroEntradaDto d, string? usuario = null)
    {
        // Capturar registro anterior para auditoría
        var anterior = GetById(id);

        using var con = Abrir();
        using var cmd = con.CreateCommand();

        cmd.CommandText = """
            UPDATE registro_entradas_temporal SET
                id_registro     = @ir,
                id_oc           = @io,
                serie           = @se,
                folio           = @fo,
                oc              = @oc,
                fecha_ingreso   = @fi,
                proveedor       = @pr,
                recibido_por    = @rp,
                categoria       = @ca,
                subcategoria    = @sc,
                tipo            = @ti,
                marca           = @ma,
                modelo          = @mo,
                ubicacion       = @ub,
                comentarios     = @co,
                precio_unitario = @pu,
                moneda          = @mn,
                status_entrada  = @st,
                fecha_salida    = @fs,
                destino         = @de,
                responsable     = @re
            WHERE id = @id
            """;

        cmd.Parameters.AddWithValue("ir", (object?)d.ID_REGISTRO     ?? DBNull.Value);
        cmd.Parameters.AddWithValue("io", (object?)d.ID_OC           ?? DBNull.Value);
        cmd.Parameters.AddWithValue("se", (object?)d.SERIE           ?? DBNull.Value);
        cmd.Parameters.AddWithValue("fo", (object?)d.FOLIO           ?? DBNull.Value);
        cmd.Parameters.AddWithValue("oc", (object?)d.OC              ?? DBNull.Value);
        cmd.Parameters.AddWithValue("fi", (object?)d.FECHA_INGRESO   ?? DBNull.Value);
        cmd.Parameters.AddWithValue("pr", (object?)d.PROVEEDOR       ?? DBNull.Value);
        cmd.Parameters.AddWithValue("rp", (object?)d.RECIBIDO_POR    ?? DBNull.Value);
        cmd.Parameters.AddWithValue("ca", (object?)d.CATEGORIA       ?? DBNull.Value);
        cmd.Parameters.AddWithValue("sc", (object?)d.SUBCATEGORIA    ?? DBNull.Value);
        cmd.Parameters.AddWithValue("ti", (object?)d.TIPO            ?? DBNull.Value);
        cmd.Parameters.AddWithValue("ma", (object?)d.MARCA           ?? DBNull.Value);
        cmd.Parameters.AddWithValue("mo", (object?)d.MODELO          ?? DBNull.Value);
        cmd.Parameters.AddWithValue("ub", (object?)d.UBICACION       ?? DBNull.Value);
        cmd.Parameters.AddWithValue("co", (object?)d.COMENTARIOS     ?? DBNull.Value);
        cmd.Parameters.AddWithValue("pu", (object?)d.PRECIO_UNITARIO ?? DBNull.Value);
        cmd.Parameters.AddWithValue("mn", (object?)d.MONEDA          ?? DBNull.Value);
        cmd.Parameters.AddWithValue("st", (object?)d.STATUS_ENTRADA  ?? DBNull.Value);
        cmd.Parameters.AddWithValue("fs", (object?)d.FECHA_SALIDA    ?? DBNull.Value);
        cmd.Parameters.AddWithValue("de", (object?)d.DESTINO         ?? DBNull.Value);
        cmd.Parameters.AddWithValue("re", (object?)d.RESPONSABLE     ?? DBNull.Value);
        cmd.Parameters.AddWithValue("id", id);

        cmd.ExecuteNonQuery();

        // ── Auditoría ──────────────────────────────────────────────────────
        if (anterior != null)
            GuardarAuditoria(id, anterior, d, usuario);
    }

    // ── DELETE ────────────────────────────────────────────────────────────
    public void Delete(int id)
    {
        using var con = Abrir();
        using var cmd = con.CreateCommand();

        cmd.CommandText = "DELETE FROM registro_entradas_temporal WHERE id = @id";
        cmd.Parameters.AddWithValue("id", id);
        cmd.ExecuteNonQuery();
    }

    // ── PDF ───────────────────────────────────────────────────────────────
    public void GuardarRutaPDF(int id, string ruta)
    {
        using var con = Abrir();
        using var cmd = con.CreateCommand();
        cmd.CommandText = "UPDATE registro_entradas_temporal SET pdf = @ruta WHERE id = @id";
        cmd.Parameters.AddWithValue("ruta", ruta);
        cmd.Parameters.AddWithValue("id", id);
        cmd.ExecuteNonQuery();
    }

    public string? ObtenerRutaPDF(int id)
    {
        using var con = Abrir();
        using var cmd = con.CreateCommand();
        cmd.CommandText = "SELECT pdf FROM registro_entradas_temporal WHERE id = @id";
        cmd.Parameters.AddWithValue("id", id);
        var r = cmd.ExecuteScalar();
        return r is DBNull or null ? null : (string)r;
    }

    public void EliminarPDF(int id)
    {
        using var con = Abrir();
        using var cmd = con.CreateCommand();
        cmd.CommandText = "UPDATE registro_entradas_temporal SET pdf = NULL WHERE id = @id";
        cmd.Parameters.AddWithValue("id", id);
        cmd.ExecuteNonQuery();
    }

    // ── EXCEL ─────────────────────────────────────────────────────────────
    public byte[] GenerarExcel(IEnumerable<RegistroEntradaRow> rows)
    {
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Registro Entradas");

        string[] headers =
        {
            "ID Registro","ID OC","Serie","Folio","OC","Fecha Ingreso",
            "Proveedor","Recibido Por","Categoría","Subcategoría","Tipo",
            "Marca","Modelo","Ubicación","Comentarios","Precio Unitario",
            "Moneda","Status Entrada","Fecha Salida","Destino","Responsable"
        };

        for (int i = 0; i < headers.Length; i++)
        {
            ws.Cell(1, i + 1).Value = headers[i];
            ws.Cell(1, i + 1).Style.Font.Bold = 1;
            ws.Cell(1, i + 1).Style.Fill.BackgroundColor = XLColor.FromHtml("#1a2235");
        }

        int row = 2;
        foreach (var r in rows)
        {
            ws.Cell(row, 1).Value  = r.ID_REGISTRO;
            ws.Cell(row, 2).Value  = r.ID_OC;
            ws.Cell(row, 3).Value  = r.SERIE;
            ws.Cell(row, 4).Value  = r.FOLIO;
            ws.Cell(row, 5).Value  = r.OC;
            ws.Cell(row, 6).Value  = r.FECHA_INGRESO;
            ws.Cell(row, 7).Value  = r.PROVEEDOR;
            ws.Cell(row, 8).Value  = r.RECIBIDO_POR;
            ws.Cell(row, 9).Value  = r.CATEGORIA;
            ws.Cell(row, 10).Value = r.SUBCATEGORIA;
            ws.Cell(row, 11).Value = r.TIPO;
            ws.Cell(row, 12).Value = r.MARCA;
            ws.Cell(row, 13).Value = r.MODELO;
            ws.Cell(row, 14).Value = r.UBICACION;
            ws.Cell(row, 15).Value = r.COMENTARIOS;
            ws.Cell(row, 16).Value = r.PRECIO_UNITARIO;
            ws.Cell(row, 17).Value = r.MONEDA;
            ws.Cell(row, 18).Value = r.STATUS_ENTRADA;
            ws.Cell(row, 19).Value = r.FECHA_SALIDA;
            ws.Cell(row, 20).Value = r.DESTINO;
            ws.Cell(row, 21).Value = r.RESPONSABLE;
            row++;
        }

        ws.Columns().AdjustToContents();

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    // ── GET POR AÑO ───────────────────────────────────────────────────────
    public List<RegistroEntradaRow> GetPorAnio(int anio)
    {
        using var con = Abrir();
        using var cmd = con.CreateCommand();

        cmd.CommandText = """
            SELECT id, id_registro, id_oc, serie, folio, oc,
                   fecha_ingreso, proveedor, recibido_por, categoria,
                   subcategoria, tipo, marca, modelo, ubicacion,
                   comentarios, precio_unitario, moneda, status_entrada,
                   fecha_salida, destino, responsable, pdf
            FROM registro_entradas_temporal
            WHERE EXTRACT(YEAR FROM fecha_ingreso) = @anio
            AND (activo IS NULL OR activo = 1)
            ORDER BY id DESC
            """;

        cmd.Parameters.AddWithValue("anio", anio);

        var list = new List<RegistroEntradaRow>();
        using var dr = cmd.ExecuteReader();

        while (dr.Read())
        {
            var ruta = dr.IsDBNull(22) ? null : dr.GetString(22);
            list.Add(new RegistroEntradaRow
            {
                ID               = dr.GetInt32(0),
                ID_REGISTRO      = dr.IsDBNull(1)  ? null : dr.GetString(1),
                ID_OC            = dr.IsDBNull(2)  ? null : dr.GetString(2),
                SERIE            = dr.IsDBNull(3)  ? null : dr.GetString(3),
                FOLIO            = dr.IsDBNull(4)  ? null : dr.GetString(4),
                OC               = dr.IsDBNull(5)  ? null : dr.GetString(5),
                FECHA_INGRESO    = dr.IsDBNull(6)  ? null : dr.GetValue(6).ToString(),
                PROVEEDOR        = dr.IsDBNull(7)  ? null : dr.GetString(7),
                RECIBIDO_POR     = dr.IsDBNull(8)  ? null : dr.GetString(8),
                CATEGORIA        = dr.IsDBNull(9)  ? null : dr.GetString(9),
                SUBCATEGORIA     = dr.IsDBNull(10) ? null : dr.GetString(10),
                TIPO             = dr.IsDBNull(11) ? null : dr.GetString(11),
                MARCA            = dr.IsDBNull(12) ? null : dr.GetString(12),
                MODELO           = dr.IsDBNull(13) ? null : dr.GetString(13),
                UBICACION        = dr.IsDBNull(14) ? null : dr.GetString(14),
                COMENTARIOS      = dr.IsDBNull(15) ? null : dr.GetString(15),
                PRECIO_UNITARIO  = dr.IsDBNull(16) ? null : dr.GetDecimal(16),
                MONEDA           = dr.IsDBNull(17) ? null : dr.GetString(17),
                STATUS_ENTRADA   = dr.IsDBNull(18) ? null : dr.GetString(18),
                FECHA_SALIDA     = dr.IsDBNull(19) ? null : dr.GetValue(19).ToString(),
                DESTINO          = dr.IsDBNull(20) ? null : dr.GetString(20),
                RESPONSABLE      = dr.IsDBNull(21) ? null : dr.GetString(21),
                PDF_RUTA         = ruta,
                TIENE_PDF        = ruta != null
            });
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
            FROM auditoria_registro_entradas_temporal
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
                id                 = dr.GetInt32(0),
                fecha              = dr.IsDBNull(1) ? null : dr.GetDateTime(1).ToString("yyyy-MM-dd HH:mm:ss"),
                usuario            = dr.IsDBNull(2) ? null : dr.GetString(2),
                registro_anterior  = dr.IsDBNull(3) ? null : dr.GetString(3),
                registro_nuevo     = dr.IsDBNull(4) ? null : dr.GetString(4)
            });
        }

        return list;
    }

    // ── AUDITORÍA ─────────────────────────────────────────────────────────
    private void GuardarAuditoria(int id, RegistroEntradaRow anterior, RegistroEntradaDto nuevo, string? usuario)
    {
        using var con = Abrir();
        using var cmd = con.CreateCommand();

        cmd.CommandText = """
            INSERT INTO auditoria_registro_entradas_temporal
            (registro_id, fecha_cambio, usuario, registro_anterior, registro_nuevo)
            VALUES (@rid, GETDATE(), @usr, @ant, @nvo)
            """;

        var antJson = System.Text.Json.JsonSerializer.Serialize(anterior);
        var nvoJson = System.Text.Json.JsonSerializer.Serialize(nuevo);

        cmd.Parameters.AddWithValue("rid", id);
        cmd.Parameters.AddWithValue("usr", (object?)usuario ?? DBNull.Value);
        cmd.Parameters.AddWithValue("ant", antJson);
        cmd.Parameters.AddWithValue("nvo", nvoJson);

        cmd.ExecuteNonQuery();
    }
}
