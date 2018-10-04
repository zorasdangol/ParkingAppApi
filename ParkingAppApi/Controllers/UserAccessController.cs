using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using ParkingStandardLibrary.Business;
using ParkingStandardLibrary.Models;

namespace ParkingAppApi.Controllers
{
    public class UserAccessController : Controller
    {
        [Route("api/UserVerification")]
        [HttpPost]
        public FunctionResponse postuserVerification([FromBody]User User)
        {
            return new UserAccessMethods().postuserVerification(User);
        }
    }
}