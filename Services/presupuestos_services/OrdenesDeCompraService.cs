using ChiIT.Data;
using Npgsql;
using ClosedXML.Excel;

public class OrdenDeCompraDto
{
    public string? ORDEN_DE_COMPRA { get; set; }
    public string? FOLIO { get; set; }
    public string? SOLICITANTE { get; set; }
    public string? PRESUPUESTO_MES { get; set; }
    public string? SERIE_UBICACION_NO_EMPLEADO { get; set; }
    public string? ACCESORIO_SOLICITADO { get; set; }
    public string? PROVEEDOR_ELEGIDO { get; set; }
    public string? PIEZA_SERVICIO { get; set; }
    public decimal? CANTIDAD { get; set; }
    public decimal? PRECIO_UNITARIO { get; set; }
    public decimal? TOTAL_SIN_IVA { get; set; }
    public string? MONEDA { get; set; }
    public string? COMENTARIOS { get; set; }
    public string? HOJA_CONTROL { get; set; }
    public string? REQUISICION { get; set; }
    public string? FECHA_OC { get; set; }
    public string? OC { get; set; }
    public string? FECHA_ENTRADA { get; set; }
    public decimal? CANTIDAD_REGISTRADA { get; set; }
    public string? ESTATUS_OC { get; set; }
}

public class OrdenDeCompraRow : OrdenDeCompraDto
{
    public int ID { get; set; }
    public bool TIENE_PDF { get; set; }
    public string? PDF_RUTA { get; set; }
}

public class OrdenesDeCompraService
{
    private readonly DbConnectionPool _db;

    public OrdenesDeCompraService(DbConnectionPool db) => _db = db;

    private NpgsqlConnection Abrir() => _db.Open();

    private static object ParseFecha(string? valor)
    {
        if (string.IsNullOrWhiteSpace(valor)) return DBNull.Value;
        return DateTime.TryParse(valor, out var dt) ? (object)dt : DBNull.Value;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // MAPEO CANÓNICO: normaliza cualquier alias de hoja al nombre oficial.
    // Es public static para que otros Services puedan llamarlo directamente.
    // ═══════════════════════════════════════════════════════════════════════
    public static string? NormalizarHojaControl(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        return raw.Trim().ToUpperInvariant() switch
        {
            "ACCESORIO NF"
            or "ACCESORIOS NF" => "Accesorio NF",

            "CONSUMIBLES NF"
            or "CONSUMIBLE NF" => "Consumibles NF",

            "DISPOSITIVOS NF"
            or "DISPOSITIVO NF" => "Dispositivos NF",

            "IMPRESORAS NF"
            or "IMPRESORA NF" => "IMPRESORAS NF",

            "PANTALLAS NF"
            or "PANTALLA NF" => "PANTALLAS NF",

            "PERIFERICOS NF"
            or "PERIFERICO NF"
            or "PERIFÉRICOS NF" => "PERIFERICOS NF",

            "REFACCIONES NF"
            or "REFACCION NF"
            or "REFACCIONES" => "Refacciones NF",

            "RADIO NF"
            or "RADIOS NF"
            or "RADIOS" => "Radio NF",

            "HERRAMIENTA NF"
            or "HERRAMIENTAS NF"
            or "HERRAMIENTAS" => "HERRAMIENTAS NF",

            "EQUIPO DE RED"
            or "EQUIPO RED"
            or "EQUIPOS DE RED" => "EQUIPO DE RED",

            "CAMARAS_AUDIO"
            or "CAMARAS AUDIO"
            or "CÁMARAS AUDIO"
            or "CAMARA AUDIO" => "CAMARAS AUDIO",

            "FIRECOM"
            or "BITACORA FIRECOM"
            or "BITÁCORA FIRECOM" => "BITACORA FIRECOM",

            "TINTAS,TONER,RIBON NF"
            or "TINTAS TONER RIBON NF"
            or "TINTAS TONER RIBON" => "Tintas,Toner,Ribon NF",

            "SERVICIOS POR PROVEEDORES NF"
            or "SERVICIOS POR PROVEEDORES"
            or "SERVICIOS PROVEEDORES" => "Servicios por Proveedores NF",

            "INVENTARIOS NF"
            or "INVENTARIO NF"
            or "INVENTARIOS" => "Inventarios NF",

            _ => null
        };
    }

