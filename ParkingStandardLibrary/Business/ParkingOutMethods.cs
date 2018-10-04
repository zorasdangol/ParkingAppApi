using Dapper;
using ParkingStandardLibrary.Helpers;
using ParkingStandardLibrary.Models;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Text;
using System.Linq;
using System.Data;
using RawPrintFunctions;

namespace ParkingStandardLibrary.Business
{
    public class ParkingOutMethods:BaseModel
    {
        private string _InvoicePrefix;
        public string InvoicePrefix { get { return _InvoicePrefix; } set { _InvoicePrefix = value; OnPropertyChanged("InvoicePrefix"); } }

        private string _InvoiceNo;
        public string InvoiceNo { get { return _InvoiceNo; } set { _InvoiceNo = value; OnPropertyChanged("InvoiceNo"); } }

        private DateTime _CurDate;
        public DateTime CurDate { get { return _CurDate; } set { _CurDate = value; OnPropertyChanged("CurDate"); } }

        private bool _TaxInvoice;
        public bool TaxInvoice
        {
            get { return _TaxInvoice; }
            set
            {
                _TaxInvoice = value;
                OnPropertyChanged("TaxInvoice");
                InvoicePrefix = (value) ? "TI" : "SI";
                //if (_action != ButtonAction.RePrint)
                //    InvoiceNo = GlobalClass.GetInvoiceNo(InvoicePrefix);
            }
        }
        

        private string _CurTime;
        public string CurTime { get { return _CurTime; } set { _CurTime = value; OnPropertyChanged("CurTime"); } }
        
        public List<VoucherType> VoucherTypes;

        public ParkingOutMethods()
        {
            try
            {
                TaxInvoice = false;
                //nepDate = new DateConverter(GlobalClass.DataConnectionString);
                CurDate = DateTime.Today;

                CurTime = DateTime.Now.ToString("hh:mm tt");
               
                using (SqlConnection conn = new SqlConnection(GlobalClass.DataConnectionString))
                {
                    VoucherTypes = conn.Query<VoucherType>("SELECT VoucherId, VehicleType FROM VoucherTypes").ToList();
                }

            }catch(Exception e)
            {

            }
        }

