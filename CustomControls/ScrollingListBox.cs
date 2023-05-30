using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;

namespace MAAS_BreastPlan_helper.CustomControls
{
    public class ScrollingListBox : ListBox
    {
        protected override void OnItemsChanged(System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            if (e != null)
            {
                if (e.NewItems != null)
                {
                    int newItemCount = e.NewItems.Count;

                    if (newItemCount > 0)
                        this.ScrollIntoView(e.NewItems[newItemCount - 1]);
                }
            
            }

            base.OnItemsChanged(e);
        }
    }
}
