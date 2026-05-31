using System.Windows.Controls;

namespace DevKitApp.Core.Prism;

public class TabControlRegionAdapter : RegionAdapterBase<TabControl>
{
    public TabControlRegionAdapter(IRegionBehaviorFactory factory)
        : base(factory)
    {
    }

    protected override void Adapt(IRegion region, TabControl regionTarget)
    {
        region.Views.CollectionChanged += (s, e) =>
        {
            if (e.NewItems != null)
            {
                foreach (var view in e.NewItems)
                {
                    var tab = new TabItem
                    {
                        Header = view.GetType().Name,
                        Content = view
                    };

                    regionTarget.Items.Add(tab);
                    regionTarget.SelectedItem = tab;
                }
            }
        };
    }

    protected override IRegion CreateRegion()
    {
        return new AllActiveRegion();
    }
}
