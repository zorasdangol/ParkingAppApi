using System;
using System.Collections.Generic;
using System.Text;

namespace ParkingStandardLibrary.Models
{
    public class FunctionResponse<T>
    {
        public string status { get; set; }
        public T result { get; set; }
        public string Message { get; set; }
        public string RefNo { get; set; }
        public string Location { get; set; }

        public FunctionResponse()
        {
            status = "error";
            Message = "Response not set";
        }
    }

    public class FunctionResponse
    {
        public string status { get; set; }
        public object result { get; set; }
        public string RefNo { get; set; }
        public string Location { get; set; }
        public string Message { get; set; }
    }

    public class FunctionResponseEventArgs : EventArgs
    {
        public FunctionResponse Response;
    }
}
