using ChiIT.Data;
using Npgsql;

using OfficeOpenXml;
using OfficeOpenXml.Style;

using System.Drawing;

namespace ChiIT.Services;

// ── DTOs / Modelos ────────────────────────────────────────────────────────────

public class DispositivoNFDto
{
    public string?  ID_UNICO                    { get; set; }
    public string?  OC                          { get; set; }
    public string?  FOLIO                       { get; set; }
    public string?  FECHA_REGISTRO              { get; set; }
    public string?  RECIBIDO_POR                { get; set; }
    public string?  SUBCATEGORIA                { get; set; }
    public string?  MARCA                       { get; set; }
    public string?  MODELO                      { get; set; }
    public string?  NUMERO_SERIE                { get; set; }
    public int?     CANTIDAD                    { get; set; }
    public decimal? COSTO                       { get; set; }
    public string?  PROVEEDOR                   { get; set; }
    public string?  ACTIVO_FIJO                 { get; set; }
    public string?  PROCESADOR                  { get; set; }
    public string?  ARQUITECTURA                { get; set; }
    public string?  ALMACENAMIENTO              { get; set; }
    public string?  TIPO_DISCO_DURO             { get; set; }
    public string?  SISTEMA_OPERATIVO           { get; set; }
    public string?  LICENCIA_SISTEMA_OPERATIVO  { get; set; }
    public string?  MEMORIA_RAM                 { get; set; }
    public string?  VELOCIDAD_MEMORIA           { get; set; }
    public string?  TIPO_MEMORIA                { get; set; }
    public string?  SLOT_MEMORIA                { get; set; }
    public string?  MAX_MEMORIA                 { get; set; }
    public string?  MODELO_CARGADOR             { get; set; }
    public string?  NO_SERIE_ELIMINADOR         { get; set; }
    public string?  BATERIA_LAPTOP              { get; set; }
    public string?  WIFI_MAC                    { get; set; }
    public string?  ETH_MAC                     { get; set; }
    public string?  ACCESORIOS                  { get; set; }
    public string?  UBICACION                   { get; set; }
    public string?  EDIFICIO                    { get; set; }
    public string?  FECHA_SALIDA                { get; set; }
    public string?  ASIGNADO_A                  { get; set; }
    public string?  DESTINO_PLANTA              { get; set; }
    public bool?    DISPONIBLE                  { get; set; }
    public string?  PERSONAL_IT_QUE_ASIGNA      { get; set; }
    public string?  FORMATO_DE_BAJA             { get; set; }
}

public class DispositivoNFFiltros
{
    public string? ID_UNICO                    { get; set; }
    public string? OC                          { get; set; }
    public string? FOLIO                       { get; set; }
    public string? FECHA_REGISTRO              { get; set; }
    public string? RECIBIDO_POR                { get; set; }
    public string? SUBCATEGORIA                { get; set; }
    public string? MARCA                       { get; set; }
    public string? MODELO                      { get; set; }
    public string? NUMERO_SERIE                { get; set; }
    public string? PROVEEDOR                   { get; set; }
    public string? ACTIVO_FIJO                 { get; set; }
    public string? PROCESADOR                  { get; set; }
    public string? ARQUITECTURA                { get; set; }
    public string? ALMACENAMIENTO              { get; set; }
    public string? TIPO_DISCO_DURO             { get; set; }
    public string? SISTEMA_OPERATIVO           { get; set; }
    public string? LICENCIA_SISTEMA_OPERATIVO  { get; set; }
    public string? MEMORIA_RAM                 { get; set; }
    public string? TIPO_MEMORIA                { get; set; }
    public string? UBICACION                   { get; set; }
    public string? EDIFICIO                    { get; set; }
    public string? DISPONIBLE                  { get; set; }
    public string? ASIGNADO_A                  { get; set; }
    public string? DESTINO_PLANTA              { get; set; }
    public string? PERSONAL_IT_QUE_ASIGNA      { get; set; }
    public string? FORMATO_DE_BAJA             { get; set; }
}

// ── Servicio ──────────────────────────────────────────────────────────────────

public class DispositivosNFService
{
    private readonly DbConnectionPool _pool;
    private readonly OrdenesDeCompraService _ordenesService;

