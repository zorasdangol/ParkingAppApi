using ParkingStandardLibrary.Helpers;
using System;
using System.Collections.Generic;
using System.Text;

namespace ParkingStandardLibrary.Models
{
    public class User:BaseModel
    {
        public string UserName { get; set; }
        public string Password { get; set; }
        public string UniqueID { get; set; }
                
        public string ip1 { get; set; }
        public string ip2 { get; set; }
        public string ip3 { get; set; }
        public string ip4 { get; set; }
        public string Port { get; set; }

        public int Session { get; set; }
        public byte STATUS { get; set; }
        private byte _DESKTOP_ACCESS;
        public byte DESKTOP_ACCESS { get { return _DESKTOP_ACCESS; } set { _DESKTOP_ACCESS = value; OnPropertyChanged("DESKTOP_ACCESS"); } }

        private byte _MOBILE_ACCESS;
        public byte MOBILE_ACCESS { get { return _MOBILE_ACCESS; } set { _MOBILE_ACCESS = value; OnPropertyChanged("MOBILE_ACCESS"); } }

        public string MemberBarcodePrefix { get; set; }
        public string CompanyName { get; set; }
        public string CompanyAddress { get; set; }

        public string _Salt;
        public string SALT { get { return _Salt; } set { _Salt = value; OnPropertyChanged("SALT"); } }

        int _UserID;
        public int UID { get { return _UserID; } set { _UserID = value; OnPropertyChanged("UID"); } }

        public string Remarks { get; set; }

        //public string IPAddress { get; set; }

        public User()
        {
            UserName = "";
            Password = "";
            ip1 = "";
            ip2 =
            ip3 = "";
            ip4 = "";
            Port = "";
            //IPAddress = "";
            UniqueID = "";
            Remarks = "";
        }
    }
}
