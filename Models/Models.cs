namespace ChiIT.Models;

// ── REQUEST BODIES ───────────────────────────────────────

public class LoginRequest
{
    public string Usuario { get; set; } = "";
    public string Password { get; set; } = "";
}

public class CambioPasswordRequest
{
    public string Usuario { get; set; } = "";
    public string PasswordActual { get; set; } = "";
    public string PasswordNuevo { get; set; } = "";
}

public class PreventivoRequest
{
    public string? ID_EQUIPO { get; set; }
    public string? UBICACION { get; set; }
    public string? PLAZO { get; set; }
    public string? REALIZADO_POR { get; set; }
    public string? FECHA_REALIZACION { get; set; }
    public string? OBSERVACIONES { get; set; }
    public string? nombre_dispositivo { get; set; }
    public string? PLANTA { get; set; }
    public string? CATEGORIA_COLOR { get; set; }
    public int? ANIO_CREACION { get; set; }
}

public class GuardarPmRequest
{
    public string Usuario { get; set; } = "SISTEMA";
    public string Fecha { get; set; } = "";
    public List<int> Checks { get; set; } = new();
    public string Observaciones { get; set; } = "";
    public bool RequiereCorrectivo { get; set; } = false;
}

public class GuardarDigitalRequest
{
    public string? Fecha { get; set; }
    public string? Observaciones { get; set; }
    public List<int> Checks { get; set; } = new();
    public List<double> Ids_con_check { get; set; } = new();
}

// ── FILTROS ──────────────────────────────────────────────

public class FiltrosPreventivo
{
    public int Page { get; set; } = 1;
    public int Limit { get; set; } = 10;
    public string? ID_EQUIPO { get; set; }
    public string? UBICACION { get; set; }
    public string? nombre_dispositivo { get; set; }
    public string? PLANTA { get; set; }
    public string? CATEGORIA_COLOR { get; set; }
    public string? OBSERVACIONES { get; set; }
    public string? ANIO_CREACION { get; set; }
}