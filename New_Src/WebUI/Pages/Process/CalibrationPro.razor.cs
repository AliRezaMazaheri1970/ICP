using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using MudBlazor;
using System.Text.Json;
using WebUI.Services;

namespace WebUI.Pages.Process
{
    public partial class CalibrationPro
    {
        public class RMElement { public string Label { get; set; } public double Orig { get; set; } public double Curr { get; set; } }
        private List<RMElement> Elements = new List<RMElement>
    {
        new RMElement { Label = "RM 1-15", Orig = -0.04, Curr = -0.04 },
        new RMElement { Label = "RM 1-49", Orig = -0.98, Curr = -0.98 },
        new RMElement { Label = "RM 1-70", Orig = -1.41, Curr = -1.41 },
        new RMElement { Label = "RM 1-98", Orig = -0.29, Curr = -0.29 },
        new RMElement { Label = "RM 1-122", Orig = -0.17, Curr = -0.17 }
    };
    }
}