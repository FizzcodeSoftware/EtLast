﻿namespace FizzCode.EtLast;

public delegate void NoMatchActionDelegate(IRow row);

public sealed class NoMatchAction(MatchMode mode)
{
    public MatchMode Mode { get; } = mode;
    public NoMatchActionDelegate CustomAction { get; init; }

    public void InvokeCustomAction(IRow row)
    {
        try
        {
            var tracker = new TrackedRow(row);
            CustomAction?.Invoke(tracker);
            tracker.ApplyChanges();
        }
        catch (Exception ex)
        {
            throw new NoMatchActionDelegateException(row.Owner, row, ex);
        }
    }
}
