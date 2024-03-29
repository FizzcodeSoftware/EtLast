﻿namespace FizzCode.EtLast;

public class DelimitedValueFormatter : IValueFormatter
{
    public static DelimitedValueFormatter Default { get; } = new DelimitedValueFormatter();

    /// <summary>
    /// Default value is "yyyy.MM.dd HH:mm:ss.fffffff"
    /// </summary>
    public string DateTimeFormat { get; init; } = "yyyy.MM.dd HH:mm:ss.fffffff";

    /// <summary>
    /// Default value is "yyyy.MM.dd"
    /// </summary>
    public string DateFormat { get; init; } = "yyyy.MM.dd";

    /// <summary>
    /// Default value is "HH:mm:ss.fffffff"
    /// </summary>
    public string TimeFormat { get; init; } = "HH:mm:ss.fffffff";

    /// <summary>
    /// Default value is "yyyy.MM.dd HH:mm:ss.fffffff zzz"
    /// </summary>
    public string DateTimeOffsetFormat { get; init; } = "yyyy.MM.dd HH:mm:ss.fffffff zzz";

    /// <summary>
    /// Default value is "G"
    /// </summary>
    public string TimeSpanFormat { get; init; } = "G";

    /// <summary>
    /// Default value is "D"
    /// </summary>
    public string IntegerFormat { get; init; } = "G";

    /// <summary>
    /// Default value is "G"
    /// </summary>
    public string FloatingFormat { get; init; } = "G";

    /// <summary>
    /// Default value is "G"
    /// </summary>
    public string DecimalFormat { get; init; } = "G";

    /// <summary>
    /// Divide the decimal values with 1.000000000000000000000000000000000m before formatting to string to remove trailing zeros.
    /// </summary>
    public bool NormalizeDecimal { get; init; } = true;

    /// <summary>
    /// Default value is "D"
    /// </summary>
    public string GuidFormat { get; init; } = "D";

    /// <summary>
    /// Default value is "G"
    /// </summary>
    public string GenericFormat { get; init; } = "G";

    public string Format(object v, IFormatProvider formatProvider = null)
    {
        if (v == null)
            return null;

        if (v is string str)
            return str;

        if (v is Enum e)
            return e.GetType().Name + "." + e.ToString();

        if (v is bool b)
            return b ? "true" : "false";

        if (v is char chr)
            return chr.ToString(formatProvider);

        if (v is byte bv)
            return bv.ToString(IntegerFormat, formatProvider ?? CultureInfo.InvariantCulture);

        if (v is sbyte sbv)
            return sbv.ToString(IntegerFormat, formatProvider ?? CultureInfo.InvariantCulture);

        if (v is short sv)
            return sv.ToString(IntegerFormat, formatProvider ?? CultureInfo.InvariantCulture);

        if (v is ushort usv)
            return usv.ToString(IntegerFormat, formatProvider ?? CultureInfo.InvariantCulture);

        if (v is int iv)
            return iv.ToString(IntegerFormat, formatProvider ?? CultureInfo.InvariantCulture);

        if (v is uint uiv)
            return uiv.ToString(IntegerFormat, formatProvider ?? CultureInfo.InvariantCulture);

        if (v is long lv)
            return lv.ToString(IntegerFormat, formatProvider ?? CultureInfo.InvariantCulture);

        if (v is ulong ulv)
            return ulv.ToString(IntegerFormat, formatProvider ?? CultureInfo.InvariantCulture);

        if (v is double dv)
            return dv.ToString(FloatingFormat, formatProvider ?? CultureInfo.InvariantCulture);

        if (v is float fv)
            return fv.ToString(FloatingFormat, formatProvider ?? CultureInfo.InvariantCulture);

        if (v is decimal decv)
        {
            return NormalizeDecimal
                ? (decv / 1.000000000000000000000000000000000m).ToString(DecimalFormat, formatProvider ?? CultureInfo.InvariantCulture)
                : decv.ToString(DecimalFormat, formatProvider ?? CultureInfo.InvariantCulture);
        }

        if (v is TimeSpan ts)
            return ts.ToString(TimeSpanFormat, formatProvider ?? CultureInfo.InvariantCulture);

        if (v is DateTime dt)
            return dt.ToString(DateTimeFormat, formatProvider ?? CultureInfo.InvariantCulture);

        if (v is DateTimeOffset dto)
            return dto.ToString(DateTimeOffsetFormat, formatProvider ?? CultureInfo.InvariantCulture);

        if (v is DateOnly dateOnly)
            return dateOnly.ToString(DateFormat, formatProvider ?? CultureInfo.InvariantCulture);

        if (v is TimeOnly timeOnly)
            return timeOnly.ToString(TimeFormat, formatProvider ?? CultureInfo.InvariantCulture);

        if (v is Guid guid)
            return guid.ToString(GuidFormat, formatProvider ?? CultureInfo.InvariantCulture);

        if (v is IFormattable fmt)
            return fmt.ToString(GenericFormat, formatProvider ?? CultureInfo.InvariantCulture);

        if (v is byte[] data)
            return Convert.ToBase64String(data);

        return v.ToString();
    }
}