    private static readonly string[] COLS =
    [
        "ID_UNICO","OC","FOLIO","FECHA_REGISTRO","RECIBIDO_POR",
        "SUBCATEGORIA","MARCA","MODELO","NUMERO_SERIE","CANTIDAD",
        "COSTO","PROVEEDOR","ACTIVO_FIJO","PROCESADOR","ARQUITECTURA",
        "ALMACENAMIENTO","TIPO_DISCO_DURO","SISTEMA_OPERATIVO","LICENCIA_SISTEMA_OPERATIVO",
        "MEMORIA_RAM","VELOCIDAD_MEMORIA","TIPO_MEMORIA","SLOT_MEMORIA","MAX_MEMORIA",
        "MODELO_CARGADOR","NO_SERIE_ELIMINADOR","BATERIA_LAPTOP","WIFI_MAC","ETH_MAC",
        "ACCESORIOS","UBICACION","EDIFICIO","FECHA_SALIDA","ASIGNADO_A",
        "DESTINO_PLANTA","DISPONIBLE","PERSONAL_IT_QUE_ASIGNA","FORMATO_DE_BAJA"
    ];

    public DispositivosNFService(DbConnectionPool pool, OrdenesDeCompraService ordenesService)
    {
        _pool = pool;
        _ordenesService = ordenesService;
        _ = InicializarTablaAsync();
    }

    // ── DDL ───────────────────────────────────────────────────────────────
    private async Task InicializarTablaAsync()
    {
        await using var conn = await _pool.OpenAsync();
        await using var cmd = new NpgsqlCommand("""
            CREATE TABLE IF NOT EXISTS dispositivos_nf (
                id                          SERIAL PRIMARY KEY,
                id_unico                    TEXT,
                oc                          TEXT,
                folio                       TEXT,
                fecha_registro              DATE,
                recibido_por                TEXT,
                subcategoria                TEXT,
                marca                       TEXT,
                modelo                      TEXT,
                numero_serie                TEXT,
                cantidad                    INTEGER,
                costo                       NUMERIC(12,2),
                proveedor                   TEXT,
                activo_fijo                 TEXT,
                procesador                  TEXT,
                arquitectura                TEXT,
                almacenamiento              TEXT,
                tipo_disco_duro             TEXT,
                sistema_operativo           TEXT,
                licencia_sistema_operativo  TEXT,
                memoria_ram                 TEXT,
                velocidad_memoria           TEXT,
                tipo_memoria                TEXT,
                slot_memoria                TEXT,
                max_memoria                 TEXT,
                modelo_cargador             TEXT,
                no_serie_eliminador         TEXT,
                bateria_laptop              TEXT,
                wifi_mac                    TEXT,
                eth_mac                     TEXT,
                accesorios                  TEXT,
                ubicacion                   TEXT,
                edificio                    TEXT,
                fecha_salida                DATE,
                asignado_a                  TEXT,
                destino_planta              TEXT,
                disponible                  BOOLEAN,
                personal_it_que_asigna      TEXT,
                formato_de_baja             TEXT
            );

            CREATE TABLE IF NOT EXISTS dispositivos_nf_historial (
                id                  SERIAL PRIMARY KEY,
                dispositivo_id      INTEGER NOT NULL REFERENCES dispositivos_nf(id) ON DELETE CASCADE,
                usuario             TEXT    NOT NULL,
                fecha               TIMESTAMPTZ DEFAULT NOW(),
                registro_anterior   JSONB,
                registro_nuevo      JSONB
            );
            """, conn);

        await cmd.ExecuteNonQueryAsync();
    }

