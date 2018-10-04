using System;
using System.Collections.Generic;
using System.Text;

namespace ParkingStandardLibrary.Models
{
    public class BarCodeTransfer
    {
        public int Session { get; set; }
        public string barcode { get; set; }
        public List<Voucher> Vouchers { get; set; }
        public MemberDiscount mDiscount { get; set; }
        public ParkingIn PIN { get; set; }
        public ParkingOut POUT { get; set; }
        public bool IsHoliday { get; set; }

        public BarCodeTransfer()
        {
            barcode = "";
            Vouchers = new List<Voucher>();
            mDiscount = new MemberDiscount();
            PIN = new ParkingIn();
            POUT = new ParkingOut();
        }
    }
}
