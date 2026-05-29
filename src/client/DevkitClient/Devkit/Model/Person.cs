//Person class added by the syncfusion
using Syncfusion.Windows.PropertyGrid;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Net;
using System.Security.RightsManagement;
using System.Windows.Media;

namespace Devkit.Model
{
    
    
        [PropertyGridAttribute(NestedPropertyDisplayMode = NestedPropertyDisplayMode.None,
                           PropertyName = "DOB,FavoriteColor")]
        [TypeConverter(typeof(ExpandableObjects))]
        [Editor("Mobile", typeof(MobileEditor))]
    public class Person
    {
        
        [CategoryAttribute("Identity")]
        [DisplayNameAttribute("First Name")]
        [DescriptionAttribute("First Name of the actual person.")]
        public string FirstName { get; set; }

        
        [CategoryAttribute("Identity")]
        [DisplayNameAttribute("Last Name")]
        [DescriptionAttribute("Last Name of the actual person.")]
        public string LastName { get; set; }

       
        [Browsable(false)]
        public string MaritalStatus { get; set; }

        
        [CategoryAttribute("Additional Info")]
        [DescriptionAttribute("Bank in which the person has account.")]
        [ReadOnly(true)]
        public Bank Bank { get; set; }

        
        [CategoryAttribute("Additional Info")]
        [DisplayNameAttribute("Email IDs")]
        [Mask(MaskAttribute.EmailId)]
        [DescriptionAttribute("Emails of the actual person.")]
        public List<string> Email { get; set; }

        
        [CategoryAttribute("Identity")]
        [DescriptionAttribute("Age of the actual person.")]
        public int Age { get; set; }

        
        [CategoryAttribute("Identity")]
        [DisplayNameAttribute("Date of Birth")]
        [DescriptionAttribute("Birth date of the actual person.")]
        public DateTime DOB { get; set; }

        
        [CategoryAttribute("Identity")]
        [DescriptionAttribute("Gender information of the actual person.")]
        public BankGender Gender { get; set; }

        
        [CategoryAttribute("Additional Info")]
        [DisplayNameAttribute("Favorite Color")]
        [DescriptionAttribute("Favorite color of the actual person.")]
        public Brush FavoriteColor { get; set; }

        
        [CategoryAttribute("Additional Info")]
        [DisplayNameAttribute("Permanent Employee")]
        [DescriptionAttribute("Determines whether the person is permanent or not.")]
        public bool IsPermanent { get; set; }

        
        [CategoryAttribute("Additional Info")]
        [DescriptionAttribute("License key for the person.")]
        [Bindable(false)]
        public string Key { get; set; }

       
        [ReadOnly(true)]
        [CategoryAttribute("Identity")]
        [DescriptionAttribute("ID of the actual person.")]
        public string ID { get; set; }

       
        [CategoryAttribute("Additional Info")]
        [DescriptionAttribute("Country where the actual person is located.")]
        public BankCountry Country { get; set; }

        
        [CategoryAttribute("Additional Info")]
        [DisplayNameAttribute("Mobile Number")]
        [DescriptionAttribute("Contact Number of the actual person.")]
        public object Mobile { get; set; }

        public Person()
        {
            FirstName = "Carl";
            LastName = "Johnson";
            Age = 30;
            Mobile = 91983467382;
            Email = new List<string>() { "carljohnson@gmail.com", "cj@hotmail.com" };
            ID = "0005A";
            FavoriteColor = Brushes.Gray;
            IsPermanent = true;
            DOB = new DateTime(1987, 10, 16);
            Key = "dasd798@79hiujodsa';psdoiu9*(Uj0JK)(";
            Bank = new Bank()
            {
                Name = "Centura Banks",
                AccountNumber = 00987453721,
                CustomerID = "carljohnson",
                Password = "nuttertools",
                Address = new Address()
                {
                    State = "New Yark",
                    DoorNo = 87,
                    StreetName = "Martin street"
                }
            };
        }

    }
}