    // ── Listar (paginado + filtros) ───────────────────────────────────────
    public async Task<object> ListarAsync(int page, int limit, DispositivoNFFiltros f)
    {
        await using var conn = await _pool.OpenAsync();

        var (where, parms) = ConstruirWhere(f);
        var offset = (page - 1) * limit;

        // Total
        await using var cmdCount = new NpgsqlCommand(
            $"SELECT COUNT(*) FROM dispositivos_nf {where}", conn);
        foreach (var (k, v) in parms) cmdCount.Parameters.AddWithValue(k, v ?? (object)DBNull.Value);
        var total = Convert.ToInt64(await cmdCount.ExecuteScalarAsync());

        // Datos
        await using var cmdData = new NpgsqlCommand(
            $@"SELECT id, id_unico, oc, folio, fecha_registro,
                      recibido_por, subcategoria, marca, modelo, numero_serie,
                      cantidad, costo, proveedor, activo_fijo, procesador,
                      arquitectura, almacenamiento, tipo_disco_duro, sistema_operativo, licencia_sistema_operativo,
                      memoria_ram, velocidad_memoria, tipo_memoria, slot_memoria, max_memoria,
                      modelo_cargador, no_serie_eliminador, bateria_laptop, wifi_mac, eth_mac,
                      accesorios, ubicacion, edificio, fecha_salida, asignado_a,
                      destino_planta, disponible, personal_it_que_asigna, formato_de_baja
               FROM dispositivos_nf {where}
               ORDER BY id DESC
               LIMIT @lim OFFSET @off", conn);

        foreach (var (k, v) in parms) cmdData.Parameters.AddWithValue(k, v ?? (object)DBNull.Value);
        cmdData.Parameters.AddWithValue("lim", limit);
        cmdData.Parameters.AddWithValue("off", offset);

        var lista = new List<object>();
        await using var reader = await cmdData.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            lista.Add(new
            {
                ID                          = reader.GetInt32(0),
                ID_UNICO                    = Str(reader, 1),
                OC                          = Str(reader, 2),
                FOLIO                       = Str(reader, 3),
                FECHA_REGISTRO              = Str(reader, 4),
                RECIBIDO_POR                = Str(reader, 5),
                SUBCATEGORIA                = Str(reader, 6),
                MARCA                       = Str(reader, 7),
                MODELO                      = Str(reader, 8),
                NUMERO_SERIE                = Str(reader, 9),
                CANTIDAD                    = reader.IsDBNull(10) ? (int?)null     : reader.GetInt32(10),
                COSTO                       = reader.IsDBNull(11) ? (decimal?)null : reader.GetDecimal(11),
                PROVEEDOR                   = Str(reader, 12),
                ACTIVO_FIJO                 = Str(reader, 13),
                PROCESADOR                  = Str(reader, 14),
                ARQUITECTURA                = Str(reader, 15),
                ALMACENAMIENTO              = Str(reader, 16),
                TIPO_DISCO_DURO             = Str(reader, 17),
                SISTEMA_OPERATIVO           = Str(reader, 18),
                LICENCIA_SISTEMA_OPERATIVO  = Str(reader, 19),
                MEMORIA_RAM                 = Str(reader, 20),
                VELOCIDAD_MEMORIA           = Str(reader, 21),
                TIPO_MEMORIA                = Str(reader, 22),
                SLOT_MEMORIA                = Str(reader, 23),
                MAX_MEMORIA                 = Str(reader, 24),
                MODELO_CARGADOR             = Str(reader, 25),
                NO_SERIE_ELIMINADOR         = Str(reader, 26),
                BATERIA_LAPTOP              = Str(reader, 27),
                WIFI_MAC                    = Str(reader, 28),
                ETH_MAC                     = Str(reader, 29),
                ACCESORIOS                  = Str(reader, 30),
                UBICACION                   = Str(reader, 31),
                EDIFICIO                    = Str(reader, 32),
                FECHA_SALIDA                = Str(reader, 33),
                ASIGNADO_A                  = Str(reader, 34),
                DESTINO_PLANTA              = Str(reader, 35),
                DISPONIBLE                  = reader.IsDBNull(36) ? (bool?)null    : reader.GetBoolean(36),
                PERSONAL_IT_QUE_ASIGNA      = Str(reader, 37),
                FORMATO_DE_BAJA             = Str(reader, 38)
            });
        }

        return new { total, data = lista };
    }

