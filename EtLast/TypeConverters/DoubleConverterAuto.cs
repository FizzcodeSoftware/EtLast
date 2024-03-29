﻿namespace FizzCode.EtLast;

public class DoubleConverterAuto(IFormatProvider formatProvider, NumberStyles numberStyles = NumberStyles.Any) : DoubleConverter
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

            if (double.TryParse(stringValue, NumberStyles, FormatProvider, out var value))
            {
                return value;
            }
        }

        return base.Convert(source);
    }
}

[EditorBrowsable(EditorBrowsableState.Never)]
public static class DoubleConverterAutoFluent
{
    public static ReaderColumn AsDoubleAuto(this ReaderColumn column, IFormatProvider formatProvider, NumberStyles numberStyles) => column.WithTypeConverter(new DoubleConverterAuto(formatProvider, numberStyles));
    public static TextReaderColumn AsDoubleAuto(this TextReaderColumn column, IFormatProvider formatProvider, NumberStyles numberStyles) => column.WithTypeConverter(new DoubleConverterAuto(formatProvider, numberStyles));
    public static IConvertMutatorBuilder_NullStrategy ToDoubleAuto(this IConvertMutatorBuilder_WithTypeConverter builder, IFormatProvider formatProvider, NumberStyles numberStyles) => builder.WithTypeConverter(new DoubleConverterAuto(formatProvider, numberStyles));
}