    // ═══════════════════════════════════════════════════════════════════════
    // FÓRMULA: CANTIDAD REGISTRADA
    // Equivale a:
    //   =SUMAR.SI.CONJUNTO(INDIRECTO("'"&N&"'!J:J"),
    //                      INDIRECTO("'"&N&"'!A:A"),
    //                      A&B)
    //
    // Mapeo verificado contra esquema real de la DB:
    //  Hoja canónica          Tabla PG               col OC   col folio        col cantidad
    //  ─────────────────────────────────────────────────────────────────────────────────────
    //  Accesorio NF           accesorios_nf           oc       folio            cantidad
    //  Consumibles NF         consumibles_nf          oc       folio_cantidad   cantidad
    //  Dispositivos NF        dispositivos_nf         oc       folio            cantidad
    //  IMPRESORAS NF          impresoras_nf           oc       folio_inventario cantidad
    //  PANTALLAS NF           pantallas_nf            oc       folio            cantidad
    //  PERIFERICOS NF         perifericos_nf          oc       folio_inventario cantidad
    //  Refacciones NF         refacciones_nf          oc       folio_correctivo cantidad
    //  Radio NF               radios_nf               oc       folio            cantidad
    //  HERRAMIENTAS NF        herramientas_nf         oc       folio_correctivo cantidad
    //  EQUIPO DE RED          equipo_red_nf           oc       folio_correctivo cantidad
    //  CAMARAS AUDIO          camaras_audio           oc       folio_inventario cantidad
    //  BITACORA FIRECOM       bitacora_firecom         oc       orden_servicio   cantidad
    //  Tintas,Toner,Ribon NF  tintas_toner_ribon_nf   oc       —                cantidad_recibida
    //  Inventarios NF         inventarios_nf          oc       inv_folio        cantidad
    //  Servicios Proveedores  servicios_proveedores    —        —                —  → 0
    // ═══════════════════════════════════════════════════════════════════════
    private decimal SumarCantidadRegistrada(
        NpgsqlConnection con,
        string? ordenDeCompra,
        string? folio,
        string? hojaControl)
    {
        var hoja = NormalizarHojaControl(hojaControl);
        if (hoja == null) return 0;

        // Servicios no tiene OC → siempre 0
        if (hoja == "Servicios por Proveedores NF") return 0;

        using var cmd = con.CreateCommand();

        (string tabla, string colFolio, string colCantidad, bool usaFolio) cfg = hoja switch
        {
            "Accesorio NF" => ("accesorios_nf", "folio", "cantidad", true),
            "Consumibles NF" => ("consumibles_nf", "folio_cantidad", "cantidad", true),
            "Dispositivos NF" => ("dispositivos_nf", "folio", "cantidad", true),
            "IMPRESORAS NF" => ("impresoras_nf", "folio_inventario", "cantidad", true),
            "PANTALLAS NF" => ("pantallas_nf", "folio", "cantidad", true),
            "PERIFERICOS NF" => ("perifericos_nf", "folio_inventario", "cantidad", true),
            "Refacciones NF" => ("refacciones_nf", "folio_correctivo", "cantidad", true),
            "Radio NF" => ("radios_nf", "folio", "cantidad", true),
            "HERRAMIENTAS NF" => ("herramientas_nf", "folio_correctivo", "cantidad", true),
            "EQUIPO DE RED" => ("equipo_red_nf", "folio_correctivo", "cantidad", true),
            "CAMARAS AUDIO" => ("camaras_audio", "folio_inventario", "cantidad", true),
            "BITACORA FIRECOM" => ("bitacora_firecom", "orden_servicio", "cantidad", true),
            "Tintas,Toner,Ribon NF" => ("tintas_toner_ribon_nf", "", "cantidad_recibida", false),
            "Inventarios NF" => ("inventarios_nf", "inv_folio", "cantidad", true),
            _ => ("", "", "", false)
        };

        if (string.IsNullOrEmpty(cfg.tabla)) return 0;

        if (cfg.usaFolio)
        {
            cmd.CommandText = $"""
                SELECT COALESCE(SUM({cfg.colCantidad}), 0)
                FROM {cfg.tabla}
                WHERE oc = @odc
                  AND {cfg.colFolio} = @folio
                  AND (activo IS NULL OR activo = true)
                """;
            cmd.Parameters.AddWithValue("odc", (object?)ordenDeCompra ?? DBNull.Value);
            cmd.Parameters.AddWithValue("folio", (object?)folio ?? DBNull.Value);
        }
        else
        {
            // Solo OC (ej. Tintas)
            cmd.CommandText = $"""
                SELECT COALESCE(SUM({cfg.colCantidad}), 0)
                FROM {cfg.tabla}
                WHERE oc = @odc
                  AND (activo IS NULL OR activo = true)
                """;
            cmd.Parameters.AddWithValue("odc", (object?)ordenDeCompra ?? DBNull.Value);
        }

        var result = cmd.ExecuteScalar();
        return result is DBNull or null ? 0 : Convert.ToDecimal(result);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // RECALCULAR ORDENES AFECTADAS POR CAMBIO EN TABLA HIJA
    //
    // Llamar desde CUALQUIER servicio hijo cada vez que se modifique
    // la columna "cantidad" de un registro.
    //
    // Ejemplo de uso en AccesoriosNfService.Update():
    //   _ordenesService.RecalcularPorCambioEnHija("Accesorio NF", oc, folio);
    //
    // El método busca todas las ordenes_de_compra cuya (hoja_control, oc, folio)
    // coincida, recalcula cantidad_registrada y estatus_oc, y las actualiza.
    // ═══════════════════════════════════════════════════════════════════════
    public int RecalcularPorCambioEnHija(string hojaControl, string? oc, string? folio)
    {
        var hojaCanonica = NormalizarHojaControl(hojaControl) ?? hojaControl;

        using var con = Abrir();

        // 1. Encontrar todas las OC afectadas
        var afectadas = new List<(int id, decimal? cantidad)>();
        using (var cmd = con.CreateCommand())
        {
            cmd.CommandText = """
                SELECT id, cantidad
                FROM ordenes_de_compra
                WHERE (activo IS NULL OR activo = true)
                  AND hoja_control = @hc
                  AND oc           = @oc
                  AND folio        = @fo
                """;
            cmd.Parameters.AddWithValue("hc", hojaCanonica);
            cmd.Parameters.AddWithValue("oc", (object?)oc ?? DBNull.Value);
            cmd.Parameters.AddWithValue("fo", (object?)folio ?? DBNull.Value);

            using var dr = cmd.ExecuteReader();
            while (dr.Read())
                afectadas.Add((dr.GetInt32(0), dr.IsDBNull(1) ? null : dr.GetDecimal(1)));
        }

        // 2. Recalcular y actualizar cada una
        int actualizados = 0;
        foreach (var (id, cantidad) in afectadas)
        {
            decimal cantReg = SumarCantidadRegistrada(con, oc, folio, hojaCanonica);
            string estatus = CalcularEstatusOC(cantidad, cantReg);

            using var upd = con.CreateCommand();
            upd.CommandText = """
                UPDATE ordenes_de_compra
                SET cantidad_registrada = @cr,
                    estatus_oc          = @es
                WHERE id = @id
                """;
            upd.Parameters.AddWithValue("cr", cantReg);
            upd.Parameters.AddWithValue("es", estatus);
            upd.Parameters.AddWithValue("id", id);
            actualizados += upd.ExecuteNonQuery();
        }

        return actualizados;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // ESTATUS OC
    // =SI(I=S,"COMPLETA", SI(Y(S>0,S<I),"PARCIAL","PENDIENTE"))
    // ═══════════════════════════════════════════════════════════════════════
    private static string CalcularEstatusOC(decimal? cantidad, decimal cantidadRegistrada)
    {
        if (cantidad.HasValue && cantidad.Value > 0)
        {
            if (cantidadRegistrada >= cantidad.Value) return "COMPLETA";
            if (cantidadRegistrada > 0) return "PARCIAL";
        }
        return "PENDIENTE";
    }

    // ═══════════════════════════════════════════════════════════════════════
    // BUSCARV en Req VS OC
    // ═══════════════════════════════════════════════════════════════════════
    private (string? ordenDeCompra, string? fechaOc) BuscarEnReqVsOC(NpgsqlConnection con, string? requisicion)
    {
        if (string.IsNullOrWhiteSpace(requisicion)) return (null, null);

        using var cmd = con.CreateCommand();
        cmd.CommandText = """
            SELECT orden_compra, fecha_compra
            FROM req_vs_oc
            WHERE no_requisicion = @req
              AND (activo IS NULL OR activo = true)
            LIMIT 1
            """;
        cmd.Parameters.AddWithValue("req", requisicion.Trim());

        using var dr = cmd.ExecuteReader();
        if (!dr.Read()) return (null, null);

        string? oc = dr.IsDBNull(0) ? null : dr.GetString(0);
        string? foc = dr.IsDBNull(1) ? null : ((DateTime)dr.GetValue(1)).ToString("yyyy-MM-dd");

        if (oc == "0" || string.IsNullOrWhiteSpace(oc)) oc = null;
        return (oc, foc);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // GET PAGINADO + FILTROS
    // ═══════════════════════════════════════════════════════════════════════
    public (List<OrdenDeCompraRow> data, int total) GetAll(
        int page = 1, int limit = 10,
        string? ORDEN_DE_COMPRA = null, string? FOLIO = null,
        string? SOLICITANTE = null, string? PRESUPUESTO_MES = null,
        string? SERIE_UBICACION_NO_EMPLEADO = null, string? ACCESORIO_SOLICITADO = null,
        string? PROVEEDOR_ELEGIDO = null, string? PIEZA_SERVICIO = null,
        string? MONEDA = null, string? REQUISICION = null,
        string? OC = null, string? ESTATUS_OC = null)
    {
        using var con = Abrir();

        var where = new List<string> { "(activo IS NULL OR activo = true)" };
        var parms = new List<(string name, string value)>();

        void F(string col, string? val, string p)
        {
            if (!string.IsNullOrWhiteSpace(val))
            { where.Add($"{col} ILIKE @{p}"); parms.Add((p, $"%{val}%")); }
        }

        F("orden_de_compra", ORDEN_DE_COMPRA, "odc");
        F("folio", FOLIO, "fo");
        F("solicitante", SOLICITANTE, "sol");
        F("presupuesto_mes", PRESUPUESTO_MES, "pm");
        F("serie_ubicacion_no_empleado", SERIE_UBICACION_NO_EMPLEADO, "su");
        F("accesorio_solicitado", ACCESORIO_SOLICITADO, "ac");
        F("proveedor_elegido", PROVEEDOR_ELEGIDO, "pr");
        F("pieza_servicio", PIEZA_SERVICIO, "ps");
        F("moneda", MONEDA, "mo");
        F("requisicion", REQUISICION, "re");
        F("oc", OC, "oc");
        F("estatus_oc", ESTATUS_OC, "es");

        string filtro = "WHERE " + string.Join(" AND ", where);

        int total;
        using (var c = con.CreateCommand())
        {
            c.CommandText = $"SELECT COUNT(*) FROM ordenes_de_compra {filtro}";
            foreach (var (n, v) in parms) c.Parameters.AddWithValue(n, v);
            total = Convert.ToInt32(c.ExecuteScalar());
        }

        int offset = (page - 1) * limit;
        var list = new List<OrdenDeCompraRow>();

        using (var c = con.CreateCommand())
        {
            c.CommandText = $"""
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
            foreach (var (n, v) in parms) c.Parameters.AddWithValue(n, v);
            using var dr = c.ExecuteReader();
            while (dr.Read()) list.Add(MapRow(dr));
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
            FROM ordenes_de_compra WHERE id = @id
            """;
        cmd.Parameters.AddWithValue("id", id);
        using var dr = cmd.ExecuteReader();
        if (!dr.Read()) return null;
        return MapRow(dr);
    }

    // ── MAP ROW ───────────────────────────────────────────────────────────
    private static OrdenDeCompraRow MapRow(NpgsqlDataReader dr)
    {
        string? ruta = dr.IsDBNull(21) ? null : dr.GetString(21);
        return new OrdenDeCompraRow
        {
            ID = dr.GetInt32(0),
            ORDEN_DE_COMPRA = dr.IsDBNull(1) ? null : dr.GetString(1),
            FOLIO = dr.IsDBNull(2) ? null : dr.GetString(2),
            SOLICITANTE = dr.IsDBNull(3) ? null : dr.GetString(3),
            PRESUPUESTO_MES = dr.IsDBNull(4) ? null : dr.GetString(4),
            SERIE_UBICACION_NO_EMPLEADO = dr.IsDBNull(5) ? null : dr.GetString(5),
            ACCESORIO_SOLICITADO = dr.IsDBNull(6) ? null : dr.GetString(6),
            PROVEEDOR_ELEGIDO = dr.IsDBNull(7) ? null : dr.GetString(7),
            PIEZA_SERVICIO = dr.IsDBNull(8) ? null : dr.GetString(8),
            CANTIDAD = dr.IsDBNull(9) ? null : dr.GetDecimal(9),
            PRECIO_UNITARIO = dr.IsDBNull(10) ? null : dr.GetDecimal(10),
            TOTAL_SIN_IVA = dr.IsDBNull(11) ? null : dr.GetDecimal(11),
            MONEDA = dr.IsDBNull(12) ? null : dr.GetString(12),
            COMENTARIOS = dr.IsDBNull(13) ? null : dr.GetString(13),
            HOJA_CONTROL = dr.IsDBNull(14) ? null : dr.GetString(14),
            REQUISICION = dr.IsDBNull(15) ? null : dr.GetString(15),
            FECHA_OC = dr.IsDBNull(16) ? null : ((DateTime)dr.GetValue(16)).ToString("yyyy-MM-dd"),
            OC = dr.IsDBNull(17) ? null : dr.GetString(17),
            FECHA_ENTRADA = dr.IsDBNull(18) ? null : ((DateTime)dr.GetValue(18)).ToString("yyyy-MM-dd"),
            CANTIDAD_REGISTRADA = dr.IsDBNull(19) ? null : dr.GetDecimal(19),
            ESTATUS_OC = dr.IsDBNull(20) ? null : dr.GetString(20),
            PDF_RUTA = ruta,
            TIENE_PDF = ruta != null
        };
    }

    // ═══════════════════════════════════════════════════════════════════════
    // CREATE
    // ═══════════════════════════════════════════════════════════════════════
    public void Create(OrdenDeCompraDto d, string? usuario = null)
    {
        using var con = Abrir();

        string? hojaControl = NormalizarHojaControl(d.HOJA_CONTROL) ?? d.HOJA_CONTROL;
        decimal? total = (d.CANTIDAD.HasValue && d.PRECIO_UNITARIO.HasValue)
                                 ? d.CANTIDAD.Value * d.PRECIO_UNITARIO.Value
                                 : d.TOTAL_SIN_IVA;

        var (ocLookup, fechaOcLookup) = BuscarEnReqVsOC(con, d.REQUISICION);
        string? ordenDeCompra = !string.IsNullOrWhiteSpace(d.ORDEN_DE_COMPRA) ? d.ORDEN_DE_COMPRA : ocLookup;
        string? fechaOc = !string.IsNullOrWhiteSpace(d.FECHA_OC) ? d.FECHA_OC : fechaOcLookup;
        string? oc = !string.IsNullOrWhiteSpace(d.OC) ? d.OC : ocLookup;

        // Aplica la fórmula solo si hay oc + folio + hoja_control reconocida.
        // Si faltan datos no sobreescribe: respeta lo que venga en el DTO o deja 0.
        bool puedeCalcularCreate = !string.IsNullOrWhiteSpace(ordenDeCompra)
                                && !string.IsNullOrWhiteSpace(d.FOLIO)
                                && NormalizarHojaControl(hojaControl) != null;

        decimal cantReg = puedeCalcularCreate
            ? SumarCantidadRegistrada(con, ordenDeCompra, d.FOLIO, hojaControl)
            : (d.CANTIDAD_REGISTRADA ?? 0);

        string estatus = CalcularEstatusOC(d.CANTIDAD, cantReg);

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

        Bind(cmd, ordenDeCompra, d, hojaControl, total, fechaOc, oc, cantReg, estatus);
        cmd.ExecuteNonQuery();
    }

    // ═══════════════════════════════════════════════════════════════════════
    // UPDATE
    // ═══════════════════════════════════════════════════════════════════════
    public void Update(int id, OrdenDeCompraDto d, string? usuario = null)
    {
        var anterior = GetById(id);
        using var con = Abrir();

        string? hojaControl = NormalizarHojaControl(d.HOJA_CONTROL) ?? d.HOJA_CONTROL;
        decimal? total = (d.CANTIDAD.HasValue && d.PRECIO_UNITARIO.HasValue)
                                 ? d.CANTIDAD.Value * d.PRECIO_UNITARIO.Value
                                 : d.TOTAL_SIN_IVA;

        var (ocLookup, fechaOcLookup) = BuscarEnReqVsOC(con, d.REQUISICION);
        string? ordenDeCompra = !string.IsNullOrWhiteSpace(d.ORDEN_DE_COMPRA) ? d.ORDEN_DE_COMPRA : ocLookup;
        string? fechaOc = !string.IsNullOrWhiteSpace(d.FECHA_OC) ? d.FECHA_OC : fechaOcLookup;
        string? oc = !string.IsNullOrWhiteSpace(d.OC) ? d.OC : ocLookup;

        // Aplica la fórmula solo si hay oc + folio + hoja_control reconocida.
        // Si faltan datos respeta el valor que ya tiene el registro en BD (anterior).
        bool puedeCalcularUpdate = !string.IsNullOrWhiteSpace(ordenDeCompra)
                                && !string.IsNullOrWhiteSpace(d.FOLIO)
                                && NormalizarHojaControl(hojaControl) != null;

        decimal cantReg = puedeCalcularUpdate
            ? SumarCantidadRegistrada(con, ordenDeCompra, d.FOLIO, hojaControl)
            : (anterior?.CANTIDAD_REGISTRADA ?? d.CANTIDAD_REGISTRADA ?? 0);

        string estatus = CalcularEstatusOC(d.CANTIDAD, cantReg);

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

        Bind(cmd, ordenDeCompra, d, hojaControl, total, fechaOc, oc, cantReg, estatus);
        cmd.Parameters.AddWithValue("id", id);
        cmd.ExecuteNonQuery();

        if (anterior != null)
            GuardarAuditoria(id, anterior, d, usuario);
    }

    // ── Utilidad: BindParameters compartido entre Create y Update ─────────
    private static void Bind(
        NpgsqlCommand cmd,
        string? ordenDeCompra, OrdenDeCompraDto d, string? hojaControl,
        decimal? total, string? fechaOc, string? oc,
        decimal cantReg, string estatus)
    {
        cmd.Parameters.AddWithValue("odc", (object?)ordenDeCompra ?? DBNull.Value);
        cmd.Parameters.AddWithValue("fo", (object?)d.FOLIO ?? DBNull.Value);
        cmd.Parameters.AddWithValue("sol", (object?)d.SOLICITANTE ?? DBNull.Value);
        cmd.Parameters.AddWithValue("pm", (object?)d.PRESUPUESTO_MES ?? DBNull.Value);
        cmd.Parameters.AddWithValue("su", (object?)d.SERIE_UBICACION_NO_EMPLEADO ?? DBNull.Value);
        cmd.Parameters.AddWithValue("ac", (object?)d.ACCESORIO_SOLICITADO ?? DBNull.Value);
        cmd.Parameters.AddWithValue("pr", (object?)d.PROVEEDOR_ELEGIDO ?? DBNull.Value);
        cmd.Parameters.AddWithValue("ps", (object?)d.PIEZA_SERVICIO ?? DBNull.Value);
        cmd.Parameters.AddWithValue("ca", (object?)d.CANTIDAD ?? DBNull.Value);
        cmd.Parameters.AddWithValue("pu", (object?)d.PRECIO_UNITARIO ?? DBNull.Value);
        cmd.Parameters.AddWithValue("tot", (object?)total ?? DBNull.Value);
        cmd.Parameters.AddWithValue("mo", (object?)d.MONEDA ?? DBNull.Value);
        cmd.Parameters.AddWithValue("co", (object?)d.COMENTARIOS ?? DBNull.Value);
        cmd.Parameters.AddWithValue("hc", (object?)hojaControl ?? DBNull.Value);
        cmd.Parameters.AddWithValue("re", (object?)d.REQUISICION ?? DBNull.Value);
        cmd.Parameters.AddWithValue("foc", ParseFecha(fechaOc));
        cmd.Parameters.AddWithValue("oc", (object?)oc ?? DBNull.Value);
        cmd.Parameters.AddWithValue("fen", ParseFecha(d.FECHA_ENTRADA));
        cmd.Parameters.AddWithValue("cr", cantReg);
        cmd.Parameters.AddWithValue("es", estatus);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // RECALCULAR TODO (endpoint POST /ORDENES_DE_COMPRA/RECALCULAR)
    // ═══════════════════════════════════════════════════════════════════════
    public int RecalcularFormulas()
    {
        using var con = Abrir();

        var todos = new List<(int id, string? req, string? odc, string? folio,
                               string? foc, string? oc, decimal? cant,
                               decimal? pu, string? hoja, decimal? cantRegActual)>();

        using (var cmd = con.CreateCommand())
        {
            cmd.CommandText = """
                SELECT id, requisicion, orden_de_compra, folio, fecha_oc, oc,
                       cantidad, precio_unitario, hoja_control, cantidad_registrada
                FROM ordenes_de_compra
                WHERE (activo IS NULL OR activo = true)
                ORDER BY id
                """;
            using var dr = cmd.ExecuteReader();
            while (dr.Read())
                todos.Add((
                    dr.GetInt32(0),
                    dr.IsDBNull(1) ? null : dr.GetString(1),
                    dr.IsDBNull(2) ? null : dr.GetString(2),
                    dr.IsDBNull(3) ? null : dr.GetString(3),
                    dr.IsDBNull(4) ? null : ((DateTime)dr.GetValue(4)).ToString("yyyy-MM-dd"),
                    dr.IsDBNull(5) ? null : dr.GetString(5),
                    dr.IsDBNull(6) ? null : dr.GetDecimal(6),
                    dr.IsDBNull(7) ? null : dr.GetDecimal(7),
                    dr.IsDBNull(8) ? null : dr.GetString(8),
                    dr.IsDBNull(9) ? null : dr.GetDecimal(9)
                ));
        }

        int actualizados = 0;
        foreach (var r in todos)
        {
            var (ocLookup, fechaOcLookup) = BuscarEnReqVsOC(con, r.req);

            string? odc = !string.IsNullOrWhiteSpace(r.odc) ? r.odc : ocLookup;
            string? foc = !string.IsNullOrWhiteSpace(r.foc) ? r.foc : fechaOcLookup;
            string? oc = !string.IsNullOrWhiteSpace(r.oc) ? r.oc : ocLookup;
            string? hoja = NormalizarHojaControl(r.hoja) ?? r.hoja;
            decimal? total = (r.cant.HasValue && r.pu.HasValue) ? r.cant.Value * r.pu.Value : null;

            // Solo recalcula si tiene los 3 datos que requiere la fórmula.
            // Si no, deja cantidad_registrada y estatus_oc tal como están en BD.
            bool puedeCalcular = !string.IsNullOrWhiteSpace(odc)
                              && !string.IsNullOrWhiteSpace(r.folio)
                              && NormalizarHojaControl(hoja) != null;

            if (!puedeCalcular) continue;

            decimal cantReg = SumarCantidadRegistrada(con, odc, r.folio, hoja);
            string estatus = CalcularEstatusOC(r.cant, cantReg);

            using var upd = con.CreateCommand();
            upd.CommandText = """
                UPDATE ordenes_de_compra SET
                    orden_de_compra     = COALESCE(@odc, orden_de_compra),
                    fecha_oc            = COALESCE(@foc, fecha_oc),
                    oc                  = COALESCE(@oc,  oc),
                    total_sin_iva       = COALESCE(@tot, total_sin_iva),
                    hoja_control        = COALESCE(@hc,  hoja_control),
                    cantidad_registrada = @cr,
                    estatus_oc          = @es
                WHERE id = @id
                """;
            upd.Parameters.AddWithValue("odc", (object?)odc ?? DBNull.Value);
            upd.Parameters.AddWithValue("foc", ParseFecha(foc));
            upd.Parameters.AddWithValue("oc", (object?)oc ?? DBNull.Value);
            upd.Parameters.AddWithValue("tot", (object?)total ?? DBNull.Value);
            upd.Parameters.AddWithValue("hc", (object?)hoja ?? DBNull.Value);
            upd.Parameters.AddWithValue("cr", cantReg);
            upd.Parameters.AddWithValue("es", estatus);
            upd.Parameters.AddWithValue("id", r.id);
            actualizados += upd.ExecuteNonQuery();
        }

        return actualizados;
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

    // ── EXPORTAR EXCEL ────────────────────────────────────────────────────
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
            ws.Cell(row, 1).Value = r.ID;
            ws.Cell(row, 2).Value = r.ORDEN_DE_COMPRA;
            ws.Cell(row, 3).Value = r.FOLIO;
            ws.Cell(row, 4).Value = r.SOLICITANTE;
            ws.Cell(row, 5).Value = r.PRESUPUESTO_MES;
            ws.Cell(row, 6).Value = r.SERIE_UBICACION_NO_EMPLEADO;
            ws.Cell(row, 7).Value = r.ACCESORIO_SOLICITADO;
            ws.Cell(row, 8).Value = r.PROVEEDOR_ELEGIDO;
            ws.Cell(row, 9).Value = r.PIEZA_SERVICIO;
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
            WHERE (activo IS NULL OR activo = true)
              AND EXTRACT(YEAR FROM fecha_oc::date) = @anio
            ORDER BY id DESC
            """;
        cmd.Parameters.AddWithValue("anio", anio);
        var list = new List<OrdenDeCompraRow>();
        using var dr = cmd.ExecuteReader();
        while (dr.Read()) list.Add(MapRow(dr));
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
                id = dr.GetInt32(0),
                fecha = dr.IsDBNull(1) ? null : dr.GetDateTime(1).ToString("yyyy-MM-dd HH:mm:ss"),
                usuario = dr.IsDBNull(2) ? null : dr.GetString(2),
                registro_anterior = dr.IsDBNull(3) ? null : dr.GetString(3),
                registro_nuevo = dr.IsDBNull(4) ? null : dr.GetString(4)
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

    // ── SOLICITANTES ÚNICOS ───────────────────────────────────────────────
    public List<string> GetSolicitantesUnicos()
    {
        using var con = Abrir();
        using var cmd = con.CreateCommand();
        cmd.CommandText = """
            SELECT DISTINCT solicitante
            FROM ordenes_de_compra
            WHERE solicitante IS NOT NULL AND solicitante <> ''
              AND (activo IS NULL OR activo = true)
            ORDER BY solicitante
            """;
        var lista = new List<string>();
        using var dr = cmd.ExecuteReader();
        while (dr.Read()) lista.Add(dr.GetString(0));
        return lista;
    }
}