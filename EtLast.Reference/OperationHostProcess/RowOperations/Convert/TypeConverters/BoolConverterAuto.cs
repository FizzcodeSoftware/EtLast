﻿using System;

namespace FizzCode.EtLast
{
    public class BoolConverterAuto : BoolConverter
    {
        public string KnownTrueString { get; set; }
        public string KnownFalseString { get; set; }

        public override object Convert(object source)
        {
            var baseResult = base.Convert(source);
            if (baseResult != null)
                return baseResult;

            if (source is string str)
            {
                switch (str.ToUpperInvariant().Trim())
                {
                    case "TRUE":
                    case "YES":
                        return true;
                    case "FALSE":
                    case "NO":
                        return false;
                }

                if (KnownTrueString != null && string.Equals(str, KnownTrueString, StringComparison.InvariantCultureIgnoreCase))
                    return true;

                if (KnownFalseString != null && string.Equals(str, KnownFalseString, StringComparison.InvariantCultureIgnoreCase))
                    return false;
            }

            return null;
        }
    }
}