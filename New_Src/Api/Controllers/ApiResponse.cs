using System;
using System.Collections.Generic;

namespace Api.Controllers
{
    // Simple uniform API response: { data, succeeded, messages }
    public class ApiResponse<T>
    {
        public T? Data { get; set; }
        public bool Succeeded { get; set; }
        public IEnumerable<string> Messages { get; set; }

        public ApiResponse() { Messages = Array.Empty<string>(); }

        public ApiResponse(bool succeeded, T? data, IEnumerable<string>? messages = null)
        {
            Succeeded = succeeded;
            Data = data;
            Messages = messages ?? Array.Empty<string>();
        }
    }
}