    // ── Crear ─────────────────────────────────────────────────────────────
    public async Task<int> CrearAsync(DispositivoNFDto dto, string usuario)
    {
        await using var conn = await _pool.OpenAsync();

        await using var cmd = new NpgsqlCommand("""
            INSERT INTO dispositivos_nf
                (id_unico, oc, folio, fecha_registro, recibido_por,
                 subcategoria, marca, modelo, numero_serie, cantidad,
                 costo, proveedor, activo_fijo, procesador, arquitectura,
                 almacenamiento, tipo_disco_duro, sistema_operativo, licencia_sistema_operativo,
                 memoria_ram, velocidad_memoria, tipo_memoria, slot_memoria, max_memoria,
                 modelo_cargador, no_serie_eliminador, bateria_laptop, wifi_mac, eth_mac,
                 accesorios, ubicacion, edificio, fecha_salida, asignado_a,
                 destino_planta, disponible, personal_it_que_asigna, formato_de_baja)
            VALUES
                (@id_unico, @oc, @folio, @fecha_registro::date, @recibido_por,
                 @subcategoria, @marca, @modelo, @numero_serie, @cantidad,
                 @costo, @proveedor, @activo_fijo, @procesador, @arquitectura,
                 @almacenamiento, @tipo_disco_duro, @sistema_operativo, @licencia_sistema_operativo,
                 @memoria_ram, @velocidad_memoria, @tipo_memoria, @slot_memoria, @max_memoria,
                 @modelo_cargador, @no_serie_eliminador, @bateria_laptop, @wifi_mac, @eth_mac,
                 @accesorios, @ubicacion, @edificio, @fecha_salida::date, @asignado_a,
                 @destino_planta, @disponible, @personal_it_que_asigna, @formato_de_baja)
            RETURNING id
            """, conn);

        AgregarParametros(cmd, dto);
        var id = Convert.ToInt32(await cmd.ExecuteScalarAsync());
        _ordenesService.RecalcularPorCambioEnHija("Dispositivos NF", dto.OC, dto.FOLIO);
        return id;
    }

    // ── Editar ────────────────────────────────────────────────────────────
    public async Task<bool> EditarAsync(int id, DispositivoNFDto dto, string usuario)
    {
        await using var conn = await _pool.OpenAsync();

        var anterior = await SnapshotAsync(conn, id);
        if (anterior is null) return false;

        await using var cmd = new NpgsqlCommand("""
            UPDATE dispositivos_nf SET
                id_unico                   = @id_unico,
                oc                         = @oc,
                folio                      = @folio,
                fecha_registro             = @fecha_registro::date,
                recibido_por               = @recibido_por,
                subcategoria               = @subcategoria,
                marca                      = @marca,
                modelo                     = @modelo,
                numero_serie               = @numero_serie,
                cantidad                   = @cantidad,
                costo                      = @costo,
                proveedor                  = @proveedor,
                activo_fijo                = @activo_fijo,
                procesador                 = @procesador,
                arquitectura               = @arquitectura,
                almacenamiento             = @almacenamiento,
                tipo_disco_duro            = @tipo_disco_duro,
                sistema_operativo          = @sistema_operativo,
                licencia_sistema_operativo = @licencia_sistema_operativo,
                memoria_ram                = @memoria_ram,
                velocidad_memoria          = @velocidad_memoria,
                tipo_memoria               = @tipo_memoria,
                slot_memoria               = @slot_memoria,
                max_memoria                = @max_memoria,
                modelo_cargador            = @modelo_cargador,
                no_serie_eliminador        = @no_serie_eliminador,
                bateria_laptop             = @bateria_laptop,
                wifi_mac                   = @wifi_mac,
                eth_mac                    = @eth_mac,
                accesorios                 = @accesorios,
                ubicacion                  = @ubicacion,
                edificio                   = @edificio,
                fecha_salida               = @fecha_salida::date,
                asignado_a                 = @asignado_a,
                destino_planta             = @destino_planta,
                disponible                 = @disponible,
                personal_it_que_asigna     = @personal_it_que_asigna,
                formato_de_baja            = @formato_de_baja
            WHERE id = @id
            """, conn);

        AgregarParametros(cmd, dto);
        cmd.Parameters.AddWithValue("id", id);
        var rows = await cmd.ExecuteNonQueryAsync();
        if (rows == 0) return false;

        var nuevo = await SnapshotAsync(conn, id);
        await RegistrarHistorialAsync(conn, id, usuario, anterior, nuevo!);
        _ordenesService.RecalcularPorCambioEnHija("Dispositivos NF", dto.OC, dto.FOLIO);
        return true;
    }

