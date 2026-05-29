//BankViewModel class added by the syncfusion
using Syncfusion.Windows.PropertyGrid;
using Syncfusion.Windows.Shared;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Security.RightsManagement;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Devkit.Model;

namespace Devkit.ViewModel
{
    public class BankViewModel : NotificationObject
    {
        private PropertyItem selectedPropertyItem;

        public PropertyItem SelectedPropertyItem
        {
            get { return selectedPropertyItem; }
            set
            {
                selectedPropertyItem = value;
                RaisePropertyChanged(nameof(SelectedPropertyItem));
            }
        }

        private bool enableGrouping;

        public bool EnableGrouping
        {
            get { return enableGrouping; }
            set
            {
                enableGrouping = value;
                this.RaisePropertyChanged(nameof(this.EnableGrouping));
            }
        }

        private bool enableToolTip;

        public bool EnableToolTip
        {
            get { return enableToolTip; }
            set
            {
                enableToolTip = value;
                this.RaisePropertyChanged(nameof(this.EnableToolTip));
            }
        }

        private Visibility buttonPanelVisibility = Visibility.Visible;

        public Visibility ButtonPanelVisibility
        {
            get { return buttonPanelVisibility; }
            set
            {
                buttonPanelVisibility = value;
                this.RaisePropertyChanged(nameof(this.ButtonPanelVisibility));
            }
        }

        private Visibility searchBoxVisibility = Visibility.Visible;

        public Visibility SearchBoxVisibility
        {
            get { return searchBoxVisibility; }
            set
            {
                searchBoxVisibility = value;
                this.RaisePropertyChanged(nameof(this.SearchBoxVisibility));
            }
        }

        private Visibility descriptionPanelVisibility = Visibility.Visible;

        public Visibility DescriptionPanelVisibility
        {
            get { return descriptionPanelVisibility; }
            set
            {
                descriptionPanelVisibility = value;
                this.RaisePropertyChanged(nameof(this.DescriptionPanelVisibility));
            }
        }

        private PropertyExpandModes propertyExpandMode = PropertyExpandModes.NestedMode;

        public PropertyExpandModes PropertyExpandMode
        {
            get { return propertyExpandMode; }
            set
            {
                propertyExpandMode = value;
                this.RaisePropertyChanged(nameof(this.PropertyExpandMode));
            }
        }

        private ListSortDirection? sortDirection = null;
        /// <summary>
        /// Gets or sets a value indicating the sort direction (Ascending/Desceding) of the properties.
        /// </summary>
        public ListSortDirection? SortDirection
        {
            get
            {
                return sortDirection;
            }

            set
            {
                sortDirection = value;
                RaisePropertyChanged(nameof(SortDirection));
            }
        }

        /// <summary>
        /// Property which stores PropertyNameColumnDefinition 
        /// </summary>
        private GridLength propertyNameColumnDefinition = new GridLength(250);
        public GridLength PropertyNameColumnDefinition
        {
            get
            {
                return propertyNameColumnDefinition;
            }
            set
            {
                propertyNameColumnDefinition = value;
                RaisePropertyChanged(nameof(PropertyNameColumnDefinition));
            }
        }

        public BankViewModel()
        {

        }
    
    }
}