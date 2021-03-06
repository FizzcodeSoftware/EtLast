﻿namespace FizzCode.EtLast
{
    using System;
    using System.Globalization;

    public class IntConverter : ITypeConverter
    {
        public string[] RemoveSubString { get; set; }

        public virtual object Convert(object source)
        {
            if (source is int)
                return source;

            // smaller whole numbers
            if (source is sbyte sbv)
                return System.Convert.ToInt32(sbv);

            if (source is byte bv)
                return System.Convert.ToInt32(bv);

            if (source is short sv)
                return System.Convert.ToInt32(sv);

            if (source is ushort usv)
                return System.Convert.ToInt32(usv);

            // larger whole numbers
            if (source is uint uiv && uiv <= int.MaxValue)
                return System.Convert.ToInt32(uiv);

            if (source is long lv && lv >= int.MinValue && lv <= int.MaxValue)
                return System.Convert.ToInt32(lv);

            if (source is ulong ulv && ulv <= int.MaxValue)
                return System.Convert.ToInt32(ulv);

            // decimal values
            if (source is float fv && fv >= int.MinValue && fv <= int.MaxValue)
                return System.Convert.ToInt32(fv);

            if (source is double dv && dv >= int.MinValue && dv <= int.MaxValue)
                return System.Convert.ToInt32(dv);

            if (source is decimal dcv && dcv >= int.MinValue && dcv <= int.MaxValue)
                return System.Convert.ToInt32(dcv);

            if (source is bool boolv)
                return boolv ? 1 : 0;

            if (source is string str)
            {
                if (RemoveSubString != null)
                {
                    foreach (var subStr in RemoveSubString)
                    {
                        str = str.Replace(subStr, "", StringComparison.InvariantCultureIgnoreCase);
                    }
                }

                if (int.TryParse(str, NumberStyles.Any, CultureInfo.InvariantCulture, out var value))
                    return value;
            }

            return null;
        }
    }
}