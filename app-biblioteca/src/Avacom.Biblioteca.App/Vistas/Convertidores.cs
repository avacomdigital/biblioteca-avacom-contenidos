using System.Globalization;

namespace Avacom.Biblioteca.App.Vistas;

/// <summary>Invierte un booleano. Sirve para mostrar el aviso justo cuando NO hay licencia.</summary>
public sealed class Negar : IValueConverter
{
    public object Convert(object? v, Type t, object? p, CultureInfo c) => v is bool b && !b;
    public object ConvertBack(object? v, Type t, object? p, CultureInfo c) => v is bool b && !b;
}

/// <summary>Visible solo si el texto tiene algo. Evita huecos vacios en la vista.</summary>
public sealed class HayTexto : IValueConverter
{
    public object Convert(object? v, Type t, object? p, CultureInfo c) => !string.IsNullOrWhiteSpace(v as string);
    public object ConvertBack(object? v, Type t, object? p, CultureInfo c) => throw new NotSupportedException();
}

/// <summary>Compara con el parametro. Se usa para encender un visor y apagar los demas.</summary>
public sealed class EsIgual : IValueConverter
{
    public object Convert(object? v, Type t, object? p, CultureInfo c) =>
        string.Equals(v as string, p as string, StringComparison.OrdinalIgnoreCase);
    public object ConvertBack(object? v, Type t, object? p, CultureInfo c) => throw new NotSupportedException();
}
