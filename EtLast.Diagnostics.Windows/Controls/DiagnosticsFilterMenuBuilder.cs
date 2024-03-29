﻿namespace FizzCode.EtLast.Diagnostics.Windows;

public class DiagnosticsFilterMenuBuilder : FilterMenuBuilder
{
    private bool _insideHandleItemCheckedWrapped;

    static DiagnosticsFilterMenuBuilder()
    {
        FILTERING_LABEL = "Column filtering";
    }

    public override ToolStripDropDown MakeFilterMenu(ToolStripDropDown strip, ObjectListView listView, OLVColumn column)
    {
        if (!column.UseFiltering || column.ClusteringStrategy == null || listView.Objects == null)
            return strip;

        var clusters = Cluster(column.ClusteringStrategy, listView, column);
        if (clusters.Count > 0)
        {
            SortClusters(column.ClusteringStrategy, clusters);
            var columnFilteringMenuItem = CreateColumnFilteringMenuItem(column, clusters);
            strip.Items.Add(columnFilteringMenuItem);
        }

        return strip;
    }

    private ToolStripMenuItem CreateColumnFilteringMenuItem(OLVColumn column, List<ICluster> clusters)
    {
        var listBox = new ToolStripCheckedListBox
        {
            Tag = column,
        };

        foreach (var cluster in clusters)
        {
            listBox.AddItem(cluster, column.ValuesChosenForFiltering.Contains(cluster.ClusterKey));
        }

        var state = listBox.CheckedItems.Count == 0
            ? CheckState.Unchecked
            : listBox.CheckedItems.Count == clusters.Count
                ? CheckState.Checked
                : CheckState.Indeterminate;

        listBox.AddItem(SELECT_ALL_LABEL, state);

        listBox.ItemCheck += HandleItemCheckedWrapped;

        var clearAllMenuItem = new ToolStripMenuItem(CLEAR_ALL_FILTERS_LABEL, ClearFilteringImage, (sender, args) => ClearAllFilters(column));

        var applMenuItem = new ToolStripMenuItem(APPLY_LABEL, FilteringImage, (sender, args) => ApplyFilter(listBox, column));

        var freeText = new ToolStripTextBoxWithPlaceHolderText()
        {
            Tag = column,
            Text = "",
            PlaceHolderText = "search in column",
        };

        var menuItem = new ToolStripMenuItem(FILTERING_LABEL, null,
            [
                freeText,
                new ToolStripSeparator(),
                clearAllMenuItem,
                new ToolStripSeparator(),
                listBox,
                applMenuItem
            ]);

        return menuItem;
    }

    private void HandleItemCheckedWrapped(object sender, ItemCheckEventArgs e)
    {
        if (_insideHandleItemCheckedWrapped)
            return;

        try
        {
            _insideHandleItemCheckedWrapped = true;
            ItemChecked(sender, e);
        }
        finally
        {
            _insideHandleItemCheckedWrapped = false;
        }
    }

    private void ItemChecked(object sender, ItemCheckEventArgs e)
    {
        if (sender is not ToolStripCheckedListBox checkedList || checkedList.Tag is not OLVColumn column || column.ListView is not ObjectListView)
            return;

        var selectAllIndex = checkedList.Items.IndexOf(SELECT_ALL_LABEL);
        if (selectAllIndex >= 0)
            HandleSelectAll(e, checkedList, selectAllIndex);
    }

    private static void HandleSelectAll(ItemCheckEventArgs e, ToolStripCheckedListBox checkedList, int selectAllIndex)
    {
        if (e.Index == selectAllIndex)
        {
            if (e.NewValue == CheckState.Checked)
                checkedList.CheckAll();

            if (e.NewValue == CheckState.Unchecked)
                checkedList.UncheckAll();

            return;
        }

        var count = checkedList.CheckedItems.Count;

        if (checkedList.GetItemCheckState(selectAllIndex) != CheckState.Unchecked)
            count--;

        if (e.NewValue != e.CurrentValue)
        {
            if (e.NewValue == CheckState.Checked)
                count++;
            else
                count--;
        }

        if (count == 0)
            checkedList.SetItemState(selectAllIndex, CheckState.Unchecked);
        else if (count == checkedList.Items.Count - 1)
            checkedList.SetItemState(selectAllIndex, CheckState.Checked);
        else
            checkedList.SetItemState(selectAllIndex, CheckState.Indeterminate);
    }

    private void ApplyFilter(ToolStripCheckedListBox checkedList, OLVColumn column)
    {
        if (column.ListView is not ObjectListView olv || olv.IsDisposed)
            return;

        var textBox = (checkedList.GetCurrentParent() as ToolStripDropDownMenu).Items[0] as ToolStripTextBox;
        if (!string.IsNullOrWhiteSpace(textBox.Text))
        {
            var filter = TextMatchFilter.Contains(olv, textBox.Text);
            filter.Columns = [column];
            column.ValueBasedFilter = filter;
            column.ValuesChosenForFiltering = new ArrayList();
            olv.UpdateColumnFiltering();
        }
        else
        {
            base.EnactFilter(checkedList, column);
        }
    }

    protected override void ClearAllFilters(OLVColumn column)
    {
        if (column.ListView is not ObjectListView listView || listView.IsDisposed)
            return;

        column.ValueBasedFilter = null;
        listView.ResetColumnFiltering();
    }
}