        public  FunctionResponse CheckParkingSlip(BarCodeTransfer transferData)
        {
            try
            {
                string barcode = transferData.barcode;
                ParkingOut POUT = new ParkingOut();
                

                decimal ChargedHours = 0;
                decimal ChargedAmount = 0;
                using (SqlConnection conn = new SqlConnection(ConnectionDbInfo.ConnectionString))
                {
                    try
                    {
                        if (transferData.POUT != null && (transferData.POUT.PID) != 0)
                            transferData.POUT.SaveLog(conn);
                    }
                    catch { }


                    var PINS = conn.Query<ParkingIn>(@"SELECT PID, VehicleType, InDate, InMiti, InTime, PlateNo, Barcode, UID FROM ParkingInDetails 
WHERE((BARCODE <> '' AND  BARCODE = @barcode) OR(ISNULL(PLATENO, '') <> '' AND ISNULL(PlateNo, '') = @barcode))
AND FYID = @fyid",new { barcode, fyid = GlobalClass.FYID });
                    if (PINS.Count() <= 0)
                    {
                        return new FunctionResponse() { status = "error", Message = "Invalid barcode readings." };
                    }
                    ParkingIn PIN = PINS.First();
                    PIN.VType = conn.Query<VehicleType>(string.Format("SELECT VTypeId, Description FROM VehicleType WHERE VTypeId = {0}", PIN.VehicleType)).First();
                    var POUTS = conn.Query<ParkingOut>(string.Format("SELECT * FROM ParkingOutDetails WHERE PID = {0} AND FYID = {1}", PIN.PID, GlobalClass.FYID));
                    if (POUTS.Count() > 0)
                    {
                        return new FunctionResponse() { status = "error", Message = "Entity already exited" };
                    }
                    POUT.Rate_ID = (int)conn.ExecuteScalar("SELECT RATE_ID FROM RATEMASTER WHERE IsDefault = 1");

                    DateTime ServerTime = conn.ExecuteScalar<DateTime>("SELECT GETDATE()");
                    POUT.OutDate = ServerTime.Date;
                    POUT.OutTime = ServerTime.ToString("hh:mm:ss tt");
                    POUT.OutMiti = conn.ExecuteScalar<String>("SELECT MITI FROM DATEMITI WHERE AD = @AD", new { AD = ServerTime.Date });
                    POUT.Interval = GetInterval(PIN.InDate, POUT.OutDate, PIN.InTime, POUT.OutTime);
                    POUT.PID = PIN.PID;

                    CalculateParkingCharge(conn, PIN.InDate.Add(DateTime.Parse(PIN.InTime).TimeOfDay), POUT.OutDate.Add(DateTime.Parse(POUT.OutTime).TimeOfDay), POUT.Rate_ID, PIN.VehicleType, ref ChargedAmount, ref ChargedHours);
                    POUT.ChargedHours = ChargedHours;
                    POUT.ChargedAmount = ChargedAmount;
                    POUT.CashAmount = POUT.ChargedAmount;
                    PIN.Barcode = string.Empty;
                    bool IsHoliday = conn.ExecuteScalar<int>("SELECT COUNT(*) FROM Holiday WHERE HolidayDate = @HolidayDate", new { HolidayDate = POUT.OutDate }) > 0;
                    return new FunctionResponse() { status = "ok", result = new BarCodeTransfer() { barcode = barcode,Vouchers= new List<Voucher>(), mDiscount= new MemberDiscount(), POUT = POUT, PIN = PIN, IsHoliday =  IsHoliday } };
                }
            }
            catch (Exception ex)
            {
                return new FunctionResponse(){ status = "error", Message = ex.Message };
                
            }
        }

        public  FunctionResponse ExecuteSave( BarCodeTransfer obj)
        {
            int Session = obj.Session;
            List<Voucher> Vouchers = obj.Vouchers;
            string barcode = obj.barcode;
            MemberDiscount mDiscount = obj.mDiscount;
            ParkingOut POUT = obj.POUT;

            GlobalClass.GetUser(POUT.UID);

            string strSQL;
            decimal Taxable, VAT, Amount, Discount = 0, NonTaxable, Rate, Quantity;
            string BillNo = string.Empty;
            try
            {
                using (SqlConnection conn = new SqlConnection(ConnectionDbInfo.ConnectionString))
                {
                    conn.Open();
                    using (SqlTransaction tran = conn.BeginTransaction())
                    {
                        POUT.SESSION_ID =  Session;
                        POUT.FYID = GlobalClass.FYID;
                        POUT.Save(tran);
                        if (POUT.CashAmount > 0)
                        {
                            BillNo = InvoicePrefix + GlobalClass.GetInvoiceNo(InvoicePrefix, tran);
                            Quantity = POUT.ChargedHours;
                            if (Vouchers.Count > 0)
                                Discount = Vouchers.Sum(x => x.Value);
                            else if (mDiscount != null && !string.IsNullOrEmpty(mDiscount.MemberId))
                                Discount = mDiscount.DiscountAmount;
                            else if (POUT.CashAmount < POUT.ChargedAmount)
                            {
                                Discount = POUT.ChargedAmount - POUT.CashAmount;
                            }
                            Amount = POUT.ChargedAmount / (1 + (GlobalClass.VAT / 100));
                            Discount = Discount / (1 + (GlobalClass.VAT / 100));
                            Rate = Amount / Quantity;
                            NonTaxable = 0;
                            Taxable = Amount - (NonTaxable + Discount);
                            VAT = Taxable * GlobalClass.VAT / 100;

                            TParkingSales PSales = new TParkingSales
                            {
                                BillNo = BillNo,
                                TDate = POUT.OutDate,
                                TMiti = POUT.OutMiti,
                                TTime = POUT.OutTime,
                                BillTo = POUT.BILLTO,
                                BILLTOADD = POUT.BILLTOADD,
                                BILLTOPAN = POUT.BILLTOPAN,
                                Amount = Amount,
                                Discount = Discount,
                                NonTaxable = NonTaxable,
                                Taxable = Taxable,
                                VAT = VAT,
                                GrossAmount = POUT.CashAmount,
                                PID = POUT.PID,
                                UID = POUT.UID,
                                SESSION_ID = POUT.SESSION_ID,
                                FYID = GlobalClass.FYID,
                                TaxInvoice = TaxInvoice
                            };
                            PSales.Save(tran);
                            TParkingSalesDetails PSalesDetails = new TParkingSalesDetails
                            {
                                BillNo = BillNo,
                                PType = 'P',
                                Description = "Parking Charge",
                                FYID = GlobalClass.FYID,
                                Quantity = Quantity,
                                Rate = Rate,
                                Amount = Amount,
                                Discount = Discount,
                                NonTaxable = NonTaxable,
                                Taxable = Taxable,
                                VAT = VAT,
                                NetAmount = POUT.CashAmount,
                            };
                            PSalesDetails.Save(tran);

                            conn.Execute("UPDATE tblSequence SET CurNo = CurNo + 1 WHERE VNAME = @VNAME AND FYID = @FYID", new { VNAME = InvoicePrefix, FYID = GlobalClass.FYID }, transaction: tran);


                            GlobalClass.SetUserActivityLog(tran, "Exit", "New", VCRHNO: BillNo, WorkDetail: "Bill No : " + BillNo);
                            //SyncFunctions.LogSyncStatus(tran, BillNo, GlobalClass.FYNAME);
                        }
                        if (Vouchers.Count > 0)
                        {
                            strSQL = "INSERT INTO VoucherDiscountDetail (BillNo, FYID, VoucherNo, DiscountAmount, UID) VALUES (@BillNo, @FYID, @VoucherNo, @DiscountAmount, @UID)";
                            foreach (Voucher v in Vouchers)
                            {
                                conn.Execute(strSQL, new
                                {
                                    BillNo = string.IsNullOrEmpty(BillNo) ? "CS1" : BillNo,
                                    FYID = GlobalClass.FYID,
                                    VoucherNo = v.VoucherNo,
                                    DiscountAmount = v.Value,
                                    UID = POUT.UID
                                }, transaction: tran);
                                conn.Execute("UPDATE ParkingVouchers SET ScannedTime = GETDATE() WHERE VoucherNo = @VoucherNo", v, tran);
                            }
                        }
                        else if (mDiscount != null && !string.IsNullOrEmpty(mDiscount.MemberId))
                        {
                            mDiscount.BillNo = string.IsNullOrEmpty(BillNo) ? "MS1" : BillNo;
                            mDiscount.Save(tran);
                        }
                        tran.Commit();
                        //if (!string.IsNullOrEmpty(SyncFunctions.username) && POUT.CashAmount > 0)
                        //{
                        //    SyncFunctions.SyncSalesData(SyncFunctions.getBillObject(BillNo), 1);
                        //}
                    }
                    //if (!string.IsNullOrEmpty(BillNo))
                    //{
                    //    RawPrinterHelper.SendStringToPrinter(GlobalClass.PrinterName, ((char)27).ToString() + ((char)112).ToString() + ((char)0).ToString() + ((char)64).ToString() + ((char)240).ToString(), "Receipt");   //Open Cash Drawer
                    //    PrintBill(BillNo.ToString(), conn, (TaxInvoice) ? "TAX INVOICE" : "ABBREVIATED TAX INVOCE");
                    //    if (TaxInvoice)
                    //    {
                    //        PrintBill(BillNo.ToString(), conn, "INVOICE");
                    //    }
                    //}

                    return new FunctionResponse() { status = "ok", Message = "Saved Successfully" };
                }
            }
            catch (Exception ex)
            {
                return new FunctionResponse() { status = "error", Message = ex.Message };
            }
        }

        public  FunctionResponse ValidateVoucher(BarCodeTransfer obj)
        {
            try
            {
                string barcode = obj.barcode;
                List<Voucher> Vouchers = obj.Vouchers;
                MemberDiscount mDiscount = obj.mDiscount;
                ParkingIn PIN = obj.PIN;
                ParkingOut POUT = obj.POUT;

                if (Vouchers.Any(x => x.Barcode.ToUpper() == barcode.ToUpper().ToString()))
                    return new FunctionResponse() { status = "error", Message = "Voucher already accepted." };

                using (SqlConnection conn = new SqlConnection(GlobalClass.DataConnectionString))
                {
                    Voucher v = conn.Query<Voucher>("SELECT VoucherNo, Barcode, VoucherId, Value, ExpDate, ValidStart, ValidEnd, ScannedTime FROM ParkingVouchers WHERE Barcode = @Barcode", new { Barcode = barcode.ToString() }).FirstOrDefault();
                    if (v == null)
                    {
                        return new FunctionResponse() { status = "error", Message = "InValid Voucher" };
                    }
                    else if (v.ScannedTime == null)
                    {
                        return new FunctionResponse() { status = "error", Message = "Voucher already redeemed." };
                    }
                    else if (!VoucherTypes.Any(x => x.VoucherId == v.VoucherId && x.VehicleType == PIN.VehicleType))
                    {
                        return new FunctionResponse() { status = "error", Message = "The Voucher is not valid for current Entrance Type." };
                    }
                    else if (v.ExpDate < CurDate)
                    {
                        return new FunctionResponse() { status = "error", Message = "Voucher has expired." };
                    }
                    else
                    {
                        TimeSpan outTime = Convert.ToDateTime(POUT.OutTime).TimeOfDay;
                        if (v.ValidStart < v.ValidEnd)
                        {
                            if (outTime < v.ValidStart || outTime > v.ValidEnd)
                            {
                                return new FunctionResponse() { status = "error", Message = "Voucher is not valid for current Shift." };
                            }
                        }
                        else
                        {
                            if (outTime < v.ValidStart && outTime > v.ValidEnd)
                            {
                                return new FunctionResponse() { status = "error", Message = "Voucher is not valid for current Shift." };
                            }
                        }
                        v.Value = (POUT.ChargedAmount > v.Value) ? v.Value : POUT.ChargedAmount;
                        POUT.CashAmount = POUT.CashAmount - v.Value;
                        //PIN.Barcode = string.Empty;
                        Vouchers.Add(v);

                        return new FunctionResponse() { status = "ok", result = obj };

                        //if (POUT.CashAmount == 0)
                        //    ExecuteSave(null);
                        //PoleDisplay.WriteToDisplay(POUT.CashAmount, PoleDisplayType.AMOUNT);
                    }
                }
            }catch(Exception ex)
            {
                return new FunctionResponse() { status = "error", Message = ex.Message };
            }
           
        }

        
        public FunctionResponse ValidateMember(BarCodeTransfer obj)
        {
            try
            {
                string barcode = obj.barcode;
                List<Voucher> Vouchers = obj.Vouchers;
                MemberDiscount mDiscount = obj.mDiscount;
                ParkingIn PIN = obj.PIN;
                ParkingOut POUT = obj.POUT;

                if (mDiscount != null && !string.IsNullOrEmpty(mDiscount.MemberId))
                    return new FunctionResponse() { status = "error", Message = "Member already accepted." };
                TimeSpan InTime = DateTime.Parse(PIN.InTime).TimeOfDay;
                TimeSpan OutTime = DateTime.Parse(POUT.OutTime).TimeOfDay;
                decimal DiscountAmount = 0;
                decimal DiscountHour = 0;
                int Interval;

                using(SqlConnection conn = new SqlConnection(GlobalClass.DataConnectionString))
                {
                    Member m = conn.Query<Member>("SELECT MemberId, MemberName, SchemeId, ExpiryDate, ActivationDate, Barcode, Address FROM Members WHERE Barcode = @MemberId ", new { MemberId = barcode.ToString() }).FirstOrDefault();
                    if (m == null)
                    {
                        return new FunctionResponse() { status = "error", Message = "The member does not exists." };
                    }
                    else if (m.ActivationDate > POUT.OutDate || m.ExpiryDate < POUT.OutDate)
                    {
                        return new FunctionResponse() { status = "error", Message = "The membership is expired or not yet activated." };
                    }
                    MembershipScheme scheme = conn.Query<MembershipScheme>("SELECT * FROM MembershipScheme WHERE SchemeId = @SchemeId", m).FirstOrDefault();
                    if (scheme == null)
                    { return new FunctionResponse() { status = "error", Message = "Membership scheme does not exists." };
                    }
                    else if (!scheme.ValidOnWeekends && POUT.OutDate.DayOfWeek == DayOfWeek.Saturday)
                    { return new FunctionResponse() { status = "error", Message = "The Membership is not valid on Weekends" };
                    }
                    else if (!scheme.ValidOnHolidays && conn.ExecuteScalar<int>("SELECT COUNT(*) FROM Holiday WHERE HolidayDate = @HolidayDate", new { HolidayDate = POUT.OutDate }) > 0)
                    {
                        return new FunctionResponse() { status = "error", Message = "The Membership is not valid on Public Holidays" };
                    }
                    List<dynamic> TimeSpentInEachSession = GetTimeSpentInEachSession(InTime, OutTime, scheme, PIN);
                   
                    Interval = conn.ExecuteScalar<int>("SELECT ISNULL(SUM(MDD.Interval - MDD.SkipInterval),0) Interval FROM MemberDiscountDetail MDD JOIN ParkingOutDetails POD ON MDD.PID = POD.PID WHERE MemberId = @MemberId AND POD.OutDate = @OutDate", new { MemberId = m.MemberId, OutDate = POUT.OutDate });
                    if (Interval >= scheme.Limit && !TimeSpentInEachSession.Any(x => x.SkipValidityPeriod && x.TimeSpent > 0))
                    {
                        return new FunctionResponse() { status = "error", Message = "Free Entrance for the Member has exceeded for day." };
                    }

                    mDiscount = new MemberDiscount
                    {
                        MemberId = m.MemberId,
                        SchemeId = m.SchemeId,
                        FYID = POUT.FYID,
                        PID = PIN.PID,
                    };
                    POUT.BILLTO = m.MemberName;
                    POUT.BILLTOADD = m.Address;

                    if (TimeSpentInEachSession.Any(x => x.SkipValidityPeriod))
                        mDiscount.SkipInterval = TimeSpentInEachSession.Where(x => x.SkipValidityPeriod).Sum(x => x.TimeSpent);

                    int DiscountInterval = scheme.Limit - Interval + mDiscount.SkipInterval;
                    foreach (dynamic session in TimeSpentInEachSession.Where(x => x.TimeSpent > 0))
                    {
                        if (session.IgnoreLimit)
                        {
                            CalculateParkingCharge(conn, session.Start, session.End, POUT.Rate_ID, PIN.VehicleType, ref DiscountAmount, ref DiscountHour);
                            DiscountInterval -= session.TimeSpent;
                        }
                        else
                        {
                            CalculateParkingCharge(conn, session.Start, (DiscountInterval < session.TimeSpent) ? session.Start.AddMinutes(DiscountInterval) : session.End, POUT.Rate_ID, PIN.VehicleType, ref DiscountAmount, ref DiscountHour);
                            DiscountInterval -= (DiscountInterval < session.TimeSpent) ? DiscountInterval : session.TimeSpent;
                        }
                        mDiscount.Interval += DiscountHour * 60;
                        mDiscount.DiscountAmount += DiscountAmount * scheme.Discount / 100;
                        DiscountHour = 0;
                        DiscountAmount = 0;
                    }
                    POUT.CashAmount = POUT.ChargedAmount = POUT.ChargedAmount - mDiscount.DiscountAmount;
                    obj.mDiscount = mDiscount;

                    return new FunctionResponse() { status = "ok", result = obj };

                }              
                
                //if (POUT.CashAmount == 0)
                //    ExecuteSave(null);
                //PoleDisplay.WriteToDisplay(POUT.CashAmount, PoleDisplayType.AMOUNT);

            }catch(Exception ex)
            {
                return new FunctionResponse() { status = "error", Message = ex.Message };
            }
        }

        public FunctionResponse ValidateStaffOrStamp(BarCodeTransfer barCodeTransfer)
        {
            ParkingOut POUT = barCodeTransfer.POUT;
            ParkingIn PIN = barCodeTransfer.PIN;
            try
            {
                using (SqlConnection conn = new SqlConnection(GlobalClass.DataConnectionString))
                {
                    if (POUT.STAFF_BARCODE != "STAMP")
                    {
                        POUT.STAFF_BARCODE = conn.ExecuteScalar<string>("SELECT BARCODE FROM tblStaff WHERE STATUS = 0 AND BCODE = '" + POUT.STAFF_BARCODE + "'");
                        if (string.IsNullOrEmpty(POUT.STAFF_BARCODE))
                        {
                            return new FunctionResponse() { status = "error", Message = "Invalid Barcode. Please Try Again." };
                        }
                        if (GlobalClass.AllowMultiVehicleForStaff == 0)
                        {
                            if (conn.ExecuteScalar<int>
                            (
                                string.Format
                                (
                                    @"SELECT COUNT(*) FROM
                            (
                                SELECT  INDATE + CAST(INTIME AS TIME) INTIME, OUTDATE + CAST(OUTTIME AS TIME) OUTTIME FROM ParkingInDetails PID 
                                JOIN ParkingOutDetails POD ON PID.PID =POD.PID AND PID.FYID = POD.FYID WHERE POD.STAFF_BARCODE = '{0}'
                            ) A WHERE (INTIME < '{1}' AND OUTTIME > '{1}') OR (INTIME > '{1}' AND OUTTIME > '{1}')", POUT.STAFF_BARCODE, PIN.InDate.Add(DateTime.Parse(PIN.InTime).TimeOfDay)
                                )
                            ) > 0)
                            {
                                return new FunctionResponse() { status = "error", Message = "Staff already parked one vehicle during current vehile's parked period. Staff are not allowed to park multiple vehicle at a time" };
                            }
                        }
                    }
                }
                POUT.CashAmount = 0;
                return ExecuteSave(barCodeTransfer);
                
                //if (StaffBarcode != null)
                //    StaffBarcode.Close();
            }
            catch (Exception ex)
            {
                POUT.STAFF_BARCODE = null;
                return new FunctionResponse() { status = "error", Message = ex.Message };
            }
            
        }

        List<dynamic> GetTimeSpentInEachSession(TimeSpan InTime, TimeSpan OutTime, MembershipScheme scheme, ParkingIn PIN)
        {
            DateTime Start = PIN.InDate;
            DateTime End = PIN.InDate;
            TimeSpan Minute = new TimeSpan(0, 1, 0);
            List<dynamic> TimeSpentInEachSession = new List<dynamic>();
          
            foreach (var session in scheme.ValidHoursList)
            {
                TimeSpan TimeSpent = new TimeSpan(0, 0, 0);
                if (InTime >= session.Start && InTime <= session.End)
                {
                    if (OutTime <= session.End.Add(Minute))
                    {
                        TimeSpent = OutTime.Subtract(InTime);
                        End = PIN.InDate.Add(OutTime);
                    }
                    else
                    {
                        TimeSpent = session.End.Add(Minute).Subtract(InTime);
                        End = PIN.InDate.Add(session.End.Add(Minute));
                    }
                    Start = PIN.InDate.Add(InTime);

                }
                else if (InTime < session.Start)
                {
                    if (OutTime <= session.End.Add(Minute))
                    {
                        TimeSpent = OutTime.Subtract(session.Start);
                        End = PIN.InDate.Add(OutTime);
                    }
                    else
                    {
                        TimeSpent = session.End.Add(Minute).Subtract(session.Start);
                        End = PIN.InDate.Add(session.End.Add(Minute));
                    }
                    Start = PIN.InDate.Add(session.Start);
                }
                TimeSpentInEachSession.Add(new { session.SkipValidityPeriod, session.IgnoreLimit, TimeSpent = Convert.ToInt32(TimeSpent.TotalMinutes), Start, End });
            }
            return TimeSpentInEachSession;
        }

        //public static void PrintBill(string BillNo, SqlConnection conn, string InvoiceName, string DuplicateCaption = "")
        //{
        //    DataRow dr;
        //    //// RawPrinterHelper printer = new RawPrinterHelper();
           
        //        dr = conn.Query<dynamic>(string.Format(@"SELECT PS.*,VT.Description VType,ISNULL(PIN.PlateNo,'') PlateNo,PIN.InTime,PIN.InMiti,POUT.OutTime,POUT.OutMiti,U.UserName, POUT.Interval, POUT.ChargedHours FROM ParkingSales PS 
        //                            INNER JOIN Users U ON U.UID=PS.UID
        //                            LEFT JOIN ParkingOutDetails POUT  ON PS.PID = POUT.PID AND PS.FYID = POUT.FYID
        //                            LEFT JOIN (ParkingInDetails PIN   
        //                            LEFT JOIN VehicleType VT ON VT.VTypeID=PIN.VehicleType) ON PS.PID = PIN.PID AND PS.FYID = PIN.FYID
        //                            WHERE BillNo = '{0}' AND PS.FYID = {1}", BillNo, GlobalClass.FYID), conn).FirstOrDefault();

            
        //    string InWords = GlobalClass.GetNumToWords(conn, Convert.ToDecimal(dr["GrossAmount"]));
        //    string strPrint = string.Empty;
        //    int PrintLen = 40;
        //    string Description = dr["Description"].ToString();
        //    string Particulars = "Particulars";
        //    string PAN = "PAN : " + GlobalClass.CompanyPan;
        //    Description = (Description.Length > PrintLen - 17) ? Description.Substring(0, PrintLen - 17) : Description.PadRight(PrintLen - 17, ' ');
        //    Particulars = (Particulars.Length > PrintLen - 17) ? Particulars.Substring(0, PrintLen - 17) : Particulars.PadLeft((PrintLen + Particulars.Length - 17) / 2, ' ').PadRight(PrintLen - 17, ' ');

        //    strPrint += (GlobalClass.CompanyName.Length > PrintLen) ? GlobalClass.CompanyName.Substring(0, PrintLen) : GlobalClass.CompanyName.PadLeft((PrintLen + GlobalClass.CompanyName.Length) / 2, ' ') + Environment.NewLine;
        //    strPrint += (GlobalClass.CompanyAddress.Length > PrintLen) ? GlobalClass.CompanyAddress.Substring(0, PrintLen) : GlobalClass.CompanyAddress.PadLeft((PrintLen + GlobalClass.CompanyAddress.Length) / 2, ' ') + Environment.NewLine;
        //    strPrint += PAN.PadLeft((PrintLen + PAN.Length) / 2, ' ') + Environment.NewLine;
        //    strPrint += InvoiceName.PadLeft((PrintLen + InvoiceName.Length) / 2, ' ') + Environment.NewLine;
        //    if (!string.IsNullOrEmpty(DuplicateCaption))
        //        strPrint += DuplicateCaption.PadLeft((PrintLen + DuplicateCaption.Length) / 2, ' ') + Environment.NewLine;
        //    strPrint += string.Format("Bill No : {0}    Date : {1}", BillNo.PadRight(7, ' '), dr["TMiti"]) + Environment.NewLine;
        //    strPrint += string.Format("Vehicle Type : {0} {1}", dr["VType"], string.IsNullOrEmpty(dr["PlateNo"].ToString()) ? string.Empty : "(" + dr["PlateNo"] + ")") + Environment.NewLine;
        //    strPrint += string.Format("Name    : {0}", dr["BillTo"]) + Environment.NewLine;
        //    strPrint += string.Format("Address : {0}", dr["BillToAdd"]) + Environment.NewLine;
        //    strPrint += string.Format("PAN     : {0}", dr["BillToPan"]) + Environment.NewLine;
        //    strPrint += "".PadRight(PrintLen, '-') + Environment.NewLine;
        //    strPrint += string.Format("Sn.|{0}|  Amount  |", Particulars) + Environment.NewLine;
        //    strPrint += string.Format("1.  {0}  {1}", Description, GParse.ToDecimal(((bool)dr["TaxInvoice"]) ? dr["Amount"] : dr["GrossAmount"]).ToString("#0.00").PadLeft(8, ' ')) + Environment.NewLine;
        //    strPrint += string.Format("    IN  : {0} {1}", dr["InTime"], dr["InMiti"]) + Environment.NewLine;
        //    strPrint += string.Format("    OUT : {0} {1}", dr["OutTime"], dr["OutMiti"]) + Environment.NewLine;
        //    strPrint += string.Format("    Interval : {0} ", dr["Interval"]) + Environment.NewLine;
        //    strPrint += string.Format("    Charged Hours : {0} ", dr["ChargedHours"]) + Environment.NewLine;

        //    strPrint += Environment.NewLine;
        //    strPrint += "------------------------".PadLeft(PrintLen, ' ') + Environment.NewLine;
        //    if ((bool)dr["TaxInvoice"])
        //    {
        //        strPrint += ("Gross Amount : " + GParse.ToDecimal(dr["Amount"]).ToString("#0.00").PadLeft(8, ' ')).PadLeft(PrintLen, ' ') + Environment.NewLine;
        //        if (GParse.ToDecimal(dr["Discount"]) > 0)
        //            strPrint += ("Discount : " + GParse.ToDecimal(dr["Discount"]).ToString("#0.00").PadLeft(8, ' ')).PadLeft(PrintLen, ' ') + Environment.NewLine;
        //        strPrint += ("Taxable : " + GParse.ToDecimal(dr["Taxable"]).ToString("#0.00").PadLeft(8, ' ')).PadLeft(PrintLen, ' ') + Environment.NewLine;
        //        strPrint += ("Non Taxable : " + GParse.ToDecimal(dr["NonTaxable"]).ToString("#0.00").PadLeft(8, ' ')).PadLeft(PrintLen, ' ') + Environment.NewLine;
        //        strPrint += ("VAT 13% : " + GParse.ToDecimal(dr["VAT"]).ToString("#0.00").PadLeft(8, ' ')).PadLeft(PrintLen, ' ') + Environment.NewLine;
        //    }
        //    strPrint += ("Net Amount : " + GParse.ToDecimal(dr["GrossAmount"]).ToString("#0.00").PadLeft(8, ' ')).PadLeft(PrintLen, ' ');
        //    strPrint += Environment.NewLine;
        //    strPrint += "".PadRight(PrintLen, '-') + Environment.NewLine;
        //    strPrint += InWords + Environment.NewLine;
        //    strPrint += "".PadRight(PrintLen, '-') + Environment.NewLine;
        //    strPrint += string.Format("Cashier : {0} ({1})", dr["UserName"], dr["TTime"]) + Environment.NewLine;
        //    strPrint += Environment.NewLine;
        //    strPrint += Environment.NewLine;
        //    strPrint += Environment.NewLine;
        //    strPrint += Environment.NewLine;
        //    strPrint += "".PadRight(PrintLen, '-') + Environment.NewLine;
        //    strPrint += ((char)29).ToString() + ((char)86).ToString() + ((char)1).ToString();

        //    if (GlobalClass.NoRawPrinter)
        //        new StringPrint(strPrint).Print();
        //    else
        //        RawPrinterHelper.SendStringToPrinter(GlobalClass.PrinterName, strPrint, "Receipt");
        //}

        static string  GetInterval(DateTime In, DateTime Out, string InTime, string OutTime)
        {
            var InDate = In.Add(DateTime.Parse(InTime).TimeOfDay);
            var OutDate = Out.Add(DateTime.Parse(OutTime).TimeOfDay);
            var interval = OutDate - InDate;
            return (interval.Days * 24 + interval.Hours).ToString() + " Hrs " + (interval.Minutes).ToString() + " Mins";
        }

        static void CalculateParkingCharge(SqlConnection conn, DateTime InTime, DateTime OutTime, int RateId, int VehicleID, ref decimal ChargedAmount, ref decimal ChargedHours)
        {
            using (SqlCommand cmd = conn.CreateCommand())
            {
                if (conn.State != ConnectionState.Open)
                    conn.Open();
                cmd.CommandText = "sp_Calculate_PCharge";
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.Parameters.AddWithValue("@InTime", InTime);
                cmd.Parameters.AddWithValue("@OutTime", OutTime);
                cmd.Parameters.AddWithValue("@RateId", RateId);
                cmd.Parameters.AddWithValue("@VehicleId", VehicleID);
                using (SqlDataReader dr = cmd.ExecuteReader())
                {
                    if (dr.Read())
                    {
                        ChargedAmount = Convert.ToDecimal(dr[0]);
                        ChargedHours = (decimal)dr[1];
                    }
                }
            }
        }
        
    }
}
