﻿namespace FizzCode.EtLast
{
    using System;

    public delegate bool CustomRowEqualityComparerDelegate(IRow leftRow, IRow rightRow);

    public class CustomRowEqualityComparer : IRowEqualityComparer
    {
        public CustomRowEqualityComparerDelegate ComparerDelegate { get; set; }

        public bool Equals(IRow leftRow, IRow rightRow)
        {
            if (ComparerDelegate == null)
                throw new ArgumentException(nameof(ComparerDelegate) + " can not be null");

            return ComparerDelegate.Invoke(leftRow, rightRow);
        }
    }
}