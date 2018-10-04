using Dapper;
using ParkingStandardLibrary.Helpers;
using ParkingStandardLibrary.Models;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace ParkingStandardLibrary.Business
{
    public class UserAccessMethods
    {
        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool SetLocalTime(ref SYSTEMTIME st);


        public FunctionResponse postuserVerification(User User)
        {
            try
            {
                string USERNAME = User.UserName;
                string PASSWORD = User.Password;
                string UNIQUEID = User.UniqueID;
                string encPassword;
                string key = "AmitLalJoshi";

                using (SqlConnection conn = new SqlConnection(ConnectionDbInfo.ConnectionString))
                {
                    var user = conn.Query<User>(string.Format("SELECT UID, UserName, [Password], FullName, UserCat, [STATUS], DESKTOP_ACCESS, MOBILE_ACCESS, SALT  FROM USERS WHERE UserName = '{0}'", USERNAME)).FirstOrDefault();
                    if (user == null)
                    {
                        return new FunctionResponse() { status = "error", Message = "Invalid UserName or Password" };
                    }
                    if (user.Password != GlobalClass.GetEncryptedPWD(PASSWORD, ref user._Salt))
                    {
                       return new FunctionResponse() { status = "error", Message = "Invalid username or password" };
                    }

                    if (user.MOBILE_ACCESS != 1)
                    {
                        return new FunctionResponse() { status = "error", Message = "You do not have privilage to access this application" };
                        
                    }

                    if (user.STATUS != 0)
                    {
                        return new FunctionResponse() { status = "error", Message = "You no longer have privilage to access this application" };
                       
                    }

                    GlobalClass.User = user;
                    if (!GlobalClass.StartSession())
                    {
                        return new FunctionResponse() { status = "error", Message = "Session Couldnot be started" };
                    }

                    var curDate = conn.ExecuteScalar<DateTime>("SELECT GETDATE()");
                    // MessageBox.Show(curDate.ToString("MM/dd/yyyy hh:mm tt"));
                    //if (curDate.Subtract(DateTime.Now) > new TimeSpan(0, 0, 5) || DateTime.Now.Subtract(curDate) > new TimeSpan(0, 0, 5))
                    //    SetSystemTime(curDate);

                    user.Session = GlobalClass.Session;
                    user.CompanyName = GlobalClass.CompanyName;
                    user.CompanyAddress = GlobalClass.CompanyAddress;
                    user.MemberBarcodePrefix = GlobalClass.MemberBarcodePrefix;
                    return new FunctionResponse() { status = "ok", result = user };
                
                }
            }
            catch(Exception ex)
            {
                return new FunctionResponse() { status = "error", Message = ex.Message };
            }
        }

        //private void SetSystemTime(DateTime SERVERDATE)
        //{
        //    SYSTEMTIME st = new SYSTEMTIME
        //    {
        //        wYear = short.Parse(SERVERDATE.Year.ToString()),
        //        wMonth = short.Parse(SERVERDATE.Month.ToString()),
        //        wDay = short.Parse(SERVERDATE.Day.ToString()),
        //        wDayOfWeek = (short)SERVERDATE.DayOfWeek,
        //        wHour = short.Parse(SERVERDATE.Hour.ToString()),
        //        wMinute = short.Parse(SERVERDATE.Minute.ToString()),
        //        wSecond = short.Parse(SERVERDATE.Second.ToString()),
        //        wMilliseconds = short.Parse(SERVERDATE.Millisecond.ToString())
        //    };
        //    if (!SetLocalTime(ref st))
        //        MessageBox.Show("Could not sync system date with server date", "Login", MessageBoxButton.OK);
        //}

        [StructLayout(LayoutKind.Sequential)]
        struct SYSTEMTIME
        {
            public short wYear;
            public short wMonth;
            public short wDayOfWeek;
            public short wDay;
            public short wHour;
            public short wMinute;
            public short wSecond;
            public short wMilliseconds;
        }

    }
}
