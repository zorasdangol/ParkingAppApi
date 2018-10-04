using Dapper;
using ParkingStandardLibrary.Models;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Text;

namespace ParkingStandardLibrary.Helpers
{
    public struct PSlipTerms
    {
        public string Description { get; set; }
        public byte Height { get; set; }
    }

    public static class GlobalClass
    {
        public static string DataConnectionString;
        //public static string Terminal;
        public static int GraceTime;
        public static string CompanyName;
        public static string CompanyAddress;
        public static string CompanyPan;
        public static decimal VAT = 13;
        public static User User;
        public static string PrinterName = "POS80";
        //public static PrintQueue printer;
        //public static double RateTimeLinePeriodWidth = (System.Windows.Forms.Screen.PrimaryScreen.Bounds.Right - 150) / 13;
        //public static Thickness FirstPeriodMargin = new Thickness(RateTimeLinePeriodWidth / 2, 0, 0, 0);
        public static DateTime BeginTime { get { return new DateTime(1900, 1, 1, 0, 0, 0, 0); } }
        public static DateTime EndTime { get { return new DateTime(1900, 1, 1, 23, 59, 59); } }
        public static IEnumerable<PSlipTerms> TCList;
        //public static RateMaster DefaultRate;
        public static int Session;
        //public static Visibility ShowCollectionAmountInCashSettlement;
        //public static Visibility DisableCashAmountChange;
        //public static Visibility DiscountVisible;
        //public static Visibility StampVisible;
        //public static Visibility StaffVisible;
        public static bool EnablePlateNo { get; set; }
        public static byte AllowMultiVehicleForStaff;
        public static short DefaultMinVacantLot;
        public static string MemberBarcodePrefix;
        public static byte FYID = 3;
        public static string FYNAME;
        public static byte SettlementMode;
        public static string AppDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "\\IMS\\Parking";
        public static decimal AbbTaxInvoiceLimit = 5000;
        internal static bool NoRawPrinter;

        public static string ReportName { get; set; }
        public static string ReportParams { get; set; }
        public static string PrintTime { get; set; }
        public static byte SlipPrinterWith { get; set; }

        //public static string DataConnectionString;
        public static string Division = "MMX";
        public static string Terminal = "001";
        
        public static Exception LastException;
        public static string GetTime = "(SELECT CONVERT(VARCHAR,(SELECT GETDate()),8))";
        public static string GetDate = "(SELECT GETDATE())";
        //public static string DataConnectionString { get { return ConfigurationManager.ConnectionStrings["DBSETTING"].ConnectionString; } }
        private static SqlConnection CnnMain;
        //public static IEnumerable<PSlipTerms> TCList;

        //public static User User { get; set; }

        static GlobalClass()
        {
            try
            {
                DataConnectionString = ConnectionDbInfo.ConnectionString;

                using (SqlConnection cnmain = new SqlConnection(DataConnectionString))
                {
                    if (File.Exists(Environment.SystemDirectory + "\\ParkingDbCon.dll"))
                    {
                        try
                        {
                            dynamic connProps = Newtonsoft.Json.JsonConvert.DeserializeObject<dynamic>(File.ReadAllText(Environment.SystemDirectory + "\\ParkingDbCon.dll"));
                            Terminal = Encrypt(connProps.TERMINAL.ToString());
                        }catch(Exception ex)
                        {
                            writeErrorToExternalFile(ex.Message, "GlobalClass:Terminal");
                        }
                    }
                    
                    dynamic Setting = cnmain.Query("SELECT CompanyName, CompanyAddress, CompanyInfo, ISNULL(GraceTime, 5) GraceTime, ISNULL(ShowCollectionAmountInCashSettlement, 0) ShowCollectionAmountInCashSettlement, ISNULL(DisableCashAmountChange,0) DisableCashAmountChange, SettlementMode, ISNULL(AllowMultiVehicleForStaff,0) AllowMultiVehicleForStaff, ISNULL(SlipPrinterWidth, 58) SlipPrinterWidth, ISNULL(EnableStaff, 0) EnableStaff, ISNULL(EnableStamp, 0) EnableStamp, ISNULL(EnableDiscount, 0) EnableDiscount, ISNULL(EnablePlateNo, 0) EnablePlateNo, MemberBarcodePrefix FROM tblSetting").First();
                    CompanyName = Setting.CompanyName;
                    CompanyAddress = Setting.CompanyAddress;
                    CompanyPan = Setting.CompanyInfo;
                    GraceTime = Setting.GraceTime;
                    SettlementMode = Setting.SettlementMode;
                    SlipPrinterWith = Setting.SlipPrinterWidth;
                    EnablePlateNo = (bool)Setting.EnablePlateNo;
                    MemberBarcodePrefix = Setting.MemberBarcodePrefix;
                    AllowMultiVehicleForStaff = (byte)Setting.AllowMultiVehicleForStaff;
                    TCList = cnmain.Query<PSlipTerms>("SELECT Description, Height from PSlipTerms");
                    FYID = cnmain.ExecuteScalar<byte>("SELECT FYID FROM tblFiscalYear WHERE CONVERT(VARCHAR,GETDATE(),101) BETWEEN BEGIN_DATE AND END_DATE");
                    FYNAME = cnmain.ExecuteScalar<string>("SELECT FYNAME FROM tblFiscalYear WHERE CONVERT(VARCHAR,GETDATE(),101) BETWEEN BEGIN_DATE AND END_DATE");
                }
            }
            catch (Exception ex)
            {
                writeErrorToExternalFile(ex.Message, "GlobalClass:Costructor");
            }
        }

