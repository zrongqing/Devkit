//Bank class added by the syncfusion
using Syncfusion.Windows.PropertyGrid;
using System.ComponentModel;
using System.Net;

namespace Devkit.Model
{
    public class Bank
    {
        [DisplayNameAttribute("Name")]
        [DescriptionAttribute("Name of the bank.")]
        public string Name
        {
            get;
            set;
        }

        [DisplayNameAttribute("Customer ID")]
        [DescriptionAttribute("Customer ID used for Net Banking.")]
        public string CustomerID
        {
            get;

            set;
        }

        [DescriptionAttribute("Password used for Net Banking.")]
        [ReadOnly(true)]
        public string Password
        {
            get;

            set;
        }

        [DisplayNameAttribute("Account Number")]
        [DescriptionAttribute("Bank Account Number.")]
        public long AccountNumber
        {
            get;

            set;
        }


        [DescriptionAttribute("Address of Bank.")]
        [ReadOnly(true)]
        public Address Address
        {
            get;

            set;
        }
        public override string ToString()
        {
            return Name;
        }
    }

    public enum BankGender
    {
        Male,

        Female
    }

    public enum BankCountry
    {
        USA,

        Germany,

        Canada,

        Mexico,

        Alaska,

        Japan,

        China,

        Peru,

        Chile,

        Argentina,

        Brazil

    }
}