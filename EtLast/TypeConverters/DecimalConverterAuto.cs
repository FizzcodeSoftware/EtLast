﻿namespace FizzCode.EtLast;

public class DecimalConverterAuto(IFormatProvider formatProvider, NumberStyles numberStyles = NumberStyles.Any) : DecimalConverter
{
    public IFormatProvider FormatProvider { get; } = formatProvider;
    public NumberStyles NumberStyles { get; } = numberStyles;

    public override object Convert(object source)
    {
        if (source is string stringValue)
        {
            if (RemoveSubString != null)
            {
                foreach (var subStr in RemoveSubString)
                {
                    stringValue = stringValue.Replace(subStr, "", StringComparison.InvariantCultureIgnoreCase);
                }
            }

            if (decimal.TryParse(stringValue, NumberStyles, FormatProvider, out var value))
            {
                return value;
            }
        }

        return base.Convert(source);
    }
}

[EditorBrowsable(EditorBrowsableState.Never)]
public static class DecimalConverterAutoFluent
{
    public static ReaderColumn AsDecimalAuto(this ReaderColumn column, IFormatProvider formatProvider, NumberStyles numberStyles) => column.WithTypeConverter(new DecimalConverterAuto(formatProvider, numberStyles));
    public static TextReaderColumn AsDecimalAuto(this TextReaderColumn column, IFormatProvider formatProvider, NumberStyles numberStyles) => column.WithTypeConverter(new DecimalConverterAuto(formatProvider, numberStyles));
    public static IConvertMutatorBuilder_NullStrategy ToDecimalAuto(this IConvertMutatorBuilder_WithTypeConverter builder, IFormatProvider formatProvider, NumberStyles numberStyles) => builder.WithTypeConverter(new DecimalConverterAuto(formatProvider, numberStyles));
}