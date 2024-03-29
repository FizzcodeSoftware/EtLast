﻿namespace FizzCode.EtLast;

[EditorBrowsable(EditorBrowsableState.Never)]
public abstract class AbstractSimpleChangeMutator : AbstractMutator
{
    protected List<KeyValuePair<string, object>> Changes;

    protected AbstractSimpleChangeMutator()
    {
    }

    protected override void StartMutator()
    {
        base.StartMutator();
        Changes = [];
    }

    protected override void CloseMutator()
    {
        if (Changes != null)
        {
            Changes.Clear();
            Changes = null;
        }
    }
}
