﻿namespace FizzCode.EtLast;

public class BoolConverter : ITypeConverter, ITextConverter
{
    public virtual object Convert(object source)
    {
        if (source is bool)
            return source;

        if (source is string stringValue)
        {
            if (stringValue.Trim() == "1")
                return true;
            else if (stringValue.Trim() == "0")
                return false;
        }

        if (source is sbyte sbv)
            return sbv == 1;

        if (source is byte bv)
            return bv == 1;

        if (source is short sv)
            return sv == 1;

        if (source is ushort usv)
            return usv == 1;

        if (source is int iv)
            return iv == 1;

        if (source is uint uiv)
            return uiv == 1;

        if (source is long lv)
            return lv == 1;

        if (source is ulong ulv)
            return ulv == 1;

        return null;
    }

    public object Convert(TextReaderStringBuilder source)
    {
        var stringValue = source.GetContentAsString();

        if (stringValue.Trim() == "1")
            return true;
        else if (stringValue.Trim() == "0")
            return false;

        return null;
    }
}