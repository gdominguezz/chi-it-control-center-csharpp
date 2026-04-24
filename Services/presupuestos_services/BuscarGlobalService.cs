using ChiIT.Data;
using Npgsql;

namespace ChiIT.Services;

public class BuscarGlobalService
{
    private readonly DbConnectionPool _pool;

    public BuscarGlobalService(DbConnectionPool pool) => _pool = pool;

    private static readonly List<(string Modulo, string Tabla, string[] Columnas)> MODULOS =
    [
        ("ORDENES DE COMPRA", "ordenes_de_compra", [
            "orden_de_compra", "folio", "solicitante", "presupuesto_mes",
            "serie_ubicacion_no_empleado", "accesorio_solicitado", "proveedor_elegido",
            "pieza_servicio", "moneda", "comentarios", "hoja_control",
            "requisicion", "oc", "estatus_oc"
        ]),
        ("REQ VS OC", "req_vs_oc", [
            "no_requisicion", "orden_compra", "moneda", "oc_subtotal", "registrada_en_oc"
        ]),
        ("PANTALLAS NF", "pantallas_nf", [
            "id_unico", "oc", "folio", "recibido_por", "subcategoria",
            "marca", "modelo", "no_serie", "accesorios",
            "mac_wifi", "mac_ethernet", "proveedor",
            "estado", "destino_planta", "asignado_a", "personal_it_que_asigna"
        ]),
        ("REFACCIONES NF", "refacciones_nf", [
            "id_unico", "oc", "folio_correctivo", "recibido_por", "subcategoria",
            "marca", "modelo", "serie", "num_parte",
            "moneda", "proveedor", "disponible", "comentarios"
        ]),
        ("ACCESORIOS NF", "accesorios_nf", [
            "id_unico", "oc", "folio", "recibido_por", "subcategoria",
            "marca", "modelo", "no_serie", "tipo",
            "accesorios", "moneda", "proveedor", "disponible",
            "asignado_a", "destino_planta", "personal_it_que_asigna"
        ]),
        ("HERRAMIENTAS NF", "herramientas_nf", [
            "id_unico", "oc", "folio_correctivo", "recibido_por", "subcategoria",
            "marca", "modelo", "numero_serie", "tipo_uso",
            "num_parte", "moneda", "proveedor", "ubicacion",
            "comentarios"
        ]),
        ("DISPOSITIVOS NF","dispositivos_nf",[
        "id_unico",
        "oc",
        "folio",
        "fecha_registro",
        "recibido_por",
        "subcategoria",
        "marca",
        "modelo",
        "numero_serie",
        "cantidad",
        "costo",
        "proveedor",
        "activo_fijo",
        "procesador",
        "arquitectura",
        "almacenamiento",
        "tipo_disco_duro",
        "sistema_operativo",
        "licencia_sistema_operativo",
        "memoria_ram",
        "velocidad_memoria",
        "tipo_memoria",
        "slot_memoria",
        "max_memoria",
        "modelo_cargador",
        "no_serie_eliminador",
        "bateria_laptop",
        "wifi_mac",
        "eth_mac",
        "accesorios",
        "ubicacion",
        "edificio",
        "fecha_salida",
        "asignado_a",
        "destino_planta",
        "disponible",
        "personal_it_que_asigna",
        "formato_de_baja"
        ]),
        ("INVENTARIOS","inventarios_nf",[
        "inv_folio",
        "equipo",
        "marca",
        "modelo",
        "cantidad",
        "precio_unitario",
        "precio_con_iva",
        "moneda",
        "proveedor",
        "presupuesto",
        "status",
        "anio",
        "oc",
        "numero_serie",
        "ubicacion_actual"
        ]),
        ("PERIFERICOS NF",   "perifericos_nf",    ["id_unico","oc","folio_correctivo","marca","modelo","serie","num_parte","proveedor","recibido_por","comentarios"]),
        ("IMPRESORAS NF",    "impresoras_nf",     ["id_unico","oc","folio_correctivo","marca","modelo","serie","num_parte","proveedor","recibido_por","comentarios"]),
        ("TELEFONIA NF",     "telefonia_nf",      ["id_unico","oc","folio_correctivo","marca","modelo","serie","num_parte","proveedor","recibido_por","comentarios"]),
        ("CONSUMIBLES NF",   "consumibles_nf",    ["id_unico","oc","folio_correctivo","marca","modelo","serie","num_parte","proveedor","recibido_por","comentarios"]),
        ("RADIOS NF",        "radio_nf",          ["id_unico","oc","folio_correctivo","marca","modelo","serie","num_parte","proveedor","recibido_por","comentarios"]),
        ("EQUIPO DE RED NF", "refacciones_red_nf",["id_unico","oc","folio_correctivo","marca","modelo","serie","num_parte","proveedor","recibido_por","comentarios"]),
        ("TINTAS TONER RIBON","tintas_toner_ribon_nf",["id_unico","oc","folio_correctivo","marca","modelo","serie","num_parte","proveedor","recibido_por","comentarios"]),

    ];