        public static string Encrypt(string Text, string Key="AmitLalJoshi")
        {
            int i;
            string TEXTCHAR;
            string KEYCHAR;
            string encoded = string.Empty;
            for (i = 0; i < Text.Length; i++)
            {
                TEXTCHAR = Text.Substring(i, 1);
                var keysI = ((i + 1) % Key.Length);
                KEYCHAR = Key.Substring(keysI);
                var encrypted = Microsoft.VisualBasic.Strings.AscW(TEXTCHAR) ^ Microsoft.VisualBasic.Strings.AscW(KEYCHAR);
                encoded += Microsoft.VisualBasic.Strings.ChrW(encrypted);
            }
            return encoded;
        }


        public static string GetEncryptedPWD(string pwd, ref string Salt)
        {

            StringBuilder sBuilder;

            if (string.IsNullOrEmpty(Salt))
            {
                System.Security.Cryptography.RNGCryptoServiceProvider rng = new System.Security.Cryptography.RNGCryptoServiceProvider();
                byte[] saltByte = new byte[8];
                rng.GetNonZeroBytes(saltByte);

                sBuilder = new StringBuilder();

                // Loop through each byte of the hashed data 
                // and format each one as a hexadecimal string.
                for (int i = 0; i < saltByte.Length; i++)
                {
                    sBuilder.Append(saltByte[i].ToString("x2"));
                }

                Salt = sBuilder.ToString();
            }

            System.Security.Cryptography.SHA256CryptoServiceProvider sha = new System.Security.Cryptography.SHA256CryptoServiceProvider();
            //System.Security.Cryptography.MD5CryptoServiceProvider x = new System.Security.Cryptography.MD5CryptoServiceProvider();
            byte[] data = System.Text.Encoding.ASCII.GetBytes(pwd + Salt);
            data = sha.ComputeHash(data);

            sBuilder = new StringBuilder();

            // Loop through each byte of the hashed data 
            // and format each one as a hexadecimal string.
            for (int i = 0; i < data.Length; i++)
            {
                sBuilder.Append(data[i].ToString("x2"));
            }
            return sBuilder.ToString();
        }

        public static DateTime GetAdDate(string BS)
        {
            try
            {
                DateTime AdDate;
                if (CnnMain.State == ConnectionState.Closed) CnnMain.Open();
                SqlCommand Cmd = new SqlCommand("Select AD from DATEMITI where MITI='" + BS + "'", CnnMain);
                using (SqlDataReader dr = Cmd.ExecuteReader())
                {
                    if (dr.Read())
                    {
                        AdDate = Convert.ToDateTime(dr["AD"]);
                        return AdDate;
                    }
                    else
                    {
                        throw new Exception(string.Format("Miti ({0}) is out of range.", BS));
                    }
                }
            }
            catch (Exception ex)
            {
                throw ex;
            }
            finally
            {
                CnnMain.Close();
            }
        }

        public static string GetBSDate(DateTime Adate)
        {
            try
            {
                using (SqlConnection Con = new SqlConnection(Helpers.ConnectionDbInfo.ConnectionString))
                {
                    return Con.ExecuteScalar<string>("select dbo.dateToMiti('" + Adate.ToString("dd/MMM/yyyy") + "','/')");
                }
            }
            catch (Exception ex)
            {
                writeErrorToExternalFile(ex.Message, "GlobalClass:GetBSDate");
                return string.Empty;
            }
        }

