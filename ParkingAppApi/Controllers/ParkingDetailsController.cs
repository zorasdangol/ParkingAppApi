using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using ParkingStandardLibrary.Business;
using ParkingStandardLibrary.Models;

namespace ParkingAppApi.Controllers
{
    public class ParkingDetailsController : Controller
    {
        [Route("api/CheckParkingSlip")]
        [HttpPost]
        public FunctionResponse CheckParkingSlip([FromBody]BarCodeTransfer obj)
        {
            return new ParkingOutMethods().CheckParkingSlip(obj);
        }


        [Route("api/CheckVoucherCode")]
        [HttpPost]
        public FunctionResponse CheckVoucherCode([FromBody] BarCodeTransfer obj )
        {
            return new ParkingOutMethods().ValidateVoucher(obj);            
        }

        [Route("api/CheckMemberCode")]
        [HttpPost]
        public FunctionResponse CheckMemberCode([FromBody] BarCodeTransfer obj)
        {
            return new ParkingOutMethods().ValidateMember(obj);
        }



        [Route("api/SavePOUT")]
        [HttpPost]
        public FunctionResponse SavePOUT([FromBody] BarCodeTransfer obj)
        {
            return new ParkingOutMethods().ExecuteSave(obj);

        }


        [Route("api/SaveStaffOrStampPOUT")]
        [HttpPost]
        public FunctionResponse SaveStaffOrStampPOUT([FromBody] BarCodeTransfer obj)
        {
            return new ParkingOutMethods().ValidateStaffOrStamp(obj);

        }



    }
}