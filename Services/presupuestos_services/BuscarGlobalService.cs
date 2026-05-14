using ChiIT.Data;
using Microsoft.Data.SqlClient;

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
        ("PERIFERICOS NF","perifericos_nf",[
        "id_unico",
        "oc",
        "folio_inventario",
        "fecha_entrada",
        "recibido_por",
        "subcategoria",
        "tipo",
        "marca",
        "modelo",
        "cantidad",
        "numero_serie",
        "proveedor",
        "costo_pesos",
        "estado",
        "destino",
        "disponible",
        "fecha_salida",
        "destino_planta",
        "asignado_a",
        "personal_it_que_asigna"
        ]),
        ("BITACORA FIRECOM","bitacora_firecom",[
        "id_unico",
        "oc",
        "orden_servicio",
        "fecha",
        "persona_que_solicita_reporta",
        "cuenta_con_poliza",
        "servicio_con_costo",
        "ubicacion",
        "area",
        "cantidad",
        "descripcion_servicio",
        "descripcion_trabajo",
        "material_equipo",
        "observaciones",
        "proveedores",
        "panel_faceplate",
        "switch_red",
        "personal_que_recibio",
        "pagado",
        "oc2"
        ]),
        ("CAMARAS AUDIO","camaras_audio",[
        "id",
        "oc",
        "folio_inventario",
        "fecha_registro",
        "recibido_por",
        "subcategoria",
        "tipo",
        "marca",
        "modelo",
        "numero_de_serie",
        "proveedor",
        "cantidad",
        "costo",
        "moneda",
        "destino",
        "accesorios",
        "fecha_de_salida",
        "planta",
        "destino2",
        "personal_it_que_asigna",
        "folio_de_servicio"
    ]),
    ("IMPRESORAS NF","impresoras_nf",[
    "id_unico",
    "oc",
    "folio_inventario",
    "fecha_de_entrada",
    "recibido_por",
    "marca",
    "modelo",
    "numero_de_serie",
    "tipo",
    "cantidad",
    "ip",
    "mac",
    "proveedor",
    "costo",
    "moneda",
    "ubicacion",
    "estado",
    "planta",
    "disponible",
    "fecha_de_asignacion",
    "observaciones",
    "fecha_de_salida",
    "destino_planta",
    "asignado_a",
    "personal_it_que_asigna",
    "fecha_de_mantenimiento"
    ]),
    ("SERVICIOS PROVEEDORES","servicios_proveedores",[
    "id_unico",
    "folio_unico",
    "folio_cotizacion",
    "folio_reporte",
    "fecha",
    "requisitor",
    "cuenta_con_poliza",
    "servicio_con_costo",
    "ubicacion_planta",
    "area",
    "cantidad",
    "descripcion_servicio",
    "descripcion_trabajo",
    "material_equipo",
    "observaciones",
    "proveedores",
    "panel_faceplate",
    "switch",
    "personal_recibio",
    "solicitud_finalizada",
    "costo"
    ]),
    ("CONSUMIBLES","consumibles_nf",[
    "id_unico",
    "oc",
    "folio_cantidad",
    "fecha_entrada",
    "recibido_por",
    "subcategoria",
    "marca",
    "modelo",
    "descripcion",
    "cantidad",
    "proveedor",
    "costo",
    "moneda",
    "planta",
    "ubicacion",
    "destino"
    ]),

    ("RADIOS","radios_nf",[
    "id_unico",
    "oc",
    "folio",
    "fecha_registro",
    "recibido_por",
    "subcategoria",
    "marca",
    "modelo",
    "no_serie",
    "cantidad",
    "observaciones",
    "proveedor",
    "costo",
    "moneda",
    "vida_util",
    "requisitor",
    "disponible",
    "fecha_salida",
    "asignado_a",
    "destino_planta",
    "personal_it_asigna"
    ]),

    ("EQUIPO RED","equipo_red_nf",[
    "id_unico",
    "oc",
    "folio_correctivo",
    "fecha_registro",
    "recibido_por",
    "subcategoria",
    "no_parte",
    "marca",
    "modelo",
    "numero_serie",
    "mac1",
    "mac2",
    "mac_address",
    "cantidad",
    "proveedor",
    "costo",
    "moneda",
    "ubicacion",
    "observaciones_comentarios",
    "destino",
    "observaciones",
    "activo_dtr3"
    ]),

    ("TINTAS TONER RIBON","tintas_toner_ribon_nf",[
    "id_unico",
    "oc",
    "modelo",
    "recibido_por",
    "subcategoria",
    "fecha_registro",
    "proveedor",
    "stock",
    "costo_mn",
    "cantidad_recibida",
    "fecha_instalacion",
    "ubicacion",
    "impresora",
    "instalado_por"
    ]),

    ("REPORTES_IMPRESORAS","reportes_impresoras",[
    "id",
    "folio",
    "fecha",
    "planta",
    "impresora",
    "area",
    "reporte",
    "quien_reporta",
    "estatus",
    "fecha_de_realizacion",
    "comentarios",
    "creado_en"
    ]),

    ("IMPRESORAS_INFO","impresoras_info",[
        "id",
        "impresora",
        "modelo",
        "numero_de_serie",
        "ip",
        "ubicacion",
        "planta",
        "identificador",
        "numero",
        "creado_en"
    ]),

    ("REMISIONES","remisiones",[
    "id_oc",
    "id_remision",
    "folio",
    "solicitante",
    "fecha_solicitud",
    "accesorio_solicitado",
    "modelo_serie_comentarios",
    "proveedor",
    "pieza_servicio",
    "cantidad",
    "precio_unitario",
    "total_sin_iva",
    "moneda",
    "pagado",
    "presupuesto",
    "cuenta_a_descontar",
    "fecha_entrada_planta",
    "status",
    "requisicion",
    "oc"
    ])
    ];

    public async Task<object> BuscarAsync(string termino, int limite = 20)
    {
        var resultados = new List<object>();

        await using var conn = await _pool.OpenAsync();

        foreach (var (modulo, tabla, columnas) in MODULOS)
        {
            try
            {
                // Verificar que la tabla existe antes de consultar
                await using var chk = new SqlCommand(
                    "SELECT EXISTS(SELECT 1 FROM information_schema.tables WHERE table_name=@t)", conn);
                chk.Parameters.AddWithValue("t", tabla);
                var existe = (bool)(await chk.ExecuteScalarAsync() ?? false);
                if (!existe) continue;

                // Filtrar solo columnas que existen en esa tabla
                var colsExistentes = await ColumnasExistentesAsync(conn, tabla, columnas);
                if (!colsExistentes.Any()) continue;

                // Excluir "id" de las columnas de búsqueda para evitar duplicado en SELECT
                var colsSinId = colsExistentes.Where(c => c != "id").ToList();

                var condiciones = colsSinId.Any()
                    ? colsSinId.Select((c, i) => $"LOWER({c}) LIKE LOWER(@p{i})").ToList()
                    : colsExistentes.Select((c, i) => $"LOWER({c}) LIKE LOWER(@p{i})").ToList();

                var colsParaWhere = colsSinId.Any() ? colsSinId : colsExistentes;
                var where = "WHERE " + string.Join(" OR ", condiciones);

                // Total
                await using var cmdCount = new SqlCommand(
                    $"SELECT COUNT(*) FROM {tabla} {where}", conn);
                for (int i = 0; i < colsParaWhere.Count; i++)
                    cmdCount.Parameters.AddWithValue($"p{i}", $"%{termino}%");
                var total = Convert.ToInt64(await cmdCount.ExecuteScalarAsync());
                if (total == 0) continue;

                // Registros (limitado) — "id" siempre primero, sin duplicar
                var selectCols = colsSinId.Any()
                    ? "id, " + string.Join(", ", colsSinId)
                    : "id";
                await using var cmdData = new SqlCommand(
                    $"SELECT {selectCols} FROM {tabla} {where} ORDER BY id DESC LIMIT @lim", conn);
                for (int i = 0; i < colsParaWhere.Count; i++)
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
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[BuscarGlobal] Error en módulo {modulo}: {ex.Message}");
                continue;
            }
        }

        return new
        {
            modulos_con_resultados = resultados.Count,
            resultados
        };
    }

    private static async Task<List<string>> ColumnasExistentesAsync(
        SqlConnection conn, string tabla, string[] columnas)
    {
        await using var cmd = new SqlCommand(
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