        public static bool StartSession()
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(Helpers.ConnectionDbInfo.ConnectionString))
                {
                    Session = (int)conn.ExecuteScalar(string.Format
                         (
                             @"INSERT INTO [SESSION] ( SESSION_ID, [START_DATE], TERMINAL_CODE, [UID], SESSION_CREATE_MODE, HostName)
                                OUTPUT INSERTED.SESSION_ID
                                VALUES ((SELECT ISNULL(MAX(SESSION_ID),0) + 1 FROM SESSION), GETDATE(), '{0}', {1}, 'LOGIN', HOST_NAME())", Terminal, User.UID
                         ));
                    return true;
                }
            }
            catch (Exception ex)
            {
                writeErrorToExternalFile(ex.Message, "GlobalClass:StartSession");
                return false;
            }
        }


        public static string GetInvoiceNo(string VNAME)
        {
            using (SqlConnection conn = new SqlConnection(ConnectionDbInfo.ConnectionString))
            {
                string invoice = conn.ExecuteScalar<string>("SELECT CurNo FROM tblSequence WHERE VNAME = @VNAME AND FYID = @FYID", new { VNAME = VNAME, FYID = GlobalClass.FYID });
                if (string.IsNullOrEmpty(invoice))
                {
                    conn.Execute("INSERT INTO tblSequence(VNAME, FYID, CurNo) VALUES(@VNAME, @FYID, 1)", new { VNAME = VNAME, FYID = GlobalClass.FYID });
                    invoice = "1";
                }
                return invoice;
            }
        }

        internal static string GetNumToWords(SqlConnection conn, decimal GrossAmount)
        {
            string InWords = "Rs. " + conn.ExecuteScalar<string>("SELECT DBO.Num_ToWordsArabic(" + Math.Floor(GrossAmount) + ")");
            if (GrossAmount > Math.Floor(GrossAmount))
                InWords += " & " + conn.ExecuteScalar<string>("SELECT DBO.Num_ToWordsArabic(" + GrossAmount.ToString("#0.00").Split('.')[1] + ")") + " Paisa";
            return InWords;
        }

        public static string GetInvoiceNo(string VNAME, SqlTransaction tran)
        {
            string invoice = tran.Connection.ExecuteScalar<string>("SELECT CurNo FROM tblSequence WHERE VNAME = @VNAME AND FYID = @FYID", new { VNAME = VNAME, FYID = GlobalClass.FYID }, tran);
            if (string.IsNullOrEmpty(invoice))
            {
                tran.Connection.Execute("INSERT INTO tblSequence(VNAME, FYID, CurNo) VALUES(@VNAME, @FYID, 1)", new { VNAME = VNAME, FYID = GlobalClass.FYID }, tran);
                invoice = "1";
            }
            return invoice;
        }

        public static void SetUserActivityLog(SqlTransaction tran, string FormName, string TrnMode, string WorkDetail = "", string VCRHNO = "", string Remarks = "")
        {

            tran.Connection.Execute
                (string.Format
                    (
                        @"INSERT INTO tblUserWorkingLog(LogID,UserId, UserSessId, FormName, TrnDate, TrnTime, TrnMode, WorkDetail, VchrNo, Remarks)
                                VALUES ((SELECT ISNULL(MAX(LogID),0) + 1 FROM tblUserWorkingLog), '{0}', {1}, '{2}',  CONVERT(VARCHAR,GETDATE(),101), 
                                CONVERT(VARCHAR, GETDATE(), 108), '{3}', '{4}', '{5}','{6}')", User.UserName, Session, FormName, TrnMode, WorkDetail, VCRHNO, Remarks
                    ), transaction: tran
                );
        }

        public static void GetUser(int UID)
        {
            try
            {
                using(SqlConnection conn = new SqlConnection(DataConnectionString))
                {
                    var user = conn.Query<User>(string.Format("SELECT UID, UserName, [Password], FullName, UserCat, [STATUS], DESKTOP_ACCESS, MOBILE_ACCESS, SALT  FROM USERS WHERE UID = {0}",UID)).FirstOrDefault();
                    if (user != null)
                    {
                        GlobalClass.User = user;
                    }
                }
            }
            catch 
            {

            }
        }

        public static void writeErrorToExternalFile(string errorMessage, string SOURCE, string DIVISION = "", string TABLE = "")
        {
            try
            {
                using (SqlConnection con = new SqlConnection(DataConnectionString))
                {
                    con.Execute(@"IF not exists(SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'DATACOLLECTORAPI_ERRORS')
                                        CREATE TABLE DATACOLLECTORAPI_ERRORS(
                                            DATE datetime,
                                            DIVISION varchar(20) NULL,
                                            [TABLE] VARCHAR(50),
                                            [SOURCE] VARCHAR(100),
											ERRORMESSAGE VARCHAR(MAX)
											)");
                    con.Execute("INSERT INTO DATACOLLECTORAPI_ERRORS (DATE,DIVISION,[TABLE],[SOURCE],ERRORMESSAGE) VALUES(@DATE,@DIVISION,@TABLE,@SOURCE,@ERRORMESSAGE)", new { DATE = DateTime.Now, DIVISION = DIVISION, TABLE = TABLE, ERRORMESSAGE = errorMessage, SOURCE = SOURCE });
                }

            }
            catch (Exception ex)
            {
                //if (File.Exists(Path.Combine(HostingEnvironment.ApplicationPhysicalPath, "masterSyncErrors.txt")))
                //{
                //    using (StreamWriter sw = new StreamWriter(Path.Combine(HostingEnvironment.ApplicationPhysicalPath, "masterSyncErrors.txt"), true))
                //    {
                //        sw.WriteLine(" Date : " + DateTime.Now + "  Error : " + ex.Message);
                //    }
                //}
            }
        }

    }
}
