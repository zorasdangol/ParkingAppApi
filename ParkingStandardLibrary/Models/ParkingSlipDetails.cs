using ParkingStandardLibrary.Helpers;
using System;
using System.Collections.Generic;
using System.Text;

namespace ParkingStandardLibrary.Models
{
    public class ParkingSlipDetails:BaseModel
    {
        public ParkingIn PIN { get; set; }
        public ParkingOut POUT { get; set; }
        public bool IsHoliday { get; set; }

        public ParkingSlipDetails()
        {
            PIN = new ParkingIn();
            POUT = new ParkingOut();
        }
    }
}