    // ── Eliminar ──────────────────────────────────────────────────────────
    public async Task<bool> EliminarAsync(int id)
    {
        await using var conn = await _pool.OpenAsync();

        string? ocVal = null, folioVal = null;
        await using (var qSnap = new NpgsqlCommand(
            "SELECT oc, folio FROM dispositivos_nf WHERE id = @id", conn))
        {
            qSnap.Parameters.AddWithValue("id", id);
            await using var r = await qSnap.ExecuteReaderAsync();
            if (await r.ReadAsync())
            {
                ocVal = r.IsDBNull(0) ? null : r.GetString(0);
                folioVal = r.IsDBNull(1) ? null : r.GetString(1);
            }
        }

        await using var cmd = new NpgsqlCommand(
            "DELETE FROM dispositivos_nf WHERE id = @id", conn);
        cmd.Parameters.AddWithValue("id", id);
        var deleted = await cmd.ExecuteNonQueryAsync() > 0;

        if (deleted)
            _ordenesService.RecalcularPorCambioEnHija("Dispositivos NF", ocVal, folioVal);

        return deleted;
    }

    // ── Historial ─────────────────────────────────────────────────────────
    public async Task<List<object>> HistorialAsync(int id)
    {
        await using var conn = await _pool.OpenAsync();
        await using var cmd = new NpgsqlCommand(
            @"SELECT id, usuario, fecha, registro_anterior, registro_nuevo
              FROM dispositivos_nf_historial
              WHERE dispositivo_id = @id
              ORDER BY fecha DESC", conn);
        cmd.Parameters.AddWithValue("id", id);

        var lista = new List<object>();
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
        {
            lista.Add(new
            {
                ID                = r.GetInt32(0),
                USUARIO           = r.GetString(1),
                FECHA             = r.GetDateTime(2).ToString("yyyy-MM-dd HH:mm:ss"),
                REGISTRO_ANTERIOR = r.IsDBNull(3) ? null : r.GetString(3),
                REGISTRO_NUEVO    = r.IsDBNull(4) ? null : r.GetString(4)
            });
        }
        return lista;
    }

    // ── Exportar (filtrado) ───────────────────────────────────────────────
    public async Task<byte[]> ExportarAsync(DispositivoNFFiltros f)
    {
        await using var conn = await _pool.OpenAsync();

        var (where, parms) = ConstruirWhere(f);

        await using var cmd = new NpgsqlCommand(
            $@"SELECT id, id_unico, oc, folio, fecha_registro,
                      recibido_por, subcategoria, marca, modelo, numero_serie,
                      cantidad, costo, proveedor, activo_fijo, procesador,
                      arquitectura, almacenamiento, tipo_disco_duro, sistema_operativo, licencia_sistema_operativo,
                      memoria_ram, velocidad_memoria, tipo_memoria, slot_memoria, max_memoria,
                      modelo_cargador, no_serie_eliminador, bateria_laptop, wifi_mac, eth_mac,
                      accesorios, ubicacion, edificio, fecha_salida, asignado_a,
                      destino_planta, disponible, personal_it_que_asigna, formato_de_baja
               FROM dispositivos_nf {where} ORDER BY id DESC", conn);

        foreach (var (k, v) in parms) cmd.Parameters.AddWithValue(k, v ?? (object)DBNull.Value);

        var rows = new List<string?[]>();
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
        {
            rows.Add(Enumerable.Range(0, 39)
                .Select(i => r.IsDBNull(i) ? null : r.GetValue(i)?.ToString())
                .ToArray());
        }

        return GenerarExcel(rows);
    }

    // ── Exportar por año ──────────────────────────────────────────────────
    public async Task<byte[]> ExportarPorAnioAsync(int anio)
    {
        await using var conn = await _pool.OpenAsync();
        await using var cmd = new NpgsqlCommand(
            @"SELECT id, id_unico, oc, folio, fecha_registro,
                     recibido_por, subcategoria, marca, modelo, numero_serie,
                     cantidad, costo, proveedor, activo_fijo, procesador,
                     arquitectura, almacenamiento, tipo_disco_duro, sistema_operativo, licencia_sistema_operativo,
                     memoria_ram, velocidad_memoria, tipo_memoria, slot_memoria, max_memoria,
                     modelo_cargador, no_serie_eliminador, bateria_laptop, wifi_mac, eth_mac,
                     accesorios, ubicacion, edificio, fecha_salida, asignado_a,
                     destino_planta, disponible, personal_it_que_asigna, formato_de_baja
              FROM dispositivos_nf
              WHERE EXTRACT(YEAR FROM fecha_registro) = @anio
              ORDER BY id DESC", conn);
        cmd.Parameters.AddWithValue("anio", anio);

        var rows = new List<string?[]>();
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
        {
            rows.Add(Enumerable.Range(0, 39)
                .Select(i => r.IsDBNull(i) ? null : r.GetValue(i)?.ToString())
                .ToArray());
        }

        return GenerarExcel(rows);
    }

    // ── Helpers privados ──────────────────────────────────────────────────

    private static (string where, List<(string key, object? val)> parms) ConstruirWhere(DispositivoNFFiltros f)
    {
        var conds = new List<string>();

        conds.Add("(activo IS NULL OR activo = true)");

        var parms = new List<(string, object?)>();
        var idx = 1;

        void Add(string col, string? val)
        {
            if (string.IsNullOrWhiteSpace(val)) return;
            conds.Add($"LOWER({col}::TEXT) LIKE LOWER(@p{idx})");
            parms.Add(($"p{idx}", $"%{val}%"));
            idx++;
        }

        Add("id_unico",                    f.ID_UNICO);
        Add("oc",                          f.OC);
        Add("folio",                       f.FOLIO);
        Add("fecha_registro",              f.FECHA_REGISTRO);
        Add("recibido_por",                f.RECIBIDO_POR);
        Add("subcategoria",                f.SUBCATEGORIA);
        Add("marca",                       f.MARCA);
        Add("modelo",                      f.MODELO);
        Add("numero_serie",                f.NUMERO_SERIE);
        Add("proveedor",                   f.PROVEEDOR);
        Add("activo_fijo",                 f.ACTIVO_FIJO);
        Add("procesador",                  f.PROCESADOR);
        Add("arquitectura",                f.ARQUITECTURA);
        Add("almacenamiento",              f.ALMACENAMIENTO);
        Add("tipo_disco_duro",             f.TIPO_DISCO_DURO);
        Add("sistema_operativo",           f.SISTEMA_OPERATIVO);
        Add("licencia_sistema_operativo",  f.LICENCIA_SISTEMA_OPERATIVO);
        Add("memoria_ram",                 f.MEMORIA_RAM);
        Add("tipo_memoria",                f.TIPO_MEMORIA);
        Add("ubicacion",                   f.UBICACION);
        Add("edificio",                    f.EDIFICIO);
        Add("disponible::TEXT",            f.DISPONIBLE);
        Add("asignado_a",                  f.ASIGNADO_A);
        Add("destino_planta",              f.DESTINO_PLANTA);
        Add("personal_it_que_asigna",      f.PERSONAL_IT_QUE_ASIGNA);
        Add("formato_de_baja",             f.FORMATO_DE_BAJA);

        var where = conds.Count > 0 ? "WHERE " + string.Join(" AND ", conds) : "";
        return (where, parms);
    }

    private static void AgregarParametros(NpgsqlCommand cmd, DispositivoNFDto dto)
    {
        cmd.Parameters.AddWithValue("id_unico",                   (object?)dto.ID_UNICO                   ?? DBNull.Value);
        cmd.Parameters.AddWithValue("oc",                         (object?)dto.OC                         ?? DBNull.Value);
        cmd.Parameters.AddWithValue("folio",                      (object?)dto.FOLIO                      ?? DBNull.Value);
        cmd.Parameters.AddWithValue("fecha_registro",             (object?)dto.FECHA_REGISTRO              ?? DBNull.Value);
        cmd.Parameters.AddWithValue("recibido_por",               (object?)dto.RECIBIDO_POR                ?? DBNull.Value);
        cmd.Parameters.AddWithValue("subcategoria",               (object?)dto.SUBCATEGORIA                ?? DBNull.Value);
        cmd.Parameters.AddWithValue("marca",                      (object?)dto.MARCA                      ?? DBNull.Value);
        cmd.Parameters.AddWithValue("modelo",                     (object?)dto.MODELO                     ?? DBNull.Value);
        cmd.Parameters.AddWithValue("numero_serie",               (object?)dto.NUMERO_SERIE               ?? DBNull.Value);
        cmd.Parameters.AddWithValue("cantidad",                   (object?)dto.CANTIDAD                   ?? DBNull.Value);
        cmd.Parameters.AddWithValue("costo",                      (object?)dto.COSTO                      ?? DBNull.Value);
        cmd.Parameters.AddWithValue("proveedor",                  (object?)dto.PROVEEDOR                  ?? DBNull.Value);
        cmd.Parameters.AddWithValue("activo_fijo",                (object?)dto.ACTIVO_FIJO                ?? DBNull.Value);
        cmd.Parameters.AddWithValue("procesador",                 (object?)dto.PROCESADOR                 ?? DBNull.Value);
        cmd.Parameters.AddWithValue("arquitectura",               (object?)dto.ARQUITECTURA               ?? DBNull.Value);
        cmd.Parameters.AddWithValue("almacenamiento",             (object?)dto.ALMACENAMIENTO             ?? DBNull.Value);
        cmd.Parameters.AddWithValue("tipo_disco_duro",            (object?)dto.TIPO_DISCO_DURO            ?? DBNull.Value);
        cmd.Parameters.AddWithValue("sistema_operativo",          (object?)dto.SISTEMA_OPERATIVO          ?? DBNull.Value);
        cmd.Parameters.AddWithValue("licencia_sistema_operativo", (object?)dto.LICENCIA_SISTEMA_OPERATIVO ?? DBNull.Value);
        cmd.Parameters.AddWithValue("memoria_ram",                (object?)dto.MEMORIA_RAM                ?? DBNull.Value);
        cmd.Parameters.AddWithValue("velocidad_memoria",          (object?)dto.VELOCIDAD_MEMORIA          ?? DBNull.Value);
        cmd.Parameters.AddWithValue("tipo_memoria",               (object?)dto.TIPO_MEMORIA               ?? DBNull.Value);
        cmd.Parameters.AddWithValue("slot_memoria",               (object?)dto.SLOT_MEMORIA               ?? DBNull.Value);
        cmd.Parameters.AddWithValue("max_memoria",                (object?)dto.MAX_MEMORIA                ?? DBNull.Value);
        cmd.Parameters.AddWithValue("modelo_cargador",            (object?)dto.MODELO_CARGADOR            ?? DBNull.Value);
        cmd.Parameters.AddWithValue("no_serie_eliminador",        (object?)dto.NO_SERIE_ELIMINADOR        ?? DBNull.Value);
        cmd.Parameters.AddWithValue("bateria_laptop",             (object?)dto.BATERIA_LAPTOP             ?? DBNull.Value);
        cmd.Parameters.AddWithValue("wifi_mac",                   (object?)dto.WIFI_MAC                   ?? DBNull.Value);
        cmd.Parameters.AddWithValue("eth_mac",                    (object?)dto.ETH_MAC                    ?? DBNull.Value);
        cmd.Parameters.AddWithValue("accesorios",                 (object?)dto.ACCESORIOS                 ?? DBNull.Value);
        cmd.Parameters.AddWithValue("ubicacion",                  (object?)dto.UBICACION                  ?? DBNull.Value);
        cmd.Parameters.AddWithValue("edificio",                   (object?)dto.EDIFICIO                   ?? DBNull.Value);
        cmd.Parameters.AddWithValue("fecha_salida",               (object?)dto.FECHA_SALIDA               ?? DBNull.Value);
        cmd.Parameters.AddWithValue("asignado_a",                 (object?)dto.ASIGNADO_A                 ?? DBNull.Value);
        cmd.Parameters.AddWithValue("destino_planta",             (object?)dto.DESTINO_PLANTA             ?? DBNull.Value);
        cmd.Parameters.AddWithValue("disponible",                 (object?)dto.DISPONIBLE                 ?? DBNull.Value);
        cmd.Parameters.AddWithValue("personal_it_que_asigna",     (object?)dto.PERSONAL_IT_QUE_ASIGNA     ?? DBNull.Value);
        cmd.Parameters.AddWithValue("formato_de_baja",            (object?)dto.FORMATO_DE_BAJA            ?? DBNull.Value);
    }

    private async Task<Dictionary<string, object?>?> SnapshotAsync(NpgsqlConnection conn, int id)
    {
        await using var cmd = new NpgsqlCommand(
            @"SELECT id_unico, oc, folio, fecha_registro, recibido_por,
                     subcategoria, marca, modelo, numero_serie, cantidad,
                     costo, proveedor, activo_fijo, procesador, arquitectura,
                     almacenamiento, tipo_disco_duro, sistema_operativo, licencia_sistema_operativo,
                     memoria_ram, velocidad_memoria, tipo_memoria, slot_memoria, max_memoria,
                     modelo_cargador, no_serie_eliminador, bateria_laptop, wifi_mac, eth_mac,
                     accesorios, ubicacion, edificio, fecha_salida, asignado_a,
                     destino_planta, disponible, personal_it_que_asigna, formato_de_baja
              FROM dispositivos_nf WHERE id=@id", conn);
        cmd.Parameters.AddWithValue("id", id);

        await using var r = await cmd.ExecuteReaderAsync();
        if (!await r.ReadAsync()) return null;

        var snap = new Dictionary<string, object?>();
        for (int i = 0; i < COLS.Length; i++)
            snap[COLS[i]] = r.IsDBNull(i) ? null : r.GetValue(i)?.ToString();

        return snap;
    }

    private async Task RegistrarHistorialAsync(NpgsqlConnection conn, int dispositivoId,
        string usuario, Dictionary<string, object?> anterior, Dictionary<string, object?> nuevo)
    {
        var antesJson   = System.Text.Json.JsonSerializer.Serialize(anterior);
        var despuesJson = System.Text.Json.JsonSerializer.Serialize(nuevo);

        await using var cmd = new NpgsqlCommand(
            """
            INSERT INTO dispositivos_nf_historial (dispositivo_id, usuario, registro_anterior, registro_nuevo)
            VALUES (@did, @usr, @ant::jsonb, @nvo::jsonb)
            """, conn);

        cmd.Parameters.AddWithValue("did", dispositivoId);
        cmd.Parameters.AddWithValue("usr", usuario);
        cmd.Parameters.AddWithValue("ant", antesJson);
        cmd.Parameters.AddWithValue("nvo", despuesJson);

        await cmd.ExecuteNonQueryAsync();
    }

    private static byte[] GenerarExcel(List<string?[]> rows)
    {
        ExcelPackage.License.SetNonCommercialPersonal("IT Control Center");
        using var pkg = new ExcelPackage();
        var ws = pkg.Workbook.Worksheets.Add("Dispositivos NF");

        string[] headers =
        [
            "ID","ID ÚNICO","OC","FOLIO","FECHA REGISTRO",
            "RECIBIDO POR","SUBCATEGORÍA","MARCA","MODELO","NO SERIE",
            "CANTIDAD","COSTO","PROVEEDOR","ACTIVO FIJO","PROCESADOR",
            "ARQUITECTURA","ALMACENAMIENTO","TIPO DISCO DURO","SISTEMA OPERATIVO","LIC. SISTEMA OPERATIVO",
            "MEMORIA RAM","VELOCIDAD MEMORIA","TIPO MEMORIA","SLOT MEMORIA","MÁX. MEMORIA",
            "MODELO CARGADOR","NO SERIE ELIMINADOR","BATERÍA LAPTOP","WIFI MAC","ETH MAC",
            "ACCESORIOS","UBICACIÓN","EDIFICIO","FECHA SALIDA","ASIGNADO A",
            "DESTINO PLANTA","DISPONIBLE","PERSONAL IT QUE ASIGNA","FORMATO DE BAJA"
        ];

        for (int c = 0; c < headers.Length; c++)
        {
            ws.Cells[1, c + 1].Value = headers[c];
            ws.Cells[1, c + 1].Style.Font.Bold = true;
            ws.Cells[1, c + 1].Style.Fill.PatternType = ExcelFillStyle.Solid;
            ws.Cells[1, c + 1].Style.Fill.BackgroundColor.SetColor(Color.FromArgb(30, 41, 59));
            ws.Cells[1, c + 1].Style.Font.Color.SetColor(Color.White);
            ws.Cells[1, c + 1].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
        }

        for (int r = 0; r < rows.Count; r++)
        {
            for (int c = 0; c < rows[r].Length; c++)
                ws.Cells[r + 2, c + 1].Value = rows[r][c];

            if (r % 2 == 0)
            {
                using var range = ws.Cells[r + 2, 1, r + 2, headers.Length];
                range.Style.Fill.PatternType = ExcelFillStyle.Solid;
                range.Style.Fill.BackgroundColor.SetColor(Color.FromArgb(241, 245, 249));
            }
        }

        ws.Cells.AutoFitColumns();
        return pkg.GetAsByteArray();
    }

    private static string? Str(NpgsqlDataReader r, int i)
        => r.IsDBNull(i) ? null : r.GetValue(i)?.ToString();
}