    public async Task<object> BuscarAsync(string termino, int limite = 20)
    {
        var resultados = new List<object>();

        await using var conn = await _pool.OpenAsync();

        foreach (var (modulo, tabla, columnas) in MODULOS)
        {
            // Verificar que la tabla existe antes de consultar
            await using var chk = new NpgsqlCommand(
                "SELECT EXISTS(SELECT 1 FROM information_schema.tables WHERE table_name=@t)", conn);
            chk.Parameters.AddWithValue("t", tabla);
            var existe = (bool)(await chk.ExecuteScalarAsync() ?? false);
            if (!existe) continue;

            // Filtrar solo columnas que existen en esa tabla
            var colsExistentes = await ColumnasExistentesAsync(conn, tabla, columnas);
            if (!colsExistentes.Any()) continue;

            var condiciones = colsExistentes
                .Select((c, i) => $"LOWER({c}::TEXT) LIKE LOWER(@p{i})")
                .ToList();

            var where = "WHERE " + string.Join(" OR ", condiciones);

            // Total
            await using var cmdCount = new NpgsqlCommand(
                $"SELECT COUNT(*) FROM {tabla} {where}", conn);
            for (int i = 0; i < colsExistentes.Count; i++)
                cmdCount.Parameters.AddWithValue($"p{i}", $"%{termino}%");
            var total = Convert.ToInt64(await cmdCount.ExecuteScalarAsync());
            if (total == 0) continue;

            // Registros (limitado)
            var selectCols = string.Join(", ", colsExistentes);
            await using var cmdData = new NpgsqlCommand(
                $"SELECT id, {selectCols} FROM {tabla} {where} ORDER BY id DESC LIMIT @lim", conn);
            for (int i = 0; i < colsExistentes.Count; i++)
                cmdData.Parameters.AddWithValue($"p{i}", $"%{termino}%");
            cmdData.Parameters.AddWithValue("lim", limite);

            var registros = new List<Dictionary<string, string?>>();
            await using var r = await cmdData.ExecuteReaderAsync();
            var fields = Enumerable.Range(0, r.FieldCount).Select(r.GetName).ToList();
            while (await r.ReadAsync())
            {
                var row = new Dictionary<string, string?>();
                foreach (var f in fields)
                    row[f.ToUpper()] = r[f] is DBNull ? null : r[f]?.ToString();
                registros.Add(row);
            }

            resultados.Add(new { modulo, total, registros });
        }

        return new
        {
            modulos_con_resultados = resultados.Count,
            resultados
        };
    }

    private static async Task<List<string>> ColumnasExistentesAsync(
        NpgsqlConnection conn, string tabla, string[] columnas)
    {
        await using var cmd = new NpgsqlCommand(
            @"SELECT column_name FROM information_schema.columns
              WHERE table_name = @t AND column_name = ANY(@cols)", conn);
        cmd.Parameters.AddWithValue("t", tabla);
        cmd.Parameters.AddWithValue("cols", columnas);

        var result = new List<string>();
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
            result.Add(r.GetString(0));
        return result;
    